namespace Equipment.Core

open System

// Unit conventions of the whole library (never mixed):
//   lengths  : mm        volumes : mm^3        angles : radians (degrees only in names ending with Deg)
//   densities: kg/m^3    masses  : kg

/// <summary>3D vector / point (mm).</summary>
[<Struct>]
type Vec3 =
    { X: float; Y: float; Z: float }
    static member Zero = { X = 0.0; Y = 0.0; Z = 0.0 }
    static member UnitX = { X = 1.0; Y = 0.0; Z = 0.0 }
    static member UnitY = { X = 0.0; Y = 1.0; Z = 0.0 }
    static member UnitZ = { X = 0.0; Y = 0.0; Z = 1.0 }
    static member (+) (a: Vec3, b: Vec3) = { X = a.X + b.X; Y = a.Y + b.Y; Z = a.Z + b.Z }
    static member (-) (a: Vec3, b: Vec3) = { X = a.X - b.X; Y = a.Y - b.Y; Z = a.Z - b.Z }
    static member (*) (k: float, a: Vec3) = { X = k * a.X; Y = k * a.Y; Z = k * a.Z }
    static member (~-) (a: Vec3) = { X = -a.X; Y = -a.Y; Z = -a.Z }
    member v.Length = sqrt (v.X * v.X + v.Y * v.Y + v.Z * v.Z)

/// <summary>Operations on <see cref="Vec3"/>.</summary>
module Vec3 =
    let create x y z : Vec3 = { X = x; Y = y; Z = z }
    let dot (a: Vec3) (b: Vec3) = a.X * b.X + a.Y * b.Y + a.Z * b.Z
    let cross (a: Vec3) (b: Vec3) : Vec3 =
        { X = a.Y * b.Z - a.Z * b.Y
          Y = a.Z * b.X - a.X * b.Z
          Z = a.X * b.Y - a.Y * b.X }
    let length (a: Vec3) = a.Length
    let distance (a: Vec3) (b: Vec3) = (a - b).Length
    /// Unit vector; a (near) zero vector is returned unchanged.
    let normalize (a: Vec3) =
        let l = a.Length
        if l < 1e-300 then a else (1.0 / l) * a

/// <summary>
/// Right-handed orthonormal coordinate frame: <c>Origin</c> plus the world coordinates of its local X, Y, Z axes.
/// A frame is also a rigid transform local → world: <c>p_world = Origin + p.X*X + p.Y*Y + p.Z*Z</c>.
/// </summary>
[<Struct>]
type Frame =
    { Origin: Vec3; X: Vec3; Y: Vec3; Z: Vec3 }

