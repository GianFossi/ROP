namespace Equipment.Core

open System

/// <summary>Point of a half-plane profile: R = distance from the revolution axis (>= 0), Z = position along the axis (mm).</summary>
[<Struct>]
type P2 =
    { R: float; Z: float }

module P2 =
    let create r z : P2 = { R = r; Z = z }

/// <summary>
/// Profile boundary segment in the (R, Z) half-plane. Circular arcs are ellipses with equal semi-axes, so heads
/// (ellipsoidal, hemispherical, torispherical) need no extra segment kind.
/// </summary>
type Seg =
    | Line of P2 * P2
    /// Ellipse arc: centre, semi-axis along R, semi-axis along Z, start angle, signed sweep (rad).
    | Elliptic of centre: P2 * a: float * b: float * startAngle: float * sweep: float

module Seg =
    let arc (centre: P2) (radius: float) (startAngle: float) (sweep: float) =
        Elliptic (centre, radius, radius, startAngle, sweep)

    /// Point at parameter t in [0,1].
    let eval (s: Seg) (t: float) : P2 =
        match s with
        | Line (p, q) -> { R = p.R + t * (q.R - p.R); Z = p.Z + t * (q.Z - p.Z) }
        | Elliptic (c, a, b, a0, sw) ->
            let th = a0 + sw * t
            { R = c.R + a * cos th; Z = c.Z + b * sin th }

    /// Derivative d(point)/dt.
    let deriv (s: Seg) (t: float) : P2 =
        match s with
        | Line (p, q) -> { R = q.R - p.R; Z = q.Z - p.Z }
        | Elliptic (_, a, b, a0, sw) ->
            let th = a0 + sw * t
            { R = -a * sin th * sw; Z = b * cos th * sw }

    let startPoint s = eval s 0.0
    let endPoint s = eval s 1.0

/// <summary>Gauss–Legendre quadrature on [0,1] (nodes computed once by Newton iteration on Legendre polynomials).</summary>
module internal Gauss =
    let private n = 24
    let nodes, weights =
        let xs = Array.zeroCreate<float> n
        let ws = Array.zeroCreate<float> n
        for i in 0 .. n - 1 do
            let mutable x = cos (Math.PI * (float i + 0.75) / (float n + 0.5))
            let mutable dp = 1.0
            for _ in 1 .. 100 do
                let mutable p0 = 1.0
                let mutable p1 = x
                for k in 2 .. n do
                    let p2 = ((2.0 * float k - 1.0) * x * p1 - (float k - 1.0) * p0) / float k
                    p0 <- p1
                    p1 <- p2
                dp <- float n * (x * p1 - p0) / (x * x - 1.0)
                x <- x - p1 / dp
            xs[i] <- (1.0 - x) / 2.0 // map [-1,1] -> [0,1]
            ws[i] <- 1.0 / ((1.0 - x * x) * dp * dp) // (2/((1-x^2)dp^2)) / 2
        xs, ws

/// <summary>Closed region of the (R, Z) half-plane, bounded by a contiguous chain of segments.</summary>
type Region = Region of Seg list

/// <summary>Moments of a region revolved 360° about the Z axis.</summary>
type RevolvedMoments =
    { Volume: float
      /// Z of the centroid of the solid (it lies on the axis).
      CentroidZ: float }

module Region =
    let segments (Region s) = s

    /// Closed polygon through the given points (straight segments).
    let polygon (pts: P2 list) : Region =
        let arr = Array.ofList pts
        Region [ for i in 0 .. arr.Length - 1 -> Line (arr[i], arr[(i + 1) % arr.Length]) ]

    let rect (r0: float) (r1: float) (z0: float) (z1: float) : Region =
        polygon [ P2.create r0 z0; P2.create r1 z0; P2.create r1 z1; P2.create r0 z1 ]

    /// Largest gap between a segment end and the next segment start (0 for a properly closed chain).
    let closureGap (Region segs) =
        let arr = Array.ofList segs
        if arr.Length = 0 then infinity
        else
            [ for i in 0 .. arr.Length - 1 ->
                let e = Seg.endPoint arr[i]
                let s = Seg.startPoint arr[(i + 1) % arr.Length]
                sqrt ((e.R - s.R) ** 2.0 + (e.Z - s.Z) ** 2.0) ]
            |> List.max

    /// Boundary integrals (Green's theorem): signed area, ∫∫R dA, ∫∫R·Z dA.
    let private integrals (Region segs) =
        let mutable a = 0.0
        let mutable mr = 0.0
        let mutable mrz = 0.0
        for s in segs do
            for i in 0 .. Gauss.nodes.Length - 1 do
                let t = Gauss.nodes[i]
                let w = Gauss.weights[i]
                let p = Seg.eval s t
                let d = Seg.deriv s t
                a <- a + w * p.R * d.Z
                mr <- mr + w * 0.5 * p.R * p.R * d.Z
                mrz <- mrz + w * 0.5 * p.R * p.R * p.Z * d.Z
        a, mr, mrz

    /// Absolute area of the region (mm²).
    let area (r: Region) = let a, _, _ = integrals r in abs a

    /// Volume and centroid of the solid obtained by revolving the region 360° about Z (Pappus–Guldinus).
    let revolve (r: Region) : RevolvedMoments =
        let a, mr, mrz = integrals r
        let sign = if a < 0.0 then -1.0 else 1.0
        let mr, mrz = sign * mr, sign * mrz
        if abs mr < 1e-300 then { Volume = 0.0; CentroidZ = 0.0 }
        else { Volume = 2.0 * Math.PI * mr; CentroidZ = mrz / mr }

    /// Boundary sample points (segment ends plus <paramref name="perCurve"/> interior points on curved segments).
    let samplePoints (perCurve: int) (Region segs) : P2 list =
        [ for s in segs do
            match s with
            | Line (p, _) -> yield p
            | Elliptic _ ->
                for k in 0 .. perCurve -> Seg.eval s (float k / float perCurve)
            ]
