module ShapeTests

open System
open Expecto
open ROP
open Equipment.Core
open Helpers

let private cavity name (b: Body) = (b.Cavities |> List.find (fun c -> c.CavityName = name)).Space
let private solidVolume (b: Body) = Component.materialVolume b

let tests =
    testList "Shapes" [
        test "cylindrical shell: volumes, mass, ports" {
            let s = Shape.cylindricalShell 1000.0 10.0 2000.0
            let b = Shape.body s
            near 1e-12 (pi / 4.0 * (1020.0 ** 2.0 - 1000.0 ** 2.0) * 2000.0) (solidVolume b) "material"
            near 1e-12 (pi / 4.0 * 1000.0 ** 2.0 * 2000.0) (cavity "Bore" b).Volume "bore"
            near 1e-9 (7850.0 * solidVolume b * 1e-9) (Component.mass (mk "S" s)) "mass"
            Expect.equal (b.Ports |> List.map (fun p -> p.PortName)) [ "End1"; "End2" ] "ports"
            nearVec 1e-12 (Vec3.create 0.0 0.0 1000.0) (cavity "Bore" b).Centroid "bore centroid"
        }
        test "shell opening: port on the outer surface, hole deducted" {
            let s = Shape.cylindricalShell 1000.0 10.0 2000.0 |> Shape.withOpening { OpeningName = "N1"; AxialPosition = 500.0; AngleDeg = 90.0; Diameter = 200.0 }
            let b = Shape.body s
            let p = b.Ports |> List.find (fun p -> p.PortName = "N1")
            nearVec 1e-9 (Vec3.create 0.0 510.0 500.0) p.Frame.Origin "port origin"
            nearVec 1e-9 Vec3.UnitY p.Frame.Z "port normal is radial"
            near 1e-9 (pi / 4.0 * 200.0 ** 2.0 * 10.0) (b.Voids |> List.sumBy (fun v -> v.Volume)) "void volume"
        }
        test "ellipsoidal 2:1 head matches the ellipsoid formulas" {
            let d, t = 1200.0, 8.0
            let a, b' = d / 2.0, d / 4.0
            let b = Shape.body (Shape.ellipsoidalHead d t 2.0 0.0)
            near 1e-10 (2.0 / 3.0 * pi * a * a * b') (cavity "Bore" b).Volume "cavity"
            near 1e-10 (2.0 / 3.0 * pi * ((a + t) ** 2.0 * (b' + t) - a * a * b')) (solidVolume b) "material"
            near 1e-10 (3.0 / 8.0 * b') ((cavity "Bore" b).Centroid.Z) "cavity centroid (3/8 b)"
        }
        test "ellipsoidal head with straight flange adds a cylinder" {
            let d, t, sf = 1200.0, 8.0, 40.0
            let b0 = Shape.body (Shape.ellipsoidalHead d t 2.0 0.0)
            let b1 = Shape.body (Shape.ellipsoidalHead d t 2.0 sf)
            near 1e-10 ((cavity "Bore" b0).Volume + pi * (d / 2.0) ** 2.0 * sf) (cavity "Bore" b1).Volume "cavity"
        }
        test "hemispherical head" {
            let d, t = 800.0, 6.0
            let a = d / 2.0
            let b = Shape.body (Shape.hemisphericalHead d t 0.0)
            near 1e-10 (2.0 / 3.0 * pi * a ** 3.0) (cavity "Bore" b).Volume "cavity"
            near 1e-10 (2.0 / 3.0 * pi * ((a + t) ** 3.0 - a ** 3.0)) (solidVolume b) "material"
        }
        test "torispherical head: cavity volume vs independent numerical integration" {
            let d, rc, rk, sf, t = 2000.0, 2000.0, 200.0, 0.0, 12.0
            let a = d / 2.0
            let zc = -sqrt ((rc - rk) ** 2.0 - (a - rk) ** 2.0)
            let depth = rc + zc
            let zTangent = (rc - rk) * 0.0 + rk * sin (atan2 (-zc) (a - rk)) // knuckle/crown junction height
            let r z = if z <= zTangent then (a - rk) + sqrt (max 0.0 (rk * rk - z * z)) else sqrt (max 0.0 (rc * rc - (z - zc) ** 2.0))
            let n = 400000
            let v = Seq.sum (seq { for i in 0 .. n - 1 -> let z = depth * (float i + 0.5) / float n in pi * r z ** 2.0 * depth / float n })
            let b = Shape.body (Shape.torisphericalHead d t rc rk sf)
            near 2e-4 v (cavity "Bore" b).Volume "cavity volume"
            near 1e-9 depth (Shape.torisphericalDepth d rc rk) "depth helper"
            // Hand calculation for D = 1 (Klöpper: Rc = D, rk = 0.1 D): depth 0.19377 D,
            // knuckle zone 0.0658 D^3 + crown cap 0.0330 D^3 = 0.0988 D^3 (the usual "0.1 D^3" is a rounded handbook figure).
            let klo = Shape.body (Shape.kloepperHead 1000.0 10.0 0.0)
            near 2e-3 (0.0989 * 1000.0 ** 3.0) (cavity "Bore" klo).Volume "Kloepper ~ 0.0989 D^3"
        }
        test "torispherical head apex port is at the outer apex" {
            let s = Shape.kloepperHead 1000.0 10.0 30.0
            let apex = Shape.ports s |> List.find (fun p -> p.PortName = "Apex")
            let hOut = 30.0 + Shape.torisphericalDepth 1000.0 1000.0 100.0 + 10.0
            near 1e-9 hOut apex.Frame.Origin.Z "apex height"
            let box = (Hull.boundsOf Frame.identity (Shape.body s).Solids.Head.Hull).Value
            near 1e-6 hOut box.Max.Z "hull top = outer apex"
        }
        test "conical shell: frustum volumes" {
            let b = Shape.body (Shape.conicalShell 400.0 800.0 10.0 600.0)
            let frustum r1 r2 h = pi * h / 3.0 * (r1 * r1 + r1 * r2 + r2 * r2)
            near 1e-10 (frustum 200.0 400.0 600.0) (cavity "Bore" b).Volume "bore"
            let len = sqrt (600.0 ** 2.0 + 200.0 ** 2.0)
            let dr = 10.0 * len / 600.0
            near 1e-10 (frustum (200.0 + dr) (400.0 + dr) 600.0 - frustum 200.0 400.0 600.0) (solidVolume b) "wall"
        }
        test "tube sheet: holes reduce the material, interface cavities have no volume" {
            let b = Shape.body (Shape.tubesheet 1000.0 60.0 20.0 300)
            near 1e-10 (pi / 4.0 * 1000.0 ** 2.0 * 60.0 - 300.0 * pi / 4.0 * 20.0 ** 2.0 * 60.0) (solidVolume b) "material"
            Expect.equal (b.Cavities |> List.sumBy (fun c -> c.Space.Volume)) 0.0 "no volume"
            Expect.equal (b.Ports |> List.length) 4 "4 ports"
        }
        test "straight bundle: volume = N x annulus x L; envelope includes the outer tubes" {
            let pos = TubeLayout.triangular 25.0 100.0 0.0
            let n = float pos.Length
            let b = Shape.body (Shape.straightTubeBundle 19.05 2.11 3000.0 pos)
            let ri = 19.05 / 2.0 - 2.11
            near 1e-10 (n * pi * ((19.05 / 2.0) ** 2.0 - ri ** 2.0) * 3000.0) (solidVolume b) "material"
            near 1e-10 (n * pi * ri ** 2.0 * 3000.0) (cavity "TubeBore" b).Volume "tube bore"
            let box = (Hull.boundsOf Frame.identity b.Solids.Head.Hull).Value
            near 1e-9 3000.0 box.Max.Z "length"
            Expect.isTrue (box.Max.X > 100.0) "outer tubes + radius"
        }
        test "U bundle: legs + bend lengths" {
            let tubes = [ { Y = 0.0; XA = -50.0; XB = 50.0 }; { Y = 30.0; XA = -80.0; XB = 80.0 } ]
            let b = Shape.body (Shape.uTubeBundle 20.0 2.0 1000.0 tubes)
            let a = pi * (10.0 ** 2.0 - 8.0 ** 2.0)
            let centreline = (2.0 * 1000.0 + pi * 50.0) + (2.0 * 1000.0 + pi * 80.0)
            near 1e-10 (a * centreline) (solidVolume b) "material = A x total centreline length"
            Expect.equal (b.Ports |> List.map (fun p -> p.PortName)) [ "EndA"; "EndB" ] "ports"
        }
        test "weld-neck flange" {
            let b = Shape.body (Shape.weldNeckFlange 600.0 300.0 40.0 360.0 80.0)
            let ring = pi * (300.0 ** 2.0 - 150.0 ** 2.0) * 40.0
            let hub = pi * (180.0 ** 2.0 - 150.0 ** 2.0) * 80.0
            near 1e-10 (ring + hub) (solidVolume b) "material"
            near 1e-10 (pi * 150.0 ** 2.0 * 120.0) (cavity "Bore" b).Volume "bore"
        }
        test "validation reports every invalid parameter" {
            let errs = errors (Component.create "X" (Shape.cylindricalShell -1.0 0.0 100.0) MaterialRef.carbonSteel)
            Expect.equal errs.Length 2 "two errors"
            Expect.isTrue (errors (Component.create "H" (Shape.torisphericalHead 1000.0 10.0 1000.0 600.0 0.0) MaterialRef.carbonSteel) |> List.isEmpty |> not) "knuckle > D/2"
            Expect.isTrue (errors (Component.create "" (Shape.block 1.0 1.0 1.0) MaterialRef.carbonSteel) |> List.isEmpty |> not) "empty tag"
        }
    ]