module Frame =
    let identity : Frame = { Origin = Vec3.Zero; X = Vec3.UnitX; Y = Vec3.UnitY; Z = Vec3.UnitZ }

    /// Local point → world point.
    let apply (f: Frame) (p: Vec3) : Vec3 = f.Origin + (p.X * f.X) + (p.Y * f.Y) + (p.Z * f.Z)

    /// Local direction → world direction (no translation).
    let applyDir (f: Frame) (d: Vec3) : Vec3 = (d.X * f.X) + (d.Y * f.Y) + (d.Z * f.Z)

    /// <c>compose a b</c>: apply <c>b</c> first, then <c>a</c> (i.e. <c>b</c> expressed in <c>a</c>'s parent).
    let compose (a: Frame) (b: Frame) : Frame =
        { Origin = apply a b.Origin
          X = applyDir a b.X
          Y = applyDir a b.Y
          Z = applyDir a b.Z }

    let inverse (f: Frame) : Frame =
        let ix = Vec3.create f.X.X f.Y.X f.Z.X
        let iy = Vec3.create f.X.Y f.Y.Y f.Z.Y
        let iz = Vec3.create f.X.Z f.Y.Z f.Z.Z
        let r = { Origin = Vec3.Zero; X = ix; Y = iy; Z = iz }
        { r with Origin = -(applyDir r f.Origin) }

    let translation (v: Vec3) : Frame = { identity with Origin = v }
    let translate x y z = translation (Vec3.create x y z)

    let rotX (a: float) : Frame =
        let c, s = cos a, sin a
        { identity with Y = Vec3.create 0.0 c s; Z = Vec3.create 0.0 -s c }
    let rotY (a: float) : Frame =
        let c, s = cos a, sin a
        { identity with X = Vec3.create c 0.0 -s; Z = Vec3.create s 0.0 c }
    let rotZ (a: float) : Frame =
        let c, s = cos a, sin a
        { identity with X = Vec3.create c s 0.0; Y = Vec3.create -s c 0.0 }

    /// Frame with the given origin, Z axis along <paramref name="z"/>, and X axis as close as possible to <paramref name="xHint"/>.
    let ofZX (origin: Vec3) (z: Vec3) (xHint: Vec3) : Frame =
        let z = Vec3.normalize z
        let x0 = xHint - (Vec3.dot xHint z) * z
        // xHint (anti)parallel to z: pick any perpendicular axis.
        let x0 =
            if x0.Length < 1e-9 then
                let a = if abs z.X < 0.9 then Vec3.UnitX else Vec3.UnitY
                a - (Vec3.dot a z) * z
            else x0
        let x = Vec3.normalize x0
        { Origin = origin; X = x; Y = Vec3.cross z x; Z = z }

    /// The 180° rotation about the local X axis used by mating: it reverses Z and Y and keeps X.
    let flip : Frame = rotX Math.PI

    /// True when two frames coincide within <paramref name="distTol"/> (mm) and <paramref name="axisTol"/> (axis vector error).
    let coincide (distTol: float) (axisTol: float) (a: Frame) (b: Frame) =
        Vec3.distance a.Origin b.Origin <= distTol
        && (a.X - b.X).Length <= axisTol
        && (a.Z - b.Z).Length <= axisTol

    /// (translation distance mm, largest axis error expressed in degrees) between two frames.
    let deviation (a: Frame) (b: Frame) =
        let d = Vec3.distance a.Origin b.Origin
        let ang (u: Vec3) (v: Vec3) = Math.Acos(Math.Clamp(Vec3.dot u v, -1.0, 1.0)) * 180.0 / Math.PI
        d, max (ang a.X b.X) (ang a.Z b.Z)

/// <summary>Axis-aligned bounding box (world axes unless stated otherwise).</summary>
type Aabb =
    { Min: Vec3; Max: Vec3 }
    member b.Size = b.Max - b.Min
    member b.Center = 0.5 * (b.Min + b.Max)
    member b.Volume = let s = b.Size in s.X * s.Y * s.Z

module Aabb =
    let ofPoint (p: Vec3) : Aabb = { Min = p; Max = p }
    let union (a: Aabb) (b: Aabb) : Aabb =
        { Min = Vec3.create (min a.Min.X b.Min.X) (min a.Min.Y b.Min.Y) (min a.Min.Z b.Min.Z)
          Max = Vec3.create (max a.Max.X b.Max.X) (max a.Max.Y b.Max.Y) (max a.Max.Z b.Max.Z) }
    let ofSeq (boxes: Aabb seq) : Aabb option =
        let mutable acc = None
        for b in boxes do
            acc <- match acc with None -> Some b | Some a -> Some (union a b)
        acc

/// <summary>
/// Extreme-point primitives that describe the outer extent of a solid. Keeping circles (not just corner points)
/// makes the bounding box of a *rotated* revolved solid exact instead of the conservative box-of-a-box.
/// </summary>
type Hull =
    | Pt of Vec3
    /// Circle (ring) with centre, unit normal and radius.
    | Ring of centre: Vec3 * normal: Vec3 * radius: float

module Hull =
    /// Exact world bounds of the primitive after applying the frame <paramref name="f"/> (local → world).
    let bounds (f: Frame) (h: Hull) : Aabb =
        match h with
        | Pt p -> Aabb.ofPoint (Frame.apply f p)
        | Ring (c, n, r) ->
            let c' = Frame.apply f c
            let n' = Vec3.normalize (Frame.applyDir f n)
            let ext k = r * sqrt (max 0.0 (1.0 - k * k))
            let ex, ey, ez = ext n'.X, ext n'.Y, ext n'.Z
            { Min = Vec3.create (c'.X - ex) (c'.Y - ey) (c'.Z - ez)
              Max = Vec3.create (c'.X + ex) (c'.Y + ey) (c'.Z + ez) }

    let transform (f: Frame) (h: Hull) : Hull =
        match h with
        | Pt p -> Pt (Frame.apply f p)
        | Ring (c, n, r) -> Ring (Frame.apply f c, Vec3.normalize (Frame.applyDir f n), r)

    let boundsOf (f: Frame) (hulls: Hull seq) : Aabb option =
        hulls |> Seq.map (bounds f) |> Aabb.ofSeq
