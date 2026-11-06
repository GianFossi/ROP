module Helpers

open System
open Expecto
open ROP
open Equipment.Core

let pi = Math.PI

/// Value of a Success (warnings dropped); fails the test with every message on Failure.
let value (r: Returns<'a, Issue>) : 'a =
    match r with
    | Success (v, _) -> v
    | Failure errs -> failtestf "Expected Success, got Failure: %s" (errs |> List.map Issue.describe |> String.concat " | ")

let warnings (r: Returns<'a, Issue>) : Issue list =
    match r with
    | Success (_, w) -> w
    | Failure errs -> failtestf "Expected Success, got Failure: %s" (errs |> List.map Issue.describe |> String.concat " | ")

let errors (r: Returns<'a, Issue>) : Issue list =
    match r with
    | Failure e -> e
    | Success _ -> failtest "Expected Failure, got Success"

/// Relative closeness (absolute when expected = 0).
let near (rel: float) (expected: float) (actual: float) (msg: string) =
    let tol = if expected = 0.0 then rel else abs expected * rel
    Expect.isTrue (abs (expected - actual) <= tol) (sprintf "%s: expected %.10g, got %.10g (tol %.3g)" msg expected actual tol)

let nearVec (tol: float) (e: Vec3) (a: Vec3) (msg: string) =
    Expect.isTrue (Vec3.distance e a <= tol) (sprintf "%s: expected (%.5f, %.5f, %.5f), got (%.5f, %.5f, %.5f)" msg e.X e.Y e.Z a.X a.Y a.Z)

let mk tag shape = value (Component.create tag shape MaterialRef.carbonSteel)

let anchorFrame (eq: Equipment) (placement: Placement) tag =
    let c = (Equipment.tryComponentByTag tag eq).Value
    placement.Frames[c.Id]
