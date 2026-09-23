/// Load tests: intensive, realistic ROP usage with measured execution time and memory, checked against budgets.
///
/// Every scenario also asserts its functional result, so a budget can never be met by computing the wrong thing.
/// Measurements are printed as "[perf] ..." lines, so a run doubles as a benchmark report.
///
/// Parameters (environment variables):
///   ROP_PERF_TIME_SCALE  multiplies every time budget (default 1.0); raise it on slow or heavily shared machines.
///   ROP_PERF_SKIP=1      skips the load tests entirely.
/// Memory budgets are not scaled: bytes allocated per item are deterministic for a given build configuration
/// (identical on net8.0 and net10.0), so exceeding one means the code changed, not the machine. They are set
/// separately for Debug and Release builds (see `bytes`), because Debug keeps closures that Release optimizes away.
module PerformanceTests

open System
open System.Diagnostics
open System.Globalization
open Expecto
open ROP
open ROP.Returns.Operators
open ROP.Validation

// ============================================================
// Measurement infrastructure
// ============================================================

/// The limits one scenario must stay within.
type PerfBudget =
    { /// Wall-clock time of one measured run, in milliseconds (multiplied by ROP_PERF_TIME_SCALE).
      MaxMilliseconds: float
      /// Bytes allocated per processed item during the measured run.
      MaxBytesPerItem: float }

/// True when the test project is built in Debug (unoptimized) configuration.
let isDebugBuild =
#if DEBUG
    true
#else
    false
#endif

/// Bytes-per-item budget for the current build configuration.
let bytes (release: float) (debug: float) = if isDebugBuild then debug else release

/// What one measured run cost.
type PerfReport =
    { Name: string
      Items: int
      Milliseconds: float
      BytesAllocated: int64
      Gen0Collections: int }
    member r.BytesPerItem = float r.BytesAllocated / float r.Items
    member r.NanosecondsPerItem = r.Milliseconds * 1e6 / float r.Items

let private envFloat name fallback =
    match Environment.GetEnvironmentVariable name with
    | null | "" -> fallback
    | s ->
        match Double.TryParse (s, NumberStyles.Float, CultureInfo.InvariantCulture) with
        | true, v when v > 0.0 -> v
        | _ -> fallback

/// Multiplier applied to every time budget.
let timeScale = envFloat "ROP_PERF_TIME_SCALE" 1.0

/// True when ROP_PERF_SKIP=1.
let skipPerf = Environment.GetEnvironmentVariable "ROP_PERF_SKIP" = "1"

