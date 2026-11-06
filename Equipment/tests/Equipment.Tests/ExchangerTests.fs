module ExchangerTests

open System
open Expecto
open ROP
open Equipment.Core
open Helpers

let private di = 600.0
let private tubeOD, tubeT = 19.05, 2.11
let private field = TubeLayout.triangular 25.4 (0.5 * di - 30.0) 0.0
let private tsT, shellL, shT = 40.0, 3000.0, 8.0

let private add c eq = Equipment.addComponent c eq |> value
let private link a pa b pb eq = Equipment.connectTags a pa b pb Frame.identity eq |> value |> fst

/// Fixed-tubesheet exchanger: channel1 | TS1 | shell | TS2 | channel2, one straight tube bundle between the sheets.
let fixedTubesheet (bundleLength: float) =
    Equipment.empty "BEM"
    |> add (mk "CH1" (Shape.cylindricalShell di shT 400.0))
    |> add (mk "TS1" (Shape.tubesheet di tsT tubeOD field.Length))
    |> add (mk "SH" (Shape.cylindricalShell di shT shellL))
    |> add (mk "TS2" (Shape.tubesheet di tsT tubeOD field.Length))
    |> add (mk "CH2" (Shape.cylindricalShell di shT 400.0))
    |> add (mk "TB" (Shape.straightTubeBundle tubeOD tubeT bundleLength field))
    |> link "CH1" "End2" "TS1" "Face1"
    |> link "TS1" "Face2" "SH" "End1"
    |> link "SH" "End2" "TS2" "Face1"
    |> link "TS2" "Face2" "CH2" "End1"
    |> link "TB" "End1" "TS1" "TubeField1"
    |> link "TB" "End2" "TS2" "TubeField2"

let private totalBundle = 2.0 * tsT + shellL

/// U-tube exchanger: one tube sheet; both bundle ends mate with the SAME tube field.
let uTube () =
    let f = TubeLayout.triangular 25.4 (0.5 * di - 30.0) 0.0 |> TubeLayout.withoutLane 20.0
    let pairs, _ = TubeLayout.pairIntoU f
    Equipment.empty "BEU"
    |> add (mk "CH" (Shape.cylindricalShell di shT 400.0))
    |> add (mk "TS" (Shape.tubesheet di tsT tubeOD (pairs.Length * 2)))
    |> add (mk "SH" (Shape.cylindricalShell di shT shellL))
    |> add (mk "HD" (Shape.kloepperHead di shT 30.0))
    |> add (mk "UB" (Shape.uTubeBundle tubeOD tubeT (shellL - 150.0) pairs))
    |> link "CH" "End2" "TS" "Face1"
    |> link "TS" "Face2" "SH" "End1"
    |> link "SH" "End2" "HD" "Weld"
    |> link "UB" "EndA" "TS" "TubeField1"
    |> link "UB" "EndB" "TS" "TubeField1"

