// ====================================================================================================== //
// Examples.Returns.HeatExchanger.fsx
//
// Comprehensive demonstration of the ROP (Railway-Oriented Programming) library.
// Domain: Shell-and-Tube Heat Exchanger — input validation and calculation.
//
// Sections:
//   A.  Domain types and message discriminated union
//   B.  Basic constructors: ok, warn, warnmany, fail, failmany
//   C.  Sequential pipeline with >>= and >=>
//   D.  Parallel applicative with <!> and <*>
//   E.  validateAll — collect ALL errors/warnings from independent validators
//   F.  Computation expression: let! (monadic sequential)
//   G.  Computation expression: and! (parallel MergeSources)
//   H.  warnIf — conditional soft-limit warnings
//   I.  mapWarnings / mapErrors — targeted message transformation
//   J.  traverseList / sequenceList — validated collection processing
//   K.  Tee and injectable log
//   L.  Validation module (createValidatorFor<'T> CE builder)
//   M.  Full integrated pipeline
// ====================================================================================================== //

#load "Returns.fs"
#load "Validation.fs"

open System
open ROP
open ROP.Validation

// ====================================================================================================== //
// A. DOMAIN TYPES
// ====================================================================================================== //

/// Shell side geometry (all SI units: metres, Pa).
type Shell = { InnerDiameter: double; Length: double; Material: string }

/// Tube bundle geometry.
type Tube  = { Count: int; OuterDiameter: double; Thickness: double }

/// Operating conditions.
type Operating =
    { ShellInletTemp:  double   // °C
      ShellOutletTemp: double   // °C
      TubeInletTemp:   double   // °C
      TubeOutletTemp:  double   // °C
      DesignPressure:  double } // bar(g)

/// Assembled heat exchanger.
type HeatExchanger = { Shell: Shell; Tube: Tube; Operating: Operating }

/// All possible messages — both errors and warnings — for HX validation and calculation.
type HXMessage =
    // ── Errors ──────────────────────────────────────────────────────────────
    | NegativeValue       of name:string * value:double
    | ZeroValue           of name:string
    | NonPositiveCount    of name:string * value:int
    | TubeLargerThanShell of tubeOD:double * shellID:double
    | TemperatureCrossover
    // ── Warnings ────────────────────────────────────────────────────────────
    | LowEfficiency    of eta:double
    | HighPressure     of pressure:double
    | MaterialAdvisory of note:string

// ====================================================================================================== //
// B. BASIC CONSTRUCTORS
// ====================================================================================================== //

printfn "\n=== B. Basic constructors ==="

let r1 = Returns.ok<double,HXMessage> 0.5
// Success (0.5, [])

let r2 = Returns.warn (LowEfficiency 0.42) 0.42
// Success (0.42, [LowEfficiency 0.42])

let r3 = Returns.warnmany [LowEfficiency 0.42; HighPressure 150.0] 0.42
// Success (0.42, [LowEfficiency 0.42; HighPressure 150.0])

let r4 = Returns.fail<double,HXMessage> (NegativeValue ("diameter", -0.3))
// Failure [NegativeValue ("diameter", -0.3)]

let r5 = Returns.failmany<double,HXMessage> [NegativeValue ("diameter", -0.3); ZeroValue "length"]
// Failure [NegativeValue ("diameter", -0.3); ZeroValue "length"]

printfn "ok       : %A" r1
printfn "warn     : %A" r2
printfn "warnmany : %A" r3
printfn "fail     : %A" r4
printfn "failmany : %A" r5

// ====================================================================================================== //
// C. SEQUENTIAL PIPELINE WITH >>= AND >=>
// ====================================================================================================== //

printfn "\n=== C. Sequential pipeline (short-circuits on first error) ==="

let requirePositive name (value:double) : Returns<double,HXMessage> =
    if value <= 0.0 then Returns.fail (NegativeValue (name, value))
    else Returns.ok value

let requireShellDiameterRange (value:double) : Returns<double,HXMessage> =
    if value < 0.050 then Returns.fail (NegativeValue ("shellID below 50 mm", value))
    elif value > 3.000 then Returns.fail (NegativeValue ("shellID above 3000 mm", value))
    else Returns.ok value

// >=> composes two switch functions: each sees the output of the previous step.
let validateShellDiameter =
    requirePositive "InnerDiameter"
    >=> requireShellDiameterRange

