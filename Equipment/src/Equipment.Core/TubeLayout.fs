namespace Equipment.Core

open System

/// <summary>Centre of a tube in the plane of the tube sheet (mm, tube-bundle local X/Y).</summary>
[<Struct>]
type TubePos = { X: float; Y: float }

/// <summary>U-tube: both legs lie in the same Y row; the bend is a semicircle of radius |XB-XA|/2 in the XZ plane.</summary>
[<Struct>]
type UTube =
    { Y: float
      XA: float
      XB: float }
    member t.BendRadius = 0.5 * abs (t.XB - t.XA)
    member t.BendCentreX = 0.5 * (t.XA + t.XB)

/// <summary>Generators of tube-field layouts.</summary>
module TubeLayout =

    /// Triangular pitch. <paramref name="rotationDeg"/> = 0 → rows parallel to X; 90° → rows parallel to Y.
    /// Only tubes whose centre lies within <paramref name="maxCentreRadius"/> (= (OTL - tube OD)/2) are kept.
    let triangular (pitch: float) (maxCentreRadius: float) (rotationDeg: float) : TubePos list =
        let h = pitch * sqrt 3.0 / 2.0
        let n = int (ceil (maxCentreRadius / h)) + 2
        let rot = rotationDeg * Math.PI / 180.0
        let c, s = cos rot, sin rot
        [ for j in -n .. n do
            let off = if j % 2 = 0 then 0.0 else 0.5 * pitch
            for i in -n .. n do
                let x = float i * pitch + off
                let y = float j * h
                if sqrt (x * x + y * y) <= maxCentreRadius + 1e-9 then
                    yield { X = x * c - y * s; Y = x * s + y * c } ]

    /// Square pitch (also usable rotated by 45° for rotated-square layouts).
    let square (pitch: float) (maxCentreRadius: float) (rotationDeg: float) : TubePos list =
        let n = int (ceil (maxCentreRadius / pitch)) + 1
        let rot = rotationDeg * Math.PI / 180.0
        let c, s = cos rot, sin rot
        [ for j in -n .. n do
            for i in -n .. n do
                let x = float i * pitch
                let y = float j * pitch
                if sqrt (x * x + y * y) <= maxCentreRadius + 1e-9 then
                    yield { X = x * c - y * s; Y = x * s + y * c } ]

    /// Removes the tubes that fall inside a pass-partition lane of the given total width centred on X = 0.
    let withoutLane (laneWidth: float) (tubes: TubePos list) =
        tubes |> List.filter (fun t -> abs t.X >= 0.5 * laneWidth - 1e-9)

    /// Pairs tubes of the same row symmetric about X = 0 into nested U-tubes (innermost with innermost).
    /// Returns the pairs and the tubes that found no partner.
    let pairIntoU (tubes: TubePos list) : UTube list * TubePos list =
        let rows = tubes |> List.groupBy (fun t -> Math.Round(t.Y, 6)) |> List.sortBy fst
        let pairs = ResizeArray<UTube>()
        let leftovers = ResizeArray<TubePos>()
        for (_, row) in rows do
            let left = row |> List.filter (fun t -> t.X < 0.0) |> List.sortByDescending (fun t -> t.X)
            let right = row |> List.filter (fun t -> t.X > 0.0) |> List.sortBy (fun t -> t.X)
            let m = min left.Length right.Length
            for k in 0 .. m - 1 do
                pairs.Add { Y = left[k].Y; XA = left[k].X; XB = right[k].X }
            leftovers.AddRange(List.skip m left)
            leftovers.AddRange(List.skip m right)
            leftovers.AddRange(row |> List.filter (fun t -> t.X = 0.0))
        List.ofSeq pairs, List.ofSeq leftovers
