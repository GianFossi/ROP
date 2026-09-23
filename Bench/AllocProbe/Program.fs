// Allocation/time probe backing docs/ZERO-ALLOC.md. Not part of ROP.sln; run with:
//   dotnet run -c Release --project Bench/AllocProbe --framework net8.0   (or net10.0)
// Reports bytes allocated per call (GC.GetAllocatedBytesForCurrentThread) and ns per call (Stopwatch),
// averaged over many calls after a warm-up. Pass a scenario-name fragment as the first argument to filter.

open System
open System.Diagnostics
open ROP
open ROP.Returns.Operators
open ROP.Validation

// Struct prototype: the cheapest honest struct form (warnings still a list).
[<Struct>]
type VReturns<'T,'M> =
    | VSuccess of value:'T * warnings:'M list
    | VFailure of errors:'M list
module V =
    let inline ok x = VSuccess (x, [])
    let inline bind ([<InlineIfLambda>] f: 'a -> VReturns<'b,'m>) (r: VReturns<'a,'m>) =
        match r with
        | VSuccess (x, []) -> f x
        | VSuccess (x, ws) -> (match f x with VSuccess (y, ws2) -> VSuccess (y, ws @ ws2) | VFailure e -> VFailure (ws @ e))
        | VFailure e -> VFailure e

type Msg = Extrapolated of string * string

type Person = { Name: string; Age: int; Height: float; Email: string }

let mutable filter = ""

let inline measure name n ([<InlineIfLambda>] f: int -> float) =
    if filter = "" || (name: string).Contains(filter, StringComparison.OrdinalIgnoreCase) then
        for i in 0 .. min n 200_000 do f i |> ignore   // warm-up / tiering
        GC.Collect()
        let before = GC.GetAllocatedBytesForCurrentThread()
        let sw = Stopwatch.StartNew()
        let mutable acc = 0.0
        for i in 0 .. n - 1 do acc <- acc + f i
        sw.Stop()
        let after = GC.GetAllocatedBytesForCurrentThread()
        printfn "%-62s %9.1f B/call %9.1f ns/call  (checksum %g)"
            name (float (after - before) / float n) (sw.Elapsed.TotalMilliseconds * 1e6 / float n) acc

let inline valueOf r = match r with Success (v, _) -> v | Failure _ -> 0.0

let step1 (x: float) : Returns<float,Msg> = Returns.ok (x + 1.0)
let step1w (x: float) : Returns<float,Msg> = if x > 0.0 then Returns.warn (Extrapolated ("G", "hi")) x else Returns.ok x
let vstep (x: float) : VReturns<float,Msg> = V.ok (x + 1.0)
let vstepw (x: float) : VReturns<float,Msg> = VSuccess (x, [Extrapolated ("G", "hi")])

// Five independent checks on the same value (validation-style).
let chk (limit: float) (x: float) : Returns<float,string> = if x < limit then Returns.ok x else Returns.fail "too big"
let chkU (limit: float) (x: float) : Returns<unit,string> = if x < limit then Returns.ok () else Returns.fail "too big"
let fiveChecks = chk 1e12 &&& chk 2e12 &&& chk 3e12 &&& chk 4e12 &&& chk 5e12
let fiveUnitChecks = [ chkU 1e12; chkU 2e12; chkU 3e12; chkU 4e12; chkU 5e12 ]

let hundred = List.init 100 float
let hundredArr = Array.init 100 float
let hundredReturns = hundred |> List.map (fun x -> Returns.ok x : Returns<float,string>)

let validatePerson =
    createValidatorFor<Person>() {
        validate (fun p -> p.Name)   [ isNotEmptyOrWhitespace; hasMaxLengthOf 50 ]
        validate (fun p -> p.Age)    [ isGreaterThanOrEqualTo 0; isLessThan 150 ]
        validate (fun p -> p.Height) [ isGreaterThan 0.0; isLessThanOrEqualTo 3.0 ]
        validate (fun p -> p.Email)  [ isNotEmpty ]
    }
let people = Array.init 1024 (fun i -> { Name = $"Person number {i}"; Age = i % 100; Height = 1.8; Email = "a@b.c" })

[<EntryPoint>]
let main argv =
    if argv.Length > 0 then filter <- argv.[0]
    let N = 5_000_000
    measure "baseline: plain float arithmetic" N (fun i -> float i * 2.0)
    measure "Returns.ok x" N (fun i -> valueOf (Returns.ok (float i)))
    measure "Returns: ok >>= s >>= s >>= s (clean)" N (fun i -> valueOf (Returns.ok (float i) >>= step1 >>= step1 >>= step1))
    measure "Returns: returns { let! x3 } (clean)" N (fun i ->
        valueOf (returns { let! a = step1 (float i) in let! b = step1 a in let! c = step1 b in return c }))
    measure "Returns: 3 steps, each adds 1 warning" N (fun i -> valueOf (Returns.ok (float i + 1.0) >>= step1w >>= step1w >>= step1w))
    measure "Returns.map (clean)" N (fun i -> valueOf (Returns.map (fun x -> x + 1.0) (step1 (float i))))
    measure "Returns.map2 (clean)" N (fun i -> valueOf (Returns.map2 (+) (step1 (float i)) (step1 1.0)))
    measure "applicative f <!> a <*> b <*> c (clean)" N (fun i ->
        valueOf ((fun a b c -> a + b + c) <!> step1 (float i) <*> step1 1.0 <*> step1 2.0))
    measure "returns { let! a and! b and! c } (clean)" N (fun i ->
        valueOf (returns {
            let! a = step1 (float i)
            and! b = step1 1.0
            and! c = step1 2.0
            return a + b + c }))
    measure "&&& x5 (clean)" N (fun i -> valueOf (fiveChecks (float i)))
    measure "validateAll x5 (clean)" N (fun i -> valueOf (Returns.validateAll fiveUnitChecks (float i)))
    measure "warnIf (false) with interpolated message" N (fun i ->
        let re = 2000.0 + float (i % 2)
        valueOf (Returns.ok (float i) |> Returns.warnIf (fun _ -> re < 1000.0) (Extrapolated ("Gnielinski", $"Re = {re} below 3000"))))
    measure "warnIfLazy (false) with interpolated message" N (fun i ->
        let re = 2000.0 + float (i % 2)
        valueOf (Returns.ok (float i) |> Returns.warnIfLazy (fun _ -> re < 1000.0) (fun () -> Extrapolated ("Gnielinski", $"Re = {re} below 3000"))))
    measure "warnIfWith (false)" N (fun i ->
        valueOf (Returns.ok (float i) |> Returns.warnIfWith (fun v -> v < -1.0) (fun v -> Extrapolated ("Gnielinski", $"v = {v}"))))
    measure "mapWarnings on a clean Success" N (fun i -> valueOf (Returns.ok (float i) |> Returns.mapWarnings id : Returns<float,string>))
    measure "failOnWarnings on a clean Success" N (fun i -> valueOf (Returns.ok (float i) |> Returns.failOnWarnings : Returns<float,string>))
    let M = 100_000
    measure "traverseList 100 elements (clean)" M (fun i -> match Returns.traverseList (fun x -> Returns.ok (x + float i) : Returns<float,string>) hundred with Success (l, _) -> l.Head | _ -> 0.0)
    measure "traverseArray 100 elements (clean)" M (fun i -> match Returns.traverseArray (fun x -> Returns.ok (x + float i) : Returns<float,string>) hundredArr with Success (a, _) -> a.[0] | _ -> 0.0)
    measure "sequenceList 100 elements (clean)" M (fun _ -> match Returns.sequenceList hundredReturns with Success (l, _) -> l.Head | _ -> 0.0)
    measure "fold 100 elements (clean)" M (fun i -> valueOf (Returns.fold (+) (Returns.ok (float i)) hundredReturns))
    measure "Validation DSL: 4 properties, 7 checks (valid record)" N (fun i ->
        match validatePerson people.[i % 1024] with Ok -> 1.0 | Errors _ -> 0.0)
    measure "struct VReturns: ok |> bind s |> bind s |> bind s (clean)" N (fun i -> match V.ok (float i) |> V.bind vstep |> V.bind vstep |> V.bind vstep with VSuccess (v,_) -> v | _ -> 0.0)
    measure "struct VReturns: 1 warning" N (fun i -> match vstepw (float i) with VSuccess (v,_) -> v | _ -> 0.0)
    0
