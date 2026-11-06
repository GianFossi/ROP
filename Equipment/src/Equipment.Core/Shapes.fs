namespace Equipment.Core

open System

// ---------------------------------------------------------------------------------------------------------------------
// Local frame conventions
//   * Revolved components (shells, heads, flanges, plates, tube bundles): the axis is the local Z axis.
//   * "End1"/"Weld"/"Face"/"Face1"/"Bottom" ports sit at z = 0 with outward normal -Z; the opposite end has normal +Z.
//   * A port frame has its Z axis along the OUTWARD normal. Mating two ports makes the normals opposite.
// ---------------------------------------------------------------------------------------------------------------------

/// <summary>
/// Connection point of a component. <c>Opens</c> names the internal cavity this port communicates with (None for a
/// solid/closed face); two connected ports that both open a cavity put those cavities in the same fluid chamber.
/// <c>MaxConnections</c> None = unlimited (e.g. a tube field receiving thousands of tubes).
/// </summary>
type Port =
    { PortName: string
      Frame: Frame
      Opens: string option
      MaxConnections: int option }

/// <summary>Fluid cavity of a component (possibly zero-volume "interface" cavity, e.g. the two faces of a tube sheet).</summary>
type Cavity = { CavityName: string; Space: Part }

/// <summary>Radial opening in a cylindrical shell (nozzle neck, manway…); becomes a port at the shell outer surface.</summary>
type Opening =
    { OpeningName: string
      AxialPosition: float
      AngleDeg: float
      Diameter: float }

type CylindricalShellSpec = { InnerDiameter: float; Thickness: float; Length: float; Openings: Opening list }
type PipeSpec = { OuterDiameter: float; Thickness: float; Length: float }
type ConicalShellSpec = { SmallInnerDiameter: float; LargeInnerDiameter: float; Thickness: float; Length: float }
/// <summary>Ellipsoidal head: <c>Ratio</c> = a/b (2.0 for the usual 2:1 head). Outer surface = ellipse with semi-axes (a+t, b+t).</summary>
type EllipsoidalHeadSpec = { InnerDiameter: float; Thickness: float; Ratio: float; StraightFlange: float }
type HemisphericalHeadSpec = { InnerDiameter: float; Thickness: float; StraightFlange: float }
/// <summary>Torispherical head (inside crown radius Rc and knuckle radius rk). Klöpper: Rc = Di, rk = 0.1·Di; Korbbogen: Rc = 0.8·Di, rk = 0.154·Di.</summary>
type TorisphericalHeadSpec =
    { InnerDiameter: float
      Thickness: float
      CrownRadius: float
      KnuckleRadius: float
      StraightFlange: float }
type FlatPlateSpec = { Diameter: float; Thickness: float }
/// <summary>Tube sheet: plate with <c>HoleCount</c> holes of <c>HoleDiameter</c> (drilled through the whole thickness).</summary>
type TubesheetSpec = { Diameter: float; Thickness: float; HoleDiameter: float; HoleCount: int }
/// <summary>Simplified weld-neck flange: ring + straight hub; the bore is constant.</summary>
type FlangeSpec =
    { OuterDiameter: float
      BoreDiameter: float
      Thickness: float
      HubOuterDiameter: float
      HubLength: float }
type BlockSpec = { Length: float; Width: float; Height: float }
type StraightBundleSpec = { TubeOuterDiameter: float; TubeThickness: float; TubeLength: float; Positions: TubePos list }
type UBundleSpec = { TubeOuterDiameter: float; TubeThickness: float; StraightLength: float; Tubes: UTube list }

/// <summary>Parametric description of a pressure-part (the single source of truth also used by the 3D engine).</summary>
type Shape =
    | CylindricalShell of CylindricalShellSpec
    | Pipe of PipeSpec
    | ConicalShell of ConicalShellSpec
    | EllipsoidalHead of EllipsoidalHeadSpec
    | HemisphericalHead of HemisphericalHeadSpec
    | TorisphericalHead of TorisphericalHeadSpec
    | FlatPlate of FlatPlateSpec
    | Tubesheet of TubesheetSpec
    | WeldNeckFlange of FlangeSpec
    | Block of BlockSpec
    | StraightTubeBundle of StraightBundleSpec
    | UTubeBundle of UBundleSpec

