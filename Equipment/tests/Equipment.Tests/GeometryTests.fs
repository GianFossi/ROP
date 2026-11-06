module GeometryTests

open System
open Expecto
open Equipment.Core
open Helpers

let tests =
    testList "Geometry" [
        test "frame compose / inverse round-trip" {
            let f = Frame.compose (Frame.translate 10.0 -5.0 3.0) (Frame.compose (Frame.rotZ 0.7) (Frame.rotX -0.3))
            let p = Vec3.create 1.5 -2.0 7.25
            nearVec 1e-9 p (Frame.apply (Frame.inverse f) (Frame.apply f p)) "inverse(apply)"
            let id = Frame.compose f (Frame.inverse f)
            nearVec 1e-9 Vec3.Zero id.Origin "origin"
            nearVec 1e-9 Vec3.UnitX id.X "x"
            nearVec 1e-9 Vec3.UnitZ id.Z "z"
        }
        test "ofZX builds a right-handed orthonormal frame" {
            let f = Frame.ofZX Vec3.Zero (Vec3.create 0.0 0.0 -1.0) Vec3.UnitX
            nearVec 1e-12 (Vec3.create 0.0 -1.0 0.0) f.Y "Y = Z x X"
            near 1e-12 1.0 (Vec3.dot (Vec3.cross f.X f.Y) f.Z) "right-handed"
        }
        test "revolving a rectangle gives the cylinder volume and centroid" {
            let m = Region.revolve (Region.rect 0.0 50.0 10.0 30.0)
            near 1e-12 (pi * 50.0 ** 2.0 * 20.0) m.Volume "volume"
            near 1e-12 20.0 m.CentroidZ "centroid"
        }
        test "revolving a half-disc gives the sphere (exact arcs)" {
            let r = 40.0
            let region = Region [ Seg.arc (P2.create 0.0 0.0) r (-pi / 2.0) pi; Line (P2.create 0.0 r, P2.create 0.0 -r) ]
            let m = Region.revolve region
            near 1e-12 (4.0 / 3.0 * pi * r ** 3.0) m.Volume "sphere volume"
            near 1e-9 0.0 m.CentroidZ "sphere centroid"
        }
        test "revolving a trapezoid gives the frustum" {
            let region = Region.polygon [ P2.create 0.0 0.0; P2.create 30.0 0.0; P2.create 10.0 50.0; P2.create 0.0 50.0 ]
            let m = Region.revolve region
            near 1e-12 (pi * 50.0 / 3.0 * (30.0 ** 2.0 + 30.0 * 10.0 + 10.0 ** 2.0)) m.Volume "frustum volume"
        }
        test "region orientation does not matter" {
            let a = Region.polygon [ P2.create 0.0 0.0; P2.create 5.0 0.0; P2.create 5.0 9.0; P2.create 0.0 9.0 ]
            let b = Region.polygon [ P2.create 0.0 9.0; P2.create 5.0 9.0; P2.create 5.0 0.0; P2.create 0.0 0.0 ]
            near 1e-12 (Region.revolve a).Volume (Region.revolve b).Volume "volume"
            near 1e-12 (Region.revolve a).CentroidZ (Region.revolve b).CentroidZ "centroid"
        }
        test "exact bounding box of a rotated cylinder (ring hull)" {
            let cyl = Part.revolve (Region.rect 0.0 20.0 0.0 100.0)
            // axis Z -> world -Y .. : rotate 90° about X (Z axis becomes -Y)
            let box = (Hull.boundsOf (Frame.rotX (pi / 2.0)) cyl.Hull).Value
            nearVec 1e-9 (Vec3.create -20.0 -100.0 -20.0) box.Min "min"
            nearVec 1e-9 (Vec3.create 20.0 0.0 20.0) box.Max "max"
        }
        test "semicircular bend: Pappus volume and centroid vs numerical integration" {
            let ro, ri, r = 12.0, 9.0, 80.0
            let bend = Part.semicircularBend ro ri r
            // numerical triple integral in (rho, phi, psi)
            let n1, n2, n3 = 60, 60, 240
            let mutable v = 0.0
            let mutable mz = 0.0
            for i in 0 .. n1 - 1 do
                let rho = ri + (ro - ri) * (float i + 0.5) / float n1
                for j in 0 .. n2 - 1 do
                    let phi = 2.0 * pi * (float j + 0.5) / float n2
                    for k in 0 .. n3 - 1 do
                        let psi = pi * (float k + 0.5) / float n3
                        let w = (r + rho * cos phi)
                        let dV = w * rho * ((ro - ri) / float n1) * (2.0 * pi / float n2) * (pi / float n3)
                        v <- v + dV
                        mz <- mz + dV * w * sin psi
            near 1e-4 v bend.Volume "volume"
            near 1e-4 (mz / v) bend.Centroid.Z "centroid"
        }
        test "triangular layout is symmetric and contains the centre tube" {
            let t = TubeLayout.triangular 25.0 300.0 0.0
            Expect.isTrue (t |> List.exists (fun p -> p.X = 0.0 && p.Y = 0.0)) "centre tube"
            Expect.equal (t |> List.filter (fun p -> p.X > 0.0 && p.Y = 0.0) |> List.length)
                         (t |> List.filter (fun p -> p.X < 0.0 && p.Y = 0.0) |> List.length) "row symmetric"
            Expect.isTrue (t |> List.forall (fun p -> sqrt (p.X * p.X + p.Y * p.Y) <= 300.0 + 1e-6)) "inside the OTL"
        }
        test "U pairing: symmetric field with a lane pairs everything" {
            let field = TubeLayout.triangular 25.0 300.0 0.0 |> TubeLayout.withoutLane 30.0
            let pairs, left = TubeLayout.pairIntoU field
            Expect.isEmpty left "no leftovers for a symmetric field"
            Expect.equal (pairs.Length * 2) field.Length "all tubes paired"
        }
    ]
