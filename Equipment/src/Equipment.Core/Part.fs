namespace Equipment.Core

open System

/// <summary>
/// A homogeneous piece of volume expressed in some local frame: its volume (mm³), the centroid (mm) and the
/// extreme-point <see cref="Hull"/> that gives its exact bounding box. Solids, voids and fluid cavities are all Parts.
/// </summary>
type Part =
    { Volume: float
      Centroid: Vec3
      Hull: Hull list }

module Part =
    let empty : Part = { Volume = 0.0; Centroid = Vec3.Zero; Hull = [] }

    /// Part obtained by revolving a closed (R, Z) region 360° about the local Z axis.
    let revolve (region: Region) : Part =
        let m = Region.revolve region
        let hull =
            Region.samplePoints 24 region
            |> List.map (fun p ->
                if p.R < 1e-9 then Pt (Vec3.create 0.0 0.0 p.Z)
                else Ring (Vec3.create 0.0 0.0 p.Z, Vec3.UnitZ, p.R))
        { Volume = m.Volume; Centroid = Vec3.create 0.0 0.0 m.CentroidZ; Hull = hull }

    /// Box with sides lx × ly × lz whose bottom-face centre is the local origin.
    let box (lx: float) (ly: float) (lz: float) : Part =
        let corners =
            [ for sx in [ -0.5; 0.5 ] do
                for sy in [ -0.5; 0.5 ] do
                    for sz in [ 0.0; 1.0 ] -> Pt (Vec3.create (sx * lx) (sy * ly) (sz * lz)) ]
        { Volume = lx * ly * lz; Centroid = Vec3.create 0.0 0.0 (0.5 * lz); Hull = corners }

    /// Part rigidly moved by the frame (local → parent).
    let transform (f: Frame) (p: Part) : Part =
        { p with Centroid = Frame.apply f p.Centroid; Hull = p.Hull |> List.map (Hull.transform f) }

    let translate (v: Vec3) (p: Part) = transform (Frame.translation v) p

    /// Union of disjoint parts (volumes add, centroid is volume-weighted).
    let combine (parts: Part seq) : Part =
        let mutable v = 0.0
        let mutable m = Vec3.Zero
        let hulls = ResizeArray<Hull>()
        for p in parts do
            v <- v + p.Volume
            m <- m + (p.Volume * p.Centroid)
            hulls.AddRange p.Hull
        { Volume = v
          Centroid = (if abs v > 1e-300 then (1.0 / v) * m else Vec3.Zero)
          Hull = List.ofSeq hulls }

    /// Tube (annulus) of constant section swept along a semicircular bend of centreline radius <paramref name="bendRadius"/>,
    /// lying in the local XZ plane, centred on (0, 0, 0) and bulging towards +Z. Exact Pappus volume and centroid.
    let semicircularBend (outerRadius: float) (innerRadius: float) (bendRadius: float) : Part =
        let area = Math.PI * (outerRadius ** 2.0 - innerRadius ** 2.0)
        let ix = Math.PI / 4.0 * (outerRadius ** 4.0 - innerRadius ** 4.0) // second moment about a diameter
        let volume = area * Math.PI * bendRadius
        let zc = 2.0 / Math.PI * (bendRadius + ix / (bendRadius * area))
        let hull =
            [ for k in 0 .. 12 ->
                let psi = Math.PI * float k / 12.0
                Ring (Vec3.create (bendRadius * cos psi) 0.0 (bendRadius * sin psi),
                      Vec3.create (-(sin psi)) 0.0 (cos psi),
                      outerRadius) ]
        { Volume = volume; Centroid = Vec3.create 0.0 0.0 zc; Hull = hull }

    /// Mass in kg of a volume in mm³ with a density in kg/m³.
    let massKg (densityKgM3: float) (volumeMm3: float) = densityKgM3 * volumeMm3 * 1e-9