/// <summary>Geometry derived from a <see cref="Shape"/> in the component's local frame.</summary>
type Body =
    { /// Material volumes (added).
      Solids: Part list
      /// Material removed from the solids (holes, openings).
      Voids: Part list
      Cavities: Cavity list
      Ports: Port list }

module Shape =

    let private P = P2.create
    let private pi = Math.PI

    // ---------- smart constructors (avoid record-label ambiguity at call sites) ----------
    let cylindricalShell innerDiameter thickness length : Shape =
        CylindricalShell ({ InnerDiameter = innerDiameter; Thickness = thickness; Length = length; Openings = [] } : CylindricalShellSpec)
    let pipe outerDiameter thickness length : Shape =
        Pipe ({ OuterDiameter = outerDiameter; Thickness = thickness; Length = length } : PipeSpec)
    let conicalShell smallInnerDiameter largeInnerDiameter thickness length : Shape =
        ConicalShell ({ SmallInnerDiameter = smallInnerDiameter; LargeInnerDiameter = largeInnerDiameter; Thickness = thickness; Length = length } : ConicalShellSpec)
    let ellipsoidalHead innerDiameter thickness ratio straightFlange : Shape =
        EllipsoidalHead ({ InnerDiameter = innerDiameter; Thickness = thickness; Ratio = ratio; StraightFlange = straightFlange } : EllipsoidalHeadSpec)
    let hemisphericalHead innerDiameter thickness straightFlange : Shape =
        HemisphericalHead ({ InnerDiameter = innerDiameter; Thickness = thickness; StraightFlange = straightFlange } : HemisphericalHeadSpec)
    let torisphericalHead innerDiameter thickness crownRadius knuckleRadius straightFlange : Shape =
        TorisphericalHead ({ InnerDiameter = innerDiameter; Thickness = thickness; CrownRadius = crownRadius; KnuckleRadius = knuckleRadius; StraightFlange = straightFlange } : TorisphericalHeadSpec)
    /// Klöpperboden (DIN 28011): Rc = Di, rk = 0.1 Di.
    let kloepperHead innerDiameter thickness straightFlange =
        torisphericalHead innerDiameter thickness innerDiameter (0.1 * innerDiameter) straightFlange
    let flatPlate diameter thickness : Shape = FlatPlate ({ Diameter = diameter; Thickness = thickness } : FlatPlateSpec)
    let tubesheet diameter thickness holeDiameter holeCount : Shape =
        Tubesheet ({ Diameter = diameter; Thickness = thickness; HoleDiameter = holeDiameter; HoleCount = holeCount } : TubesheetSpec)
    let weldNeckFlange outerDiameter boreDiameter thickness hubOuterDiameter hubLength : Shape =
        WeldNeckFlange ({ OuterDiameter = outerDiameter; BoreDiameter = boreDiameter; Thickness = thickness; HubOuterDiameter = hubOuterDiameter; HubLength = hubLength } : FlangeSpec)
    let block length width height : Shape = Block ({ Length = length; Width = width; Height = height } : BlockSpec)
    let straightTubeBundle tubeOuterDiameter tubeThickness tubeLength positions : Shape =
        StraightTubeBundle ({ TubeOuterDiameter = tubeOuterDiameter; TubeThickness = tubeThickness; TubeLength = tubeLength; Positions = positions } : StraightBundleSpec)
    let uTubeBundle tubeOuterDiameter tubeThickness straightLength tubes : Shape =
        UTubeBundle ({ TubeOuterDiameter = tubeOuterDiameter; TubeThickness = tubeThickness; StraightLength = straightLength; Tubes = tubes } : UBundleSpec)

    /// Adds an opening to a cylindrical shell (other shapes are returned unchanged).
    let withOpening (o: Opening) (s: Shape) : Shape =
        match s with
        | CylindricalShell c -> CylindricalShell { c with Openings = c.Openings @ [ o ] }
        | other -> other

    let kindName = function
        | CylindricalShell _ -> "Cylindrical shell"
        | Pipe _ -> "Pipe"
        | ConicalShell _ -> "Conical shell"
        | EllipsoidalHead _ -> "Ellipsoidal head"
        | HemisphericalHead _ -> "Hemispherical head"
        | TorisphericalHead _ -> "Torispherical head"
        | FlatPlate _ -> "Flat plate"
        | Tubesheet _ -> "Tube sheet"
        | WeldNeckFlange _ -> "Weld-neck flange"
        | Block _ -> "Block"
        | StraightTubeBundle _ -> "Straight tube bundle"
        | UTubeBundle _ -> "U-tube bundle"

    // ---------- validation ----------
    let validate (shape: Shape) : Issue list =
        let errs = ResizeArray<Issue>()
        let positive subject (v: float) =
            if not (v > 0.0) || Double.IsInfinity v then errs.Add(InvalidParameter (subject, sprintf "must be > 0 (was %g)" v))
        let nonNeg subject (v: float) =
            if not (v >= 0.0) || Double.IsInfinity v then errs.Add(InvalidParameter (subject, sprintf "must be >= 0 (was %g)" v))
        let check cond subject detail = if not cond then errs.Add(InvalidParameter (subject, detail))
        match shape with
        | CylindricalShell s ->
            positive "InnerDiameter" s.InnerDiameter; positive "Thickness" s.Thickness; positive "Length" s.Length
            let names = s.Openings |> List.map (fun o -> o.OpeningName)
            check (names.Length = (List.distinct names).Length) "Openings" "opening names must be unique"
            for o in s.Openings do
                check (o.OpeningName <> "End1" && o.OpeningName <> "End2" && o.OpeningName <> "") "Openings" (sprintf "invalid opening name '%s'" o.OpeningName)
                positive (sprintf "Opening %s diameter" o.OpeningName) o.Diameter
                check (o.AxialPosition - 0.5 * o.Diameter >= 0.0 && o.AxialPosition + 0.5 * o.Diameter <= s.Length)
                    "Openings" (sprintf "opening '%s' exceeds the shell length" o.OpeningName)
                check (o.Diameter < s.InnerDiameter) "Openings" (sprintf "opening '%s' is larger than the shell" o.OpeningName)
        | Pipe s ->
            positive "OuterDiameter" s.OuterDiameter; positive "Thickness" s.Thickness; positive "Length" s.Length
            check (2.0 * s.Thickness < s.OuterDiameter) "Thickness" "thickness must be < OuterDiameter/2"
        | ConicalShell s ->
            positive "SmallInnerDiameter" s.SmallInnerDiameter; positive "LargeInnerDiameter" s.LargeInnerDiameter
            positive "Thickness" s.Thickness; positive "Length" s.Length
            check (s.LargeInnerDiameter >= s.SmallInnerDiameter) "LargeInnerDiameter" "must be >= SmallInnerDiameter"
        | EllipsoidalHead s ->
            positive "InnerDiameter" s.InnerDiameter; positive "Thickness" s.Thickness; positive "Ratio" s.Ratio; nonNeg "StraightFlange" s.StraightFlange
        | HemisphericalHead s ->
            positive "InnerDiameter" s.InnerDiameter; positive "Thickness" s.Thickness; nonNeg "StraightFlange" s.StraightFlange
        | TorisphericalHead s ->
            positive "InnerDiameter" s.InnerDiameter; positive "Thickness" s.Thickness
            positive "CrownRadius" s.CrownRadius; positive "KnuckleRadius" s.KnuckleRadius; nonNeg "StraightFlange" s.StraightFlange
            let a = 0.5 * s.InnerDiameter
            check (s.KnuckleRadius <= a) "KnuckleRadius" "must be <= InnerDiameter/2"
            check (s.CrownRadius >= a) "CrownRadius" "must be >= InnerDiameter/2"
            check (s.CrownRadius >= s.KnuckleRadius) "CrownRadius" "must be >= KnuckleRadius"
        | FlatPlate s -> positive "Diameter" s.Diameter; positive "Thickness" s.Thickness
        | Tubesheet s ->
            positive "Diameter" s.Diameter; positive "Thickness" s.Thickness; nonNeg "HoleDiameter" s.HoleDiameter
            check (s.HoleCount >= 0) "HoleCount" "must be >= 0"
        | WeldNeckFlange s ->
            positive "OuterDiameter" s.OuterDiameter; positive "BoreDiameter" s.BoreDiameter; positive "Thickness" s.Thickness
            positive "HubOuterDiameter" s.HubOuterDiameter; nonNeg "HubLength" s.HubLength
            check (s.BoreDiameter < s.HubOuterDiameter) "HubOuterDiameter" "must be > BoreDiameter"
            check (s.HubOuterDiameter <= s.OuterDiameter) "OuterDiameter" "must be >= HubOuterDiameter"
        | Block s -> positive "Length" s.Length; positive "Width" s.Width; positive "Height" s.Height
        | StraightTubeBundle s ->
            positive "TubeOuterDiameter" s.TubeOuterDiameter; positive "TubeThickness" s.TubeThickness; positive "TubeLength" s.TubeLength
            check (2.0 * s.TubeThickness < s.TubeOuterDiameter) "TubeThickness" "thickness must be < OD/2"
            check (not s.Positions.IsEmpty) "Positions" "at least one tube is required"
        | UTubeBundle s ->
            positive "TubeOuterDiameter" s.TubeOuterDiameter; positive "TubeThickness" s.TubeThickness; nonNeg "StraightLength" s.StraightLength
            check (2.0 * s.TubeThickness < s.TubeOuterDiameter) "TubeThickness" "thickness must be < OD/2"
            check (not s.Tubes.IsEmpty) "Tubes" "at least one tube is required"
            check (s.Tubes |> List.forall (fun t -> t.BendRadius > 0.5 * s.TubeOuterDiameter)) "Tubes"
                "every bend radius must exceed the tube radius (legs would overlap)"
        List.ofSeq errs

    /// Depth of the dished part (excluding straight flange) of a torispherical head, inside surface.
    let torisphericalDepth (innerDiameter: float) (crownRadius: float) (knuckleRadius: float) =
        let a = 0.5 * innerDiameter
        crownRadius - sqrt ((crownRadius - knuckleRadius) ** 2.0 - (a - knuckleRadius) ** 2.0)

    // ---------- ports (cheap: never builds the volumes) ----------
    let private port name origin normal opens maxConn =
        { PortName = name; Frame = Frame.ofZX origin normal Vec3.UnitX; Opens = opens; MaxConnections = maxConn }

    let private zAt z = Vec3.create 0.0 0.0 z
    let private down = Vec3.create 0.0 0.0 -1.0
    let private up = Vec3.UnitZ

    let private endPorts (n1: string) (n2: string) length (opens: string option) =
        [ port n1 (zAt 0.0) down opens (Some 1)
          port n2 (zAt length) up opens (Some 1) ]

    let private openingPort (ro: float) (o: Opening) : Port =
        let th = o.AngleDeg * pi / 180.0
        let n = Vec3.create (cos th) (sin th) 0.0
        { PortName = o.OpeningName
          Frame = Frame.ofZX (Vec3.create (ro * cos th) (ro * sin th) o.AxialPosition) n Vec3.UnitZ
          Opens = Some "Bore"
          MaxConnections = Some 1 }

    let private headPorts outerHeight =
        [ port "Weld" (zAt 0.0) down (Some "Bore") (Some 1)
          port "Apex" (zAt outerHeight) up (Some "Bore") (Some 1) ]

    /// Ports of a shape, in the shape's local frame.
    let ports (shape: Shape) : Port list =
        match shape with
        | CylindricalShell s ->
            endPorts "End1" "End2" s.Length (Some "Bore")
            @ (s.Openings |> List.map (openingPort (0.5 * s.InnerDiameter + s.Thickness)))
        | Pipe s -> endPorts "End1" "End2" s.Length (Some "Bore")
        | ConicalShell s -> endPorts "End1" "End2" s.Length (Some "Bore")
        | EllipsoidalHead s ->
            let a = 0.5 * s.InnerDiameter
            headPorts (s.StraightFlange + a / s.Ratio + s.Thickness)
        | HemisphericalHead s ->
            headPorts (s.StraightFlange + 0.5 * s.InnerDiameter + s.Thickness)
        | TorisphericalHead s ->
            headPorts (s.StraightFlange + torisphericalDepth s.InnerDiameter s.CrownRadius s.KnuckleRadius + s.Thickness)
        | FlatPlate s ->
            [ port "Face1" (zAt 0.0) down None (Some 1); port "Face2" (zAt s.Thickness) up None (Some 1) ]
        | Tubesheet s ->
            [ port "Face1" (zAt 0.0) down (Some "Face1Side") (Some 1)
              port "Face2" (zAt s.Thickness) up (Some "Face2Side") (Some 1)
              port "TubeField1" (zAt 0.0) up (Some "Face1Side") None
              port "TubeField2" (zAt s.Thickness) down (Some "Face2Side") None ]
        | WeldNeckFlange s ->
            let h = s.Thickness + (if s.HubLength > 0.0 then s.HubLength else 0.0)
            [ port "Face" (zAt 0.0) down (Some "Bore") (Some 1)
              port "Weld" (zAt h) up (Some "Bore") (Some 1) ]
        | Block s -> [ port "Bottom" (zAt 0.0) down None (Some 1); port "Top" (zAt s.Height) up None (Some 1) ]
        | StraightTubeBundle s -> endPorts "End1" "End2" s.TubeLength (Some "TubeBore")
        | UTubeBundle _ ->
            [ port "EndA" (zAt 0.0) down (Some "TubeBore") (Some 1)
              port "EndB" (zAt 0.0) down (Some "TubeBore") (Some 1) ]

    // ---------- volumes ----------
    let private tubeBody (ri: float) (t: float) (length: float) (openings: Opening list) : Body =
        let ro = ri + t
        let solid = Part.revolve (Region.rect ri ro 0.0 length)
        let bore = Part.revolve (Region.rect 0.0 ri 0.0 length)
        let hole (o: Opening) =
            let th = o.AngleDeg * pi / 180.0
            let n = Vec3.create (cos th) (sin th) 0.0
            let f = Frame.ofZX (Vec3.create (ri * cos th) (ri * sin th) o.AxialPosition) n Vec3.UnitZ
            Part.revolve (Region.rect 0.0 (0.5 * o.Diameter) 0.0 t) |> Part.transform f
        { Solids = [ solid ]
          Voids = openings |> List.map hole
          Cavities = [ { CavityName = "Bore"; Space = bore } ]
          Ports = [] }

    let private coneBody (r1: float) (r2: float) (t: float) (length: float) : Body =
        let len = sqrt (length * length + (r2 - r1) ** 2.0)
        let dr = t * len / length // radial thickness giving a wall thickness t normal to the generatrix
        let solid = Region.polygon [ P r1 0.0; P (r1 + dr) 0.0; P (r2 + dr) length; P r2 length ]
        let bore = Region.polygon [ P 0.0 0.0; P r1 0.0; P r2 length; P 0.0 length ]
        { Solids = [ Part.revolve solid ]
          Voids = []
          Cavities = [ { CavityName = "Bore"; Space = Part.revolve bore } ]
          Ports = [] }

    let private headBody (solid: Region) (cavity: Region) : Body =
        { Solids = [ Part.revolve solid ]
          Voids = []
          Cavities = [ { CavityName = "Bore"; Space = Part.revolve cavity } ]
          Ports = [] }

    let private ellipsoidalBody (a: float) (b: float) (t: float) (sf: float) : Body =
        let solid =
            Region [ Line (P (a + t) 0.0, P (a + t) sf)
                     Elliptic (P 0.0 sf, a + t, b + t, 0.0, pi / 2.0)
                     Line (P 0.0 (sf + b + t), P 0.0 (sf + b))
                     Elliptic (P 0.0 sf, a, b, pi / 2.0, -pi / 2.0)
                     Line (P a sf, P a 0.0)
                     Line (P a 0.0, P (a + t) 0.0) ]
        let cavity =
            Region [ Line (P 0.0 0.0, P a 0.0)
                     Line (P a 0.0, P a sf)
                     Elliptic (P 0.0 sf, a, b, 0.0, pi / 2.0)
                     Line (P 0.0 (sf + b), P 0.0 0.0) ]
        headBody solid cavity

    let private torisphericalBody (a: float) (t: float) (rc: float) (rk: float) (sf: float) : Body =
        let zc = -sqrt ((rc - rk) ** 2.0 - (a - rk) ** 2.0) // crown centre, relative to the end of the straight flange
        let k = P (a - rk) sf
        let c = P 0.0 (sf + zc)
        let phi = atan2 (k.Z - c.Z) (k.R - c.R)
        let apexIn = c.Z + rc
        let solid =
            Region [ Line (P (a + t) 0.0, P (a + t) sf)
                     Seg.arc k (rk + t) 0.0 phi
                     Seg.arc c (rc + t) phi (pi / 2.0 - phi)
                     Line (P 0.0 (apexIn + t), P 0.0 apexIn)
                     Seg.arc c rc (pi / 2.0) -(pi / 2.0 - phi)
                     Seg.arc k rk phi -phi
                     Line (P a sf, P a 0.0)
                     Line (P a 0.0, P (a + t) 0.0) ]
        let cavity =
            Region [ Line (P 0.0 0.0, P a 0.0)
                     Line (P a 0.0, P a sf)
                     Seg.arc k rk 0.0 phi
                     Seg.arc c rc phi (pi / 2.0 - phi)
                     Line (P 0.0 apexIn, P 0.0 0.0) ]
        headBody solid cavity

    let private plateBody (d: float) (t: float) (holes: Part list) (tubeField: bool) : Body =
        let solid = Part.revolve (Region.rect 0.0 (0.5 * d) 0.0 t)
        let cavities =
            if tubeField then
                [ { CavityName = "Face1Side"; Space = { Part.empty with Centroid = zAt 0.0 } }
                  { CavityName = "Face2Side"; Space = { Part.empty with Centroid = zAt t } } ]
            else []
        { Solids = [ solid ]; Voids = holes; Cavities = cavities; Ports = [] }

    let private flangeBody (s: FlangeSpec) : Body =
        let ro, rb, rh = 0.5 * s.OuterDiameter, 0.5 * s.BoreDiameter, 0.5 * s.HubOuterDiameter
        let h = s.Thickness + s.HubLength
        let solid =
            if s.HubLength > 0.0 then
                Region.polygon [ P rb 0.0; P ro 0.0; P ro s.Thickness; P rh s.Thickness; P rh h; P rb h ]
            else Region.rect rb ro 0.0 s.Thickness
        { Solids = [ Part.revolve solid ]
          Voids = []
          Cavities = [ { CavityName = "Bore"; Space = Part.revolve (Region.rect 0.0 rb 0.0 h) } ]
          Ports = [] }

    let private straightBundleBody (s: StraightBundleSpec) : Body =
        let ro, ri, l = 0.5 * s.TubeOuterDiameter, 0.5 * s.TubeOuterDiameter - s.TubeThickness, s.TubeLength
        let n = float s.Positions.Length
        let cx = (s.Positions |> List.sumBy (fun p -> p.X)) / n
        let cy = (s.Positions |> List.sumBy (fun p -> p.Y)) / n
        let one = Part.revolve (Region.rect ri ro 0.0 l)
        let bore1 = Part.revolve (Region.rect 0.0 ri 0.0 l)
        let hull =
            [ for p in s.Positions do
                yield Ring (Vec3.create p.X p.Y 0.0, Vec3.UnitZ, ro)
                yield Ring (Vec3.create p.X p.Y l, Vec3.UnitZ, ro) ]
        let solid = { Volume = n * one.Volume; Centroid = Vec3.create cx cy (0.5 * l); Hull = hull }
        let bore = { Volume = n * bore1.Volume; Centroid = Vec3.create cx cy (0.5 * l); Hull = [] }
        { Solids = [ solid ]; Voids = []; Cavities = [ { CavityName = "TubeBore"; Space = bore } ]; Ports = [] }

    let private uBundleBody (s: UBundleSpec) : Body =
        let ro, ri, ls = 0.5 * s.TubeOuterDiameter, 0.5 * s.TubeOuterDiameter - s.TubeThickness, s.StraightLength
        let leg = Part.revolve (Region.rect ri ro 0.0 ls)
        let legBore = Part.revolve (Region.rect 0.0 ri 0.0 ls)
        let build (legPart: Part) (outerR: float) (innerR: float) =
            Part.combine
                [ for t in s.Tubes do
                    yield Part.translate (Vec3.create t.XA t.Y 0.0) legPart
                    yield Part.translate (Vec3.create t.XB t.Y 0.0) legPart
                    yield Part.translate (Vec3.create t.BendCentreX t.Y ls) (Part.semicircularBend outerR innerR t.BendRadius) ]
        let solid = build leg ro ri
        let bore = { build legBore ri 0.0 with Hull = [] }
        { Solids = [ solid ]; Voids = []; Cavities = [ { CavityName = "TubeBore"; Space = bore } ]; Ports = [] }

    /// Builds the local geometry (volumes + ports) of a shape. The shape must be valid (see <see cref="validate"/>).
    let body (shape: Shape) : Body =
        let raw =
            match shape with
            | CylindricalShell s -> tubeBody (0.5 * s.InnerDiameter) s.Thickness s.Length s.Openings
            | Pipe s -> tubeBody (0.5 * s.OuterDiameter - s.Thickness) s.Thickness s.Length []
            | ConicalShell s -> coneBody (0.5 * s.SmallInnerDiameter) (0.5 * s.LargeInnerDiameter) s.Thickness s.Length
            | EllipsoidalHead s ->
                let a = 0.5 * s.InnerDiameter
                ellipsoidalBody a (a / s.Ratio) s.Thickness s.StraightFlange
            | HemisphericalHead s ->
                let a = 0.5 * s.InnerDiameter
                ellipsoidalBody a a s.Thickness s.StraightFlange
            | TorisphericalHead s ->
                torisphericalBody (0.5 * s.InnerDiameter) s.Thickness s.CrownRadius s.KnuckleRadius s.StraightFlange
            | FlatPlate s -> plateBody s.Diameter s.Thickness [] false
            | Tubesheet s ->
                let holeVol = float s.HoleCount * pi / 4.0 * s.HoleDiameter ** 2.0 * s.Thickness
                let holes = if s.HoleCount > 0 then [ { Volume = holeVol; Centroid = zAt (0.5 * s.Thickness); Hull = [] } ] else []
                plateBody s.Diameter s.Thickness holes true
            | WeldNeckFlange s -> flangeBody s
            | Block s -> { Solids = [ Part.box s.Length s.Width s.Height ]; Voids = []; Cavities = []; Ports = [] }
            | StraightTubeBundle s -> straightBundleBody s
            | UTubeBundle s -> uBundleBody s
        { raw with Ports = ports shape }