validateShellDiameter 0.300 |> printfn "shellDiam 0.300  : %A"  // Success (0.3, [])
validateShellDiameter -0.10 |> printfn "shellDiam -0.100 : %A"  // Failure [NegativeValue]
validateShellDiameter 5.000 |> printfn "shellDiam 5.000  : %A"  // Failure [NegativeValue above 3000 mm]

// Using >>= inline — each step only runs when the previous succeeded.
let validateShellGeometry (shell:Shell) : Returns<Shell,HXMessage> =
    shell
    |> Returns.ok
    >>= (fun s ->
        if s.InnerDiameter <= 0.0
        then Returns.fail (NegativeValue ("InnerDiameter", s.InnerDiameter))
        else Returns.ok s)
    >>= (fun s ->
        if s.Length <= 0.0
        then Returns.fail (NegativeValue ("Length", s.Length))
        else Returns.ok s)

validateShellGeometry { InnerDiameter = 0.5; Length = 4.0; Material = "CS" }
|> printfn "shellGeo valid   : %A"  // Success

validateShellGeometry { InnerDiameter = -0.1; Length = 4.0; Material = "CS" }
|> printfn "shellGeo bad ID  : %A"  // Failure [NegativeValue InnerDiameter] — Length not checked

// ====================================================================================================== //
// D. PARALLEL APPLICATIVE WITH <!> AND <*>
// ====================================================================================================== //

printfn "\n=== D. Parallel applicative (accumulates ALL errors) ==="

let validateTubeCount (count:int) : Returns<int,HXMessage> =
    if count <= 0 then Returns.fail (NonPositiveCount ("TubeCount", count))
    else Returns.ok count

let validateTubeOD (od:double) : Returns<double,HXMessage> =
    if od <= 0.0 then Returns.fail (NegativeValue ("TubeOD", od))
    else Returns.ok od

let validateTubeThickness (t:double) : Returns<double,HXMessage> =
    if t <= 0.0 then Returns.fail (NegativeValue ("TubeThickness", t))
    else Returns.ok t

// Each field is validated independently; <!> lifts the constructor, <*> applies each argument.
let createTube count od thickness : Returns<Tube,HXMessage> =
    fun c o t -> { Count = c; OuterDiameter = o; Thickness = t }
    <!> validateTubeCount     count
    <*> validateTubeOD        od
    <*> validateTubeThickness thickness

createTube 19  0.019  0.002 |> printfn "tube valid      : %A"  // Success
createTube  0  0.019  0.002 |> printfn "tube bad count  : %A"  // Failure [NonPositiveCount]
createTube  0 -0.010  0.002 |> printfn "tube 2 errors   : %A"  // Failure [NonPositiveCount; NegativeValue]
createTube  0 -0.010 -0.001 |> printfn "tube 3 errors   : %A"  // Failure [3 errors]

// ====================================================================================================== //
// E. validateAll — RUN EVERY VALIDATOR, COLLECT ALL ERRORS AND WARNINGS
// ====================================================================================================== //

printfn "\n=== E. validateAll (no short-circuit; all validators run) ==="

// Each validator: Operating -> Returns<unit,HXMessage>.
let checkTempCrossover (op:Operating) : Returns<unit,HXMessage> =
    if op.ShellInletTemp <= op.ShellOutletTemp
    then Returns.fail TemperatureCrossover
    else Returns.ok ()

let checkDesignPressure (op:Operating) : Returns<unit,HXMessage> =
    if op.DesignPressure <= 0.0
    then Returns.fail (NegativeValue ("DesignPressure", op.DesignPressure))
    else Returns.ok ()

let warnHighPressure (op:Operating) : Returns<unit,HXMessage> =
    if op.DesignPressure > 100.0
    then Returns.warn (HighPressure op.DesignPressure) ()
    else Returns.ok ()

// validateAll runs all three validators and merges all messages.
let validateOperating : Operating -> Returns<Operating,HXMessage> =
    Returns.validateAll [ checkTempCrossover; checkDesignPressure; warnHighPressure ]

