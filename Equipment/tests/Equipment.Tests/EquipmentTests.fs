module EquipmentTests

open System
open Expecto
open ROP
open Equipment.Core
open Helpers

/// Equipment with shells S1 (1000 long) -> S2 (500 long) chained along Z.
let private chain () =
    let s1 = mk "S1" (Shape.cylindricalShell 1000.0 10.0 1000.0)
    let s2 = mk "S2" (Shape.cylindricalShell 1000.0 10.0 500.0)
    let eq = Equipment.empty "chain" |> Equipment.addComponent s1 |> value
    let eq = Equipment.addComponent s2 eq |> value
    let eq, cid = Equipment.connectTags "S1" "End2" "S2" "End1" Frame.identity eq |> value
    eq, cid

let tests =
    testList "Equipment graph" [
        test "first component is anchored at the origin" {
            let eq = Equipment.empty "e" |> Equipment.addComponent (mk "S1" (Shape.block 1.0 1.0 1.0)) |> value
            Expect.equal eq.Anchors.Count 1 "one anchor"
            Expect.isEmpty (Equipment.resolve eq).Unplaced "everything placed"
        }
        test "duplicate tags are rejected" {
            let eq = Equipment.empty "e" |> Equipment.addComponent (mk "S1" (Shape.block 1.0 1.0 1.0)) |> value
            let errs = errors (Equipment.addComponent (mk "S1" (Shape.block 2.0 2.0 2.0)) eq)
            Expect.equal errs [ DuplicateTag "S1" ] "duplicate tag"
        }
        test "an unconnected component is reported and stays unplaced" {
            let eq = Equipment.empty "e" |> Equipment.addComponent (mk "A" (Shape.block 1.0 1.0 1.0)) |> value
            let r = Equipment.addComponent (mk "B" (Shape.block 1.0 1.0 1.0)) eq
            Expect.equal (warnings r) [ Unconnected "B" ] "warning"
            Expect.equal (Equipment.resolve (value r)).Unplaced.Length 1 "unplaced"
            Expect.equal (errors (Assembly.validate (value r))) [ NotPlaced "B" ] "validate fails"
        }
        test "mating places the next shell at the end of the first" {
            let eq, _ = chain ()
            let p = Equipment.resolve eq
            nearVec 1e-9 (Vec3.create 0.0 0.0 1000.0) (anchorFrame eq p "S2").Origin "S2 origin"
            nearVec 1e-9 Vec3.UnitZ (anchorFrame eq p "S2").Z "S2 axis"
        }
        test "mating the same end twice flips the second shell (head on the left)" {
            let s1 = mk "S1" (Shape.cylindricalShell 1000.0 10.0 1000.0)
            let h = mk "H1" (Shape.hemisphericalHead 1000.0 10.0 0.0)
            let eq = Equipment.empty "e" |> Equipment.addComponent s1 |> value |> Equipment.addComponent h |> value
            let eq, _ = Equipment.connectTags "S1" "End1" "H1" "Weld" Frame.identity eq |> value
            let p = Equipment.resolve eq
            nearVec 1e-9 Vec3.Zero (anchorFrame eq p "H1").Origin "weld at shell start"
            nearVec 1e-9 (Vec3.create 0.0 0.0 -1.0) (anchorFrame eq p "H1").Z "head dish points to -Z"
        }
        test "connection errors" {
            let eq, _ = chain ()
            Expect.equal (errors (Equipment.connectTags "S1" "Nope" "S2" "End2" Frame.identity eq)) [ PortNotFound ("S1", "Nope") ] "unknown port"
            Expect.equal (errors (Equipment.connectTags "S1" "End2" "S2" "End2" Frame.identity eq)) [ PortCapacityExceeded ("S1", "End2") ] "capacity"
            Expect.equal (errors (Equipment.connectTags "S1" "End1" "S1" "End2" Frame.identity eq)) [ SelfConnection ("S1", "End1") ] "self"
            Expect.equal (errors (Equipment.connectTags "S9" "End1" "S2" "End2" Frame.identity eq)) [ ComponentTagNotFound "S9" ] "unknown component"
        }
        test "connection offset: axial gap and clocking rotate/move the branch" {
            let s1 = mk "S1" (Shape.cylindricalShell 1000.0 10.0 1000.0)
            let s2 = mk "S2" (Shape.cylindricalShell 1000.0 10.0 500.0)
            let eq = Equipment.empty "e" |> Equipment.addComponent s1 |> value |> Equipment.addComponent s2 |> value
            // 90° clocking about the common axis and a 5 mm gap (offset is expressed in the frame of port A, whose Z is the outward normal)
            let offset = Frame.compose (Frame.translate 0.0 0.0 5.0) (Frame.rotZ (pi / 2.0))
            let eq, cid = Equipment.connectTags "S1" "End2" "S2" "End1" offset eq |> value
            let f = anchorFrame eq (Equipment.resolve eq) "S2"
            nearVec 1e-9 (Vec3.create 0.0 0.0 1005.0) f.Origin "gap"
            // set back to zero
            let eq = Equipment.setConnectionOffset cid Frame.identity eq |> value
            nearVec 1e-9 (Vec3.create 0.0 0.0 1000.0) (anchorFrame eq (Equipment.resolve eq) "S2").Origin "back"
        }
        test "editing a dimension moves everything hanging from it" {
            let eq, _ = chain ()
            let s1 = (Equipment.tryComponentByTag "S1" eq).Value
            let eq = Equipment.replaceShape s1.Id (Shape.cylindricalShell 1000.0 10.0 1500.0) eq |> value
            nearVec 1e-9 (Vec3.create 0.0 0.0 1500.0) (anchorFrame eq (Equipment.resolve eq) "S2").Origin "S2 shifted"
        }
        test "editing cannot orphan a connection to a port that no longer exists" {
            let s1 = mk "S1" (Shape.cylindricalShell 1000.0 10.0 1000.0 |> Shape.withOpening { OpeningName = "N1"; AxialPosition = 500.0; AngleDeg = 0.0; Diameter = 100.0 })
            let n1 = mk "N1" (Shape.pipe 114.3 6.0 200.0)
            let eq = Equipment.empty "e" |> Equipment.addComponent s1 |> value |> Equipment.addComponent n1 |> value
            let eq, _ = Equipment.connectTags "S1" "N1" "N1" "End1" Frame.identity eq |> value
            // nozzle axis must be radial
            let p = Equipment.resolve eq
            let f = anchorFrame eq p "N1"
            nearVec 1e-9 (Vec3.create 510.0 0.0 500.0) f.Origin "nozzle base on the shell surface"
            nearVec 1e-9 Vec3.UnitX f.Z "nozzle axis is radial"
            let s1c = (Equipment.tryComponentByTag "S1" eq).Value
            let errs = errors (Equipment.replaceShape s1c.Id (Shape.cylindricalShell 1000.0 10.0 1000.0) eq)
            Expect.equal errs [ PortNotFound ("S1", "N1") ] "port removed by the edit"
        }
        test "removing the anchor keeps the detached branch where it was" {
            let eq, _ = chain ()
            let s1 = (Equipment.tryComponentByTag "S1" eq).Value
            let r = Equipment.removeComponent s1.Id eq
            Expect.equal (warnings r) [ Reanchored "S2" ] "S2 becomes anchor"
            let eq' = value r
            nearVec 1e-9 (Vec3.create 0.0 0.0 1000.0) (anchorFrame eq' (Equipment.resolve eq') "S2").Origin "S2 did not move"
        }
        test "disconnecting splits the model into two anchored branches" {
            let eq, cid = chain ()
            let r = Equipment.disconnect cid eq
            Expect.equal (warnings r) [ Reanchored "S2" ] "reanchored"
            Expect.equal (value r).Anchors.Count 2 "two anchors"
            Expect.isEmpty (Equipment.resolve (value r)).Unplaced "all placed"
        }
        test "path, neighbours and cycle rank on a chain" {
            let eq, _ = chain ()
            let a = (Equipment.tryComponentByTag "S1" eq).Value.Id
            let b = (Equipment.tryComponentByTag "S2" eq).Value.Id
            Expect.equal (Assembly.path a b eq) (Some [ a; b ]) "path"
            Expect.equal (Equipment.neighbours a eq) [ b ] "neighbours"
            Expect.equal (Assembly.cycleRank eq) 0 "tree"
        }
        test "release anchor fails when it would leave the model unplaced" {
            let eq, _ = chain ()
            let s1 = (Equipment.tryComponentByTag "S1" eq).Value
            Expect.equal (errors (Equipment.releaseAnchor s1.Id eq)) [ NotPlaced "S1" ] "S1 would be unplaced"
        }
        test "moving the anchor moves the whole equipment" {
            let eq, _ = chain ()
            let s1 = (Equipment.tryComponentByTag "S1" eq).Value
            let eq = Equipment.setAnchor s1.Id (Frame.translate 100.0 0.0 0.0) eq |> value
            nearVec 1e-9 (Vec3.create 100.0 0.0 1000.0) (anchorFrame eq (Equipment.resolve eq) "S2").Origin "S2 moved"
        }
    ]
