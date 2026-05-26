namespace Vessel

/// 3D point in millimetres (global Cartesian coordinate system).
type Point3D = { X: float; Y: float; Z: float }

/// 3D free vector (dimensionless when used as direction, mm when used as displacement).
type Vector3D = { X: float; Y: float; Z: float }

module Point3D =

    let origin = { X = 0.0; Y = 0.0; Z = 0.0 }

    let distance (a: Point3D) (b: Point3D) =
        let dx = b.X - a.X
        let dy = b.Y - a.Y
        let dz = b.Z - a.Z
        sqrt (dx * dx + dy * dy + dz * dz)

    let midpoint (a: Point3D) (b: Point3D) =
        { X = (a.X + b.X) / 2.0
          Y = (a.Y + b.Y) / 2.0
          Z = (a.Z + b.Z) / 2.0 }

    let toVector (a: Point3D) (b: Point3D) : Vector3D =
        { X = b.X - a.X; Y = b.Y - a.Y; Z = b.Z - a.Z }


module Vector3D =

    let unitX = { X = 1.0; Y = 0.0; Z = 0.0 }
    let unitY = { X = 0.0; Y = 1.0; Z = 0.0 }
    let unitZ = { X = 0.0; Y = 0.0; Z = 1.0 }

    let magnitude (v: Vector3D) =
        sqrt (v.X * v.X + v.Y * v.Y + v.Z * v.Z)

    let normalize (v: Vector3D) =
        let m = magnitude v
        if m < 1e-12 then v
        else { X = v.X / m; Y = v.Y / m; Z = v.Z / m }

    let dot (a: Vector3D) (b: Vector3D) =
        a.X * b.X + a.Y * b.Y + a.Z * b.Z

    let cross (a: Vector3D) (b: Vector3D) : Vector3D =
        { X = a.Y * b.Z - a.Z * b.Y
          Y = a.Z * b.X - a.X * b.Z
          Z = a.X * b.Y - a.Y * b.X }

    let scale (s: float) (v: Vector3D) : Vector3D =
        { X = s * v.X; Y = s * v.Y; Z = s * v.Z }


/// Component reference frame using beam-element convention:
///   I  = start-node position [mm] in the global frame
///   J  = end-node position   [mm] in the global frame (defines the component axis I→J)
///   K  = section reference vector, perpendicular to I→J, that fixes the angular orientation
///        (e.g. points toward the "top" of a nozzle, or toward the 0° generator of a cylinder)
type ComponentFrame = {
    I : Point3D
    J : Point3D
    K : Vector3D
}

module ComponentFrame =

    /// Unit vector along the component axis (from I to J).
    let axisDirection (frame: ComponentFrame) : Vector3D =
        Point3D.toVector frame.I frame.J |> Vector3D.normalize

    /// Length of the component (distance I→J) [mm].
    let length (frame: ComponentFrame) : float =
        Point3D.distance frame.I frame.J