let opValid   = { ShellInletTemp=120.0; ShellOutletTemp=80.0; TubeInletTemp=25.0; TubeOutletTemp=60.0; DesignPressure= 50.0 }
let opBadAll  = { ShellInletTemp= 60.0; ShellOutletTemp=80.0; TubeInletTemp=25.0; TubeOutletTemp=60.0; DesignPressure= -5.0 }
let opHighP   = { ShellInletTemp=120.0; ShellOutletTemp=80.0; TubeInletTemp=25.0; TubeOutletTemp=60.0; DesignPressure=150.0 }

validateOperating opValid  |> printfn "validateAll ok    : %A"  // Success (opValid, [])
validateOperating opBadAll |> printfn "validateAll 2errs : %A"  // Failure [TemperatureCrossover; NegativeValue]
validateOperating opHighP  |> printfn "validateAll warn  : %A"  // Success (opHighP, [HighPressure 150.0])

// ====================================================================================================== //
// F. COMPUTATION EXPRESSION: let! (monadic, short-circuits on first error)
// ====================================================================================================== //

printfn "\n=== F. CE with let! (sequential, stops on first error) ==="

let buildShell (innerDiam:double) (length:double) (material:string) : Returns<Shell,HXMessage> =
    returns {
        let! id =
            if innerDiam <= 0.0
            then Returns.fail (NegativeValue ("InnerDiameter", innerDiam))
            else Returns.ok innerDiam

        let! len =
            if length <= 0.0
            then Returns.fail (NegativeValue ("Length", length))
            else Returns.ok length

        let! mat =
            if String.IsNullOrWhiteSpace material
            then Returns.fail (ZeroValue "Material")
            else Returns.ok material

        return { InnerDiameter = id; Length = len; Material = mat }
    }

buildShell 0.5  4.0 "CS"  |> printfn "buildShell valid  : %A"  // Success
buildShell -0.1 4.0 "CS"  |> printfn "buildShell bad ID : %A"  // Failure [NegativeValue InnerDiameter]
buildShell 0.5  0.0 "CS"  |> printfn "buildShell bad L  : %A"  // Failure [NegativeValue Length]
buildShell 0.5  4.0 "  "  |> printfn "buildShell bad mat: %A"  // Failure [ZeroValue Material]

// ====================================================================================================== //
// G. COMPUTATION EXPRESSION: and! (parallel MergeSources — accumulates BOTH failures)
// ====================================================================================================== //

printfn "\n=== G. CE with and! (parallel binding, merges errors from both branches) ==="

let buildHX (shell:Shell) (tube:Tube) (op:Operating) : Returns<HeatExchanger,HXMessage> =
    returns {
        // Shell and tube/op are evaluated INDEPENDENTLY; both failure lists are merged.
        let! validShell = validateShellGeometry shell
        and! validTube  = createTube tube.Count tube.OuterDiameter tube.Thickness
        and! validOp    = validateOperating op

        // Cross-check only runs if all three above succeeded.
        do! if validTube.OuterDiameter >= validShell.InnerDiameter
            then Returns.fail (TubeLargerThanShell (validTube.OuterDiameter, validShell.InnerDiameter))
            else Returns.ok ()

        return { Shell = validShell; Tube = validTube; Operating = validOp }
    }

let goodShell = { InnerDiameter = 0.5; Length = 4.0; Material = "CS" }
let goodTube  = { Count = 19; OuterDiameter = 0.019; Thickness = 0.002 }

buildHX goodShell goodTube opValid
|> printfn "buildHX valid   : %A"   // Success

buildHX { goodShell with InnerDiameter = -0.5 } { goodTube with Count = 0 } opBadAll
|> printfn "buildHX 4 errs  : %A"   // Failure [NegativeValue; NonPositiveCount; TemperatureCrossover; NegativeValue]

// ====================================================================================================== //
// H. warnIf — CONDITIONAL SOFT-LIMIT WARNINGS
// ====================================================================================================== //

printfn "\n=== H. warnIf (value passes through; warning appended when predicate holds) ==="

/// Log-mean temperature difference for counter-current flow.
let calcLMTD (op:Operating) : Returns<double,HXMessage> =
    let dt1 = op.ShellInletTemp  - op.TubeOutletTemp    // hot end
    let dt2 = op.ShellOutletTemp - op.TubeInletTemp     // cold end
    if dt1 <= 0.0 || dt2 <= 0.0 then
        Returns.fail TemperatureCrossover
    else
        let lmtd = (dt1 - dt2) / Math.Log(dt1 / dt2)
        Returns.ok lmtd
        // Soft warning when LMTD is below 10 °C (low driving force).
        |> Returns.warnIf (fun v -> v < 10.0) (LowEfficiency 0.0)

