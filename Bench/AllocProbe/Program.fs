// Allocation probe backing docs/ZERO-ALLOC.md. Not part of ROP.sln; run with:
//   dotnet run -c Release --project Bench/AllocProbe
// Reports bytes allocated per call (GC.GetAllocatedBytesForCurrentThread over 5M calls, after warm-up).

open System
open ROP
open ROP.Returns.Operators

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

let N = 5_000_000
let inline measure name ([<InlineIfLambda>] f: int -> float) =
    for i in 0 .. 200_000 do f i |> ignore   // warm-up / tiering
    GC.Collect()
    let before = GC.GetAllocatedBytesForCurrentThread()
    let mutable acc = 0.0
    for i in 0 .. N - 1 do acc <- acc + f i
    let after = GC.GetAllocatedBytesForCurrentThread()
    printfn "%-58s %8.1f B/call  (checksum %g)" name (float (after - before) / float N) acc

let step1 (x: float) : Returns<float,Msg> = Returns.ok (x + 1.0)
let step1w (x: float) : Returns<float,Msg> = if x > 0.0 then Returns.warn (Extrapolated ("G", "hi")) x else Returns.ok x
let vstep (x: float) : VReturns<float,Msg> = V.ok (x + 1.0)
let vstepw (x: float) : VReturns<float,Msg> = VSuccess (x, [Extrapolated ("G", "hi")])

[<EntryPoint>]
let main _ =
    measure "baseline: plain float arithmetic" (fun i -> float i * 2.0)
    measure "Returns.ok x" (fun i -> match Returns.ok (float i) with Success (v,_) -> v | _ -> 0.0)
    measure "Returns: ok >>= s >>= s >>= s (clean)" (fun i -> match Returns.ok (float i) >>= step1 >>= step1 >>= step1 with Success (v,_) -> v | _ -> 0.0)
    measure "Returns: returns { let! x3 } (clean)" (fun i ->
        let r = returns { let! a = step1 (float i) in let! b = step1 a in let! c = step1 b in return c }
        match r with Success (v,_) -> v | _ -> 0.0)
    measure "Returns: 3 steps, each adds 1 warning" (fun i -> match Returns.ok (float i + 1.0) >>= step1w >>= step1w >>= step1w with Success (v,_) -> v | _ -> 0.0)
    measure "struct VReturns: ok |> bind s |> bind s |> bind s (clean)" (fun i -> match V.ok (float i) |> V.bind vstep |> V.bind vstep |> V.bind vstep with VSuccess (v,_) -> v | _ -> 0.0)
    measure "struct VReturns: 1 warning" (fun i -> match vstepw (float i) with VSuccess (v,_) -> v | _ -> 0.0)
    let re = 2000.0
    measure "warnIf (false) with interpolated message" (fun i ->
        match Returns.ok (float i) |> Returns.warnIf (fun _ -> re < 1000.0) (Extrapolated ("Gnielinski", $"Re = {re} below 3000")) with Success (v,_) -> v | _ -> 0.0)
    measure "warnIfLazy (false) with interpolated message" (fun i ->
        match Returns.ok (float i) |> Returns.warnIfLazy (fun _ -> re < 1000.0) (fun () -> Extrapolated ("Gnielinski", $"Re = {re} below 3000")) with Success (v,_) -> v | _ -> 0.0)
    0