let tests =
    testList "Heat exchanger scenarios" [
        test "fixed tubesheet: topology has exactly one loop and every loop closes" {
            let eq = fixedTubesheet totalBundle
            Expect.equal (Assembly.cycleRank eq) 1 "one loop"
            let p = Assembly.validate eq
            Expect.isEmpty (warnings p) "loop closes within tolerance"
            let pl = value p
            Expect.equal pl.ClosingEdges.Length 1 "one closing edge"
            nearVec 1e-6 (Vec3.create 0.0 0.0 (400.0 + tsT)) (anchorFrame eq pl "SH").Origin "shell start"
            nearVec 1e-6 (Vec3.create 0.0 0.0 (400.0 + tsT + shellL)) (anchorFrame eq pl "TS2").Origin "TS2"
        }
        test "wrong tube length is detected as a closing mismatch" {
            let eq = fixedTubesheet (totalBundle - 12.5)
            match warnings (Assembly.validate eq) with
            | [ ClosingMismatch (_, _, d, _) ] -> near 1e-6 12.5 d "gap"
            | other -> failtestf "unexpected issues: %A" other
        }
        test "spanning tree exposes the loop-closing edge" {
            let eq = fixedTubesheet totalBundle
            let p = value (Assembly.validate eq)
            let forest = Assembly.tree eq p
            Expect.equal forest.Length 1 "one root"
            let nodes = Assembly.flatten forest.Head |> Seq.toList
            Expect.equal nodes.Length 6 "all components visited once"
            Expect.equal (nodes |> List.sumBy (fun n -> n.ClosingEdges.Length)) 1 "one closing edge"
        }
        test "mass is the sum of the components; centroid on the axis" {
            let eq = fixedTubesheet totalBundle
            let props = value (Properties.ofEquipment eq)
            let sum = eq.Components |> List.sumBy Component.mass
            near 1e-9 sum props.SuppliedMass "total mass"
            let c = props.Centroid.Value
            near 1e-6 0.0 c.X "x on axis"
            near 1e-6 0.0 c.Y "y on axis"
            let total = 400.0 + tsT + shellL + tsT + 400.0
            near 1e-3 (0.5 * total) c.Z "symmetric layout => centroid at mid length"
        }
        test "not-supplied components are excluded from weight, CoG and envelope" {
            let eq = fixedTubesheet totalBundle
            let ch2 = (Equipment.tryComponentByTag "CH2" eq).Value
            let eq2 = Equipment.updateComponent ch2.Id Component.notSupplied eq |> value
            let a, b = value (Properties.ofEquipment eq), value (Properties.ofEquipment eq2)
            near 1e-9 (a.SuppliedMass - Component.mass ch2) b.SuppliedMass "supplied mass"
            near 1e-9 (Component.mass ch2) b.NotSuppliedMass "excluded mass"
            near 1e-6 (400.0 + tsT + shellL + tsT) b.Envelope.Value.Size.Z "envelope length without CH2"
        }
        test "envelope: length, diameter and shipping volume" {
            let eq = fixedTubesheet totalBundle
            let props = value (Properties.ofEquipment eq)
            let size = props.Envelope.Value.Size
            near 1e-6 (400.0 + tsT + shellL + tsT + 400.0) size.Z "length"
            near 1e-6 (di + 2.0 * shT) size.X "width = shell OD"
            near 1e-9 (size.X * size.Y * size.Z) props.EnvelopeVolume "volume"
        }
        test "envelope in a rotated shipping frame (horizontal on a truck)" {
            let eq = fixedTubesheet totalBundle
            let p = value (Assembly.validate eq)
            // shipping frame whose Z axis is world X: the long side of the vessel (world Z) now lies along shipping X or Y
            let shipping = Frame.rotY (pi / 2.0)
            let props = Properties.computeIn shipping eq p
            let s = props.Envelope.Value.Size
            near 1e-6 (di + 2.0 * shT) s.Z "the diameter is now along Z"
            Expect.isTrue (abs (s.X - 3880.0) < 1e-6 || abs (s.Y - 3880.0) < 1e-6) "length along a horizontal axis"
        }
        test "two chambers: tube side (channels + tubes) and shell side" {
            let eq = fixedTubesheet totalBundle
            let props = value (Properties.ofEquipment eq)
            Expect.equal props.Chambers.Length 2 "two chambers"
            let boreVol d l = pi / 4.0 * d * d * l
            let tubeBore = float field.Length * pi / 4.0 * (tubeOD - 2.0 * tubeT) ** 2.0 * totalBundle
            let tubeSide = 2.0 * boreVol di 400.0 + tubeBore
            let shellSide = boreVol di shellL
            let vols = props.Chambers |> List.map (fun c -> c.Volume) |> List.sort
            near 1e-9 (min tubeSide shellSide) vols.[0] "smaller chamber"
            near 1e-9 (max tubeSide shellSide) vols.[1] "larger chamber"
            let tubeChamber = props.Chambers |> List.find (fun c -> c.Cavities |> List.exists (fun (_, n) -> n = "TubeBore"))
            near 1e-9 tubeSide tubeChamber.Volume "tube side volume"
            near 1e-6 (0.5 * (400.0 + tsT + shellL + tsT + 400.0)) tubeChamber.Centroid.Value.Z "tube side centroid"
        }
        test "U-tube: both bundle ends on the same tube field; the loop still closes" {
            let eq = uTube ()
            Expect.equal (Assembly.cycleRank eq) 1 "one loop (the double connection)"
            Expect.isEmpty (warnings (Assembly.validate eq)) "double connection closes"
            let props = value (Properties.ofEquipment eq)
            Expect.equal props.Chambers.Length 2 "tube side / shell side"
            Expect.isTrue (props.SuppliedMass > 0.0) "mass"
            // length: channel + tubesheet + shell + head (flange + dish + thickness)
            let headH = 30.0 + Shape.torisphericalDepth di di (0.1 * di) + shT
            near 1e-6 (400.0 + tsT + shellL + headH) props.Envelope.Value.Size.Z "length"
        }
        test "U-tube: the tube field is a multi-connection port, tube-side ports are not" {
            let eq = uTube ()
            let ts = (Equipment.tryComponentByTag "TS" eq).Value
            let field = Component.tryPort "TubeField1" ts
            Expect.isNone field.Value.MaxConnections "unlimited"
            let r = Equipment.connectTags "UB" "EndA" "TS" "TubeField1" Frame.identity eq
            Expect.isNonEmpty (errors r) "EndA can't be connected twice"
        }
        test "removing a head re-anchors nothing (it is a leaf) and leaves a consistent model" {
            let eq = uTube ()
            let hd = (Equipment.tryComponentByTag "HD" eq).Value
            let r = Equipment.removeComponent hd.Id eq
            Expect.isEmpty (warnings r) "no warnings"
            Expect.equal (Equipment.resolve (value r)).Unplaced [] "all placed"
        }
        test "rotating the U-bundle about its axis breaks the double connection (clocking mismatch)" {
            let eq = uTube ()
            let conns = eq.Connections |> List.filter (fun c -> c.A.Owner = (Equipment.tryComponentByTag "UB" eq).Value.Id)
            let second = conns[1]
            let r = Equipment.setConnectionOffset second.Id (Frame.rotZ (pi / 2.0)) eq
            match warnings r with
            | [ ClosingMismatch (_, _, _, ang) ] -> near 1e-6 90.0 ang "angle"
            | other -> failtestf "unexpected: %A" other
        }
    ]