calcLMTD opValid
|> printfn "LMTD normal  : %A"   // Success (lmtd, [])

// Tight temperatures — LMTD < 10, so warning is appended.
let opTightDT = { opValid with ShellOutletTemp = 27.0; TubeOutletTemp = 26.5 }
calcLMTD opTightDT
|> printfn "LMTD tight   : %A"   // Success (lmtd, [LowEfficiency 0.0])

// Crossover — outright failure.
let opCross = { opValid with ShellOutletTemp = 20.0 }
calcLMTD opCross
|> printfn "LMTD crossov : %A"   // Failure [TemperatureCrossover]

// ====================================================================================================== //
// I. mapWarnings / mapErrors — TARGETED MESSAGE TRANSFORMATION
// ====================================================================================================== //

printfn "\n=== I. mapWarnings / mapErrors ==="

let normaliseWarning msg =
    match msg with
    | LowEfficiency eta -> LowEfficiency (Math.Round(eta, 3))   // normalise precision
    | HighPressure p    -> HighPressure  (Math.Round(p,   1))
    | other             -> other

let normaliseError msg =
    match msg with
    | NonPositiveCount (name, v) -> NegativeValue (name, double v)  // unify error types
    | other                      -> other

let withWarning = Returns.warn (LowEfficiency 0.423456789) 42.0
let withError   = Returns.fail<int,HXMessage> (NonPositiveCount ("n", -3))

withWarning |> Returns.mapWarnings normaliseWarning |> printfn "mapWarnings      : %A"   // LowEfficiency 0.423
withError   |> Returns.mapErrors   normaliseError   |> printfn "mapErrors        : %A"   // NegativeValue ("n",-3.0)

// Cross-application: mapErrors on Success and mapWarnings on Failure are both no-ops.
withWarning |> Returns.mapErrors   normaliseError   |> printfn "mapErrors/success: %A"   // unchanged
withError   |> Returns.mapWarnings normaliseWarning |> printfn "mapWarn/failure  : %A"   // unchanged

// ====================================================================================================== //
// J. traverseList / sequenceList — VALIDATED COLLECTION PROCESSING
// ====================================================================================================== //

printfn "\n=== J. traverseList / sequenceList ==="

/// Validate a single tube outer diameter: must be positive and ≤ 100 mm.
let validateSingleTubeOD (od:double) : Returns<double,HXMessage> =
    if od <= 0.0    then Returns.fail (NegativeValue ("TubeOD", od))
    elif od > 0.100 then Returns.fail (NegativeValue ("TubeOD > 100 mm", od))
    else Returns.ok od

let validDiameters = [ 0.019; 0.025; 0.032 ]
let badDiameters   = [ 0.019; -0.010; 0.200 ]   // two bad values

Returns.traverseList validateSingleTubeOD validDiameters
|> printfn "traverseList ok   : %A"   // Success ([0.019; 0.025; 0.032], [])

Returns.traverseList validateSingleTubeOD badDiameters
|> printfn "traverseList 2bad : %A"   // Failure [NegativeValue; NegativeValue] — both errors collected

// sequenceList: when you already have a list of Returns values.
let preBuilt : Returns<double,HXMessage> list =
    [ Returns.ok 0.019
      Returns.warn (HighPressure 110.0) 0.025
      Returns.ok 0.032 ]

Returns.sequenceList preBuilt
|> printfn "sequenceList warn : %A"   // Success ([0.019; 0.025; 0.032], [HighPressure 110.0])

let preBuiltWithError : Returns<double,HXMessage> list =
    [ Returns.ok 0.019
      Returns.fail (NegativeValue ("TubeOD", -0.01))
      Returns.ok 0.032 ]

Returns.sequenceList preBuiltWithError
|> printfn "sequenceList err  : %A"   // Failure [NegativeValue]

// ====================================================================================================== //
// K. TEE AND INJECTABLE LOG
// ====================================================================================================== //

printfn "\n=== K. Tee and injectable log ==="

let myLogger = printfn "[LOG] %s"