/// Runs the workload once to warm up (JIT, type loading, first-call caches), then measures a second run:
/// wall-clock time, bytes allocated on this thread, and gen-0 collections. Returns the report and the result of
/// the measured run.
let measure (name: string) (items: int) (workload: unit -> 'T) : PerfReport * 'T =
    workload () |> ignore
    GC.Collect ()
    GC.WaitForPendingFinalizers ()
    GC.Collect ()
    let gen0 = GC.CollectionCount 0
    let before = GC.GetAllocatedBytesForCurrentThread ()
    let sw = Stopwatch.StartNew ()
    let result = workload ()
    sw.Stop ()
    let report =
        { Name = name
          Items = items
          Milliseconds = sw.Elapsed.TotalMilliseconds
          BytesAllocated = GC.GetAllocatedBytesForCurrentThread () - before
          Gen0Collections = GC.CollectionCount 0 - gen0 }
    printfn "[perf] %-58s %9d items %9.1f ms %9.1f ns/item %9.1f B/item  gen0=%d"
        name items report.Milliseconds report.NanosecondsPerItem report.BytesPerItem report.Gen0Collections
    report, result

/// Fails the test if the report exceeds the budget, saying by how much.
let checkBudget (budget: PerfBudget) (report: PerfReport) =
    let maxMs = budget.MaxMilliseconds * timeScale
    if report.Milliseconds > maxMs then
        failtestf "%s: took %.1f ms, budget %.1f ms (ROP_PERF_TIME_SCALE=%g)" report.Name report.Milliseconds maxMs timeScale
    if report.BytesPerItem > budget.MaxBytesPerItem then
        failtestf "%s: allocated %.1f B/item, budget %.1f B/item" report.Name report.BytesPerItem budget.MaxBytesPerItem

/// A load test: builds the input with `prepare` (not measured), measures `workload` on it over `items` items,
/// verifies the result, then checks the budget.
let perfTest name items (budget: PerfBudget) (prepare: unit -> 'I) (workload: 'I -> 'T) (verify: 'T -> unit) =
    testCase name (fun () ->
        if skipPerf then skiptest "ROP_PERF_SKIP=1"
        let input = prepare ()
        let report, result = measure name items (fun () -> workload input)
        verify result
        checkBudget budget report)

/// Guards against accidental quadratic behaviour: runs the workload at n and 2n items and fails if the bytes
/// allocated per item grow by more than `maxGrowth` (≈1.0 for linear code, ≈2.0 for quadratic code).
let linearityTest name (n: int) (maxGrowth: float) (prepare: int -> 'I) (workload: 'I -> 'T) =
    testCase name (fun () ->
        if skipPerf then skiptest "ROP_PERF_SKIP=1"
        let smallInput, largeInput = prepare n, prepare (2 * n)
        let small, _ = measure $"{name} (n = {n})" n (fun () -> workload smallInput)
        let large, _ = measure $"{name} (n = {2 * n})" (2 * n) (fun () -> workload largeInput)
        let growth = large.BytesPerItem / small.BytesPerItem
        if growth > maxGrowth then
            failtestf "%s: bytes/item grew %.2fx from n=%d to n=%d (max %.2fx): cost is not linear" name growth n (2 * n) maxGrowth)

// ============================================================
// Scenario 1: marching solver (validation, sequential chain, lazy warnings, post-condition, context)
// ============================================================

type NodeMsg =
    | NonPositive of field: string * node: int
    | Extrapolated of correlation: string * node: int
    | WallTooHot of node: int * temperature: float
    | AtNode of node: int * inner: NodeMsg

[<Struct>]
type NodeInput = { Node: int; Re: float; Pr: float; TBulk: float }

let private positive field (n: NodeInput) (v: float) : Returns<NodeInput,NodeMsg> =
    if v > 0.0 then Returns.ok n else Returns.fail (NonPositive (field, n.Node))

/// Three independent input checks, all reported together (&&&).
let private validateNode =
    (fun (n: NodeInput) -> positive "Re" n n.Re)
    &&& (fun n -> positive "Pr" n n.Pr)
    &&& (fun n -> positive "TBulk" n n.TBulk)

let private solveNode (n: NodeInput) : Returns<float,NodeMsg> =
    validateNode n
    >>= (fun n ->
            Returns.ok (0.023 * n.Re ** 0.8 * n.Pr ** 0.4)
            |> Returns.warnIfLazy (fun _ -> n.Re < 10000.0) (fun () -> Extrapolated ("Dittus-Boelter", n.Node)))
    >>= (fun nu -> Returns.ok (nu * 0.6 / 0.02))            // heat-transfer coefficient
    >>= (fun h -> Returns.ok (n.TBulk + 5000.0 / h))         // wall temperature
    |> Returns.filterWith (fun tw -> tw < 400.0) (fun tw -> WallTooHot (n.Node, round tw))
    |> Returns.withContextBy (fun node m -> AtNode (node, m)) n.Node   // generic context: an int, no string built

let private solveMarch (nodes: NodeInput array) =
    nodes
    |> Returns.traverseArray solveNode
    |> Returns.summariseWarnings (function Extrapolated (correlation, _) -> correlation | other -> string other)

let private marchNodes count defectEvery =
    Array.init count (fun i ->
        { Node = i
          Re = (if defectEvery > 0 && i % defectEvery = 0 then -1.0 else 5000.0 + 0.5 * float i)
          Pr = 5.0
          TBulk = 300.0 + 0.001 * float i })

// ============================================================
// Scenario 2: batch record validation with the Validation DSL
// ============================================================

type Equipment = { Tag: string; DesignPressure: float; DesignTemperature: int; Material: string; Notes: string }

let private validateEquipment =
    createValidatorFor<Equipment>() {
        validate (fun e -> e.Tag)               [ isNotEmptyOrWhitespace; hasMaxLengthOf 20 ]
        validate (fun e -> e.DesignPressure)    [ isGreaterThan 0.0; isLessThanOrEqualTo 400.0 ]
        validate (fun e -> e.DesignTemperature) [ isGreaterThanOrEqualTo -196; isLessThan 900 ]
        validate (fun e -> e.Material)          [ isNotEmpty ]
        validate (fun e -> e.Notes)             [ hasMaxLengthOf 200 ]
    }

let private equipment count =
    Array.init count (fun i ->
        { Tag = $"E-{i:D5}"
          DesignPressure = (if i % 10 = 0 then -1.0 else 10.0 + float (i % 300))   // every 10th record invalid
          DesignTemperature = 20 + i % 500
          Material = "SA-516 Gr.70"
          Notes = "" })

// ============================================================
// Scenario 3: computation-expression-heavy pipeline
// ============================================================

let private measureChannel (i: int) : Returns<float,string> =
    if i % 1000 = 999 then Returns.warn "channel near saturation" (float i) else Returns.ok (float i)

let private cePipeline (i: int) : Returns<float,string> =
    returns {
        let! a = measureChannel i
        and! b = measureChannel (i + 1)
        and! c = measureChannel (i + 2)
        let! mean = Returns.ok ((a + b + c) / 3.0)
        let! scaled = Returns.ok (mean * 1.5) |> Returns.filter (fun v -> v >= 0.0) "negative reading"
        return scaled
    }

// ============================================================
// Tests
// ============================================================

let performanceTests =
    testSequenced <| testList "Performance - intensive usage (time and memory budgets)" [

        // Budgets: time is about 5x the slowest measured run (Debug build) on the reference machine, scaled by
        // ROP_PERF_TIME_SCALE; bytes/item is about 1.2x the measured value for each build configuration.

        // --- Scenario 1 -------------------------------------------------------------------------------------
        perfTest "marching solver: 50k nodes, clean" 50_000
            { MaxMilliseconds = 250.0; MaxBytesPerItem = bytes 390.0 810.0 }
            (fun () -> marchNodes 50_000 0)
            solveMarch
            (fun r ->
                match r with
                | Success (walls, warnings) ->
                    Expect.equal walls.Length 50_000 "one wall temperature per node"
                    Expect.equal warnings [ (Extrapolated ("Dittus-Boelter", 0), 10_000) ] "10k extrapolations, summarised into one entry"
                | Failure errs -> failtestf "expected Success, got %d errors" errs.Length)

        perfTest "marching solver: 50k nodes, 50 defective" 50_000
            { MaxMilliseconds = 250.0; MaxBytesPerItem = bytes 370.0 790.0 }
            (fun () -> marchNodes 50_000 1000)
            solveMarch
            (fun r ->
                let errs = Testing.expectFailure r
                Expect.equal errs.Length 50 "every defective node reported (traverseArray accumulates)"
                Expect.equal (fst errs.Head) (AtNode (0, NonPositive ("Re", 0))) "each error carries its node as context"
                Expect.equal (snd errs.Head) 1 "errors are not collapsed")

        // --- Scenario 2 -------------------------------------------------------------------------------------
        perfTest "Validation DSL: 20k equipment records, 10% invalid" 20_000
            { MaxMilliseconds = 75.0; MaxBytesPerItem = bytes 180.0 180.0 }
            (fun () -> equipment 20_000)
            (Array.sumBy (fun e -> match validateEquipment e with Ok -> 0 | Errors es -> es.Length))
            (fun errorCount -> Expect.equal errorCount 2_000 "exactly one error per invalid record")

        // --- Scenario 3 -------------------------------------------------------------------------------------
        perfTest "CE pipeline: 100k runs of let!/and!/and!/let!/filter" 100_000
            { MaxMilliseconds = 250.0; MaxBytesPerItem = bytes 390.0 600.0 }
            ignore
            (fun () ->
                let mutable total = 0.0
                let mutable warnings = 0
                for i in 0 .. 99_999 do
                    match cePipeline i with
                    | Success (v, ws) -> total <- total + v; warnings <- warnings + ws.Length
                    | Failure _ -> ()
                total, warnings)
            (fun (total, warnings) ->
                Expect.floatClose Accuracy.high total (1.5 * (float 99_999 * 100_000.0 / 2.0 + 100_000.0)) "sum of scaled means"
                Expect.equal warnings 300 "each saturated channel is seen by three consecutive runs")

        perfTest "CE for-loop: 200k iterations collecting warnings" 200_000
            { MaxMilliseconds = 150.0; MaxBytesPerItem = bytes 120.0 120.0 }
            ignore
            (fun () -> returns { for i in 0 .. 199_999 do do! measureChannel i |> Returns.map ignore })
            (fun r -> Expect.equal (Testing.getWithWarnings r |> snd |> List.length) 200 "one warning per 1000 iterations")

        // --- Collections at scale / stack safety ------------------------------------------------------------
        perfTest "traverseList over 1M elements (stack safety)" 1_000_000
            { MaxMilliseconds = 700.0; MaxBytesPerItem = bytes 90.0 90.0 }
            (fun () -> [ 0 .. 999_999 ])
            (Returns.traverseList measureChannel)
            (fun r -> Expect.equal (Testing.getWithWarnings r |> snd |> List.length) 1_000 "one warning per 1000 elements")

        perfTest ">>= chain of 1M steps (stack safety)" 1_000_000
            { MaxMilliseconds = 600.0; MaxBytesPerItem = bytes 120.0 215.0 }
            ignore
            (fun () -> Seq.fold (fun acc i -> acc >>= (fun s -> measureChannel i |> Returns.map ((+) s))) (Returns.ok 0.0) (seq { 0 .. 999_999 }))
            (fun r -> Expect.equal (Testing.getWithWarnings r |> fst) (float 999_999 * 1_000_000.0 / 2.0) "sum of all channels")

        perfTest "fold over 1M Returns" 1_000_000
            { MaxMilliseconds = 100.0; MaxBytesPerItem = bytes 8.0 8.0 }
            (fun () -> Array.init 1_000_000 measureChannel)
            (Returns.fold (+) (Returns.ok 0.0))
            (fun r -> Expect.equal (Testing.getWithWarnings r |> fst) (float 999_999 * 1_000_000.0 / 2.0) "sum of all channels")

        // --- Linearity guards (memory per item must not grow with size) --------------------------------------
        linearityTest "linearity: traverseList" 200_000 1.3
            (fun n -> [ 0 .. n - 1 ])
            (Returns.traverseList measureChannel)
        linearityTest "linearity: validateAll with every validator warning" 20_000 1.3
            (fun n -> List.init n (fun _ -> fun (_: int) -> Returns.warn "w" ()))
            (fun validators -> Returns.validateAll validators 0)
        linearityTest "linearity: >>= chain with a warning every step" 100_000 1.3
            id
            (fun n -> Seq.fold (fun acc _ -> acc >>= (fun s -> Returns.warn "w" (s + 1))) (Returns.ok 0) (seq { 1 .. n }))
        linearityTest "linearity: CE for-loop" 100_000 1.3
            id
            (fun n -> returns { for i in 0 .. n - 1 do do! measureChannel i |> Returns.map ignore })
    ]