let loggedResult =
    buildShell 0.5 4.0 "CS"
    |> Returns.log myLogger true "after buildShell"
    |> Returns.successTee (fun (shell, _) -> printfn "[TEE] Shell: ID=%.3f m, L=%.1f m" shell.InnerDiameter shell.Length)
    |> Returns.map (fun s -> { s with Material = s.Material + "_coated" })
    |> Returns.log myLogger true "after material update"

loggedResult |> printfn "final  : %A"

let failedResult =
    buildShell -0.5 4.0 "CS"
    |> Returns.log myLogger true "after bad buildShell"
    |> Returns.failureTee (fun errs -> printfn "[TEE] Errors: %A" errs)

failedResult |> printfn "failed : %A"

// ====================================================================================================== //
// L. VALIDATION MODULE — createValidatorFor<'T> CE BUILDER
// ====================================================================================================== //

printfn "\n=== L. Validation module (property-level CE builder) ==="

/// Validate Operating conditions via the property-level DSL.
let operatingValidator =
    createValidatorFor<Operating>() {
        validate (fun o -> o.DesignPressure)  [ isGreaterThan 0.0; isLessThanOrEqualTo 200.0 ]
        validate (fun o -> o.ShellInletTemp)  [ isGreaterThan 0.0 ]
        validate (fun o -> o.ShellOutletTemp) [ isGreaterThan 0.0 ]
        validate (fun o -> o.TubeInletTemp)   [ isGreaterThan 0.0 ]
        validate (fun o -> o.TubeOutletTemp)  [ isGreaterThan 0.0 ]
        validateWhen
            (fun o -> o.DesignPressure > 100.0)
            (fun o -> o.DesignPressure)
            [ isLessThanOrEqualTo 200.0 ]
    }

let badOp = { opValid with DesignPressure = -5.0; ShellInletTemp = -10.0 }

operatingValidator opValid
|> printfn "Validation valid  : %A"   // Ok

operatingValidator badOp
|> printfn "Validation errors : %A"   // Errors [isGreaterThan; isGreaterThan]

// ====================================================================================================== //
// M. FULL INTEGRATED PIPELINE
// ====================================================================================================== //

printfn "\n=== M. Full integrated pipeline ==="

/// Calculate heat transfer area from a validated HX.
let computeArea (hx:HeatExchanger) : Returns<double,HXMessage> =
    let area = Math.PI * hx.Tube.OuterDiameter * double hx.Tube.Count * hx.Shell.Length
    Returns.ok area
    |> Returns.warnIf (fun a -> a < 1.0) (LowEfficiency 0.0)

/// Build, validate, compute, and annotate — a single composable pipeline.
let runHX shellID shellLen material tubeCount tubeOD tubeThk shellTin shellTout tubeTin tubeTout pressure =
    let shell = { InnerDiameter = shellID; Length = shellLen; Material = material }
    let tube  = { Count = tubeCount; OuterDiameter = tubeOD; Thickness = tubeThk }
    let op    = { ShellInletTemp  = shellTin;  ShellOutletTemp = shellTout
                  TubeInletTemp   = tubeTin;   TubeOutletTemp  = tubeTout
                  DesignPressure  = pressure }
    buildHX shell tube op
    |> Returns.bind computeArea
    |> Returns.warnIf (fun area -> area > 10.0) (MaterialAdvisory "Large area — verify fouling allowance")
    |> Returns.mapWarnings (fun w ->
        match w with
        | LowEfficiency _ -> MaterialAdvisory "Small area — consider adding tubes"
        | other           -> other)
    |> Returns.log myLogger true "HX pipeline"

printfn "\n--- Valid HX (19 tubes, 4 m shell) ---"
runHX 0.5 4.0 "CS" 19 0.019 0.002 120.0 80.0 25.0 60.0 50.0
|> printfn "result: %A"

printfn "\n--- HX with high pressure warning ---"
runHX 0.5 4.0 "CS" 19 0.019 0.002 120.0 80.0 25.0 60.0 150.0
|> printfn "result: %A"

printfn "\n--- HX with multiple errors (bad shell ID, bad count, temperature crossover, negative pressure) ---"
runHX -0.5 4.0 "CS" 0 0.019 0.002 60.0 80.0 25.0 60.0 -5.0
|> printfn "result: %A"
