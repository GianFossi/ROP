namespace Vessel

// ─────────────────────────────────────────────────────────────────────────────
// CYLINDER
// ─────────────────────────────────────────────────────────────────────────────

type CylinderGeometry = {
    /// Inner diameter  [mm]
    InnerDiameter : float
    /// Nominal wall thickness  [mm]
    WallThickness : float
    /// Developed length along the axis  [mm]
    Length        : float
}

// ─────────────────────────────────────────────────────────────────────────────
// CONE  (reducer / transition piece)
// ─────────────────────────────────────────────────────────────────────────────

type ConeGeometry = {
    /// Inner diameter at the large end (node I)  [mm]
    InnerDiameterLarge : float
    /// Inner diameter at the small end (node J)  [mm]
    InnerDiameterSmall : float
    /// Nominal wall thickness  [mm]
    WallThickness      : float
    /// Half apex angle  α  [°]  (measured from the cone axis)
    HalfApexAngle      : float
    /// Axial length I→J  [mm]
    Length             : float
}

// ─────────────────────────────────────────────────────────────────────────────
// HEADS  (dished / formed ends)
// ─────────────────────────────────────────────────────────────────────────────

/// Geometry variant for dished heads.
type HeadShape =
    /// Semi-ellipsoidal head.  hOverD = h/D ratio; standard 2:1 is hOverD = 0.25.
    | Ellipsoidal    of hOverD        : float
    /// Full hemisphere: h = D/2.
    | Hemispherical
    /// Torispherical head defined by crown radius R and knuckle radius r.
    ///   Klöpper:  R = D,   r = 0.1 D
    ///   Korbbogen: R = 0.8 D, r = 0.154 D
    | Torispherical  of crownRadius   : float * knuckleRadius : float
    /// Conical dished end (cone with apex).
    | ConicalHead    of halfApexAngle : float

type HeadGeometry = {
    Shape         : HeadShape
    /// Inner diameter at the cylindrical flange (junction with shell)  [mm]
    InnerDiameter : float
    /// Nominal wall thickness  [mm]
    WallThickness : float
}

// ─────────────────────────────────────────────────────────────────────────────
// FLAT HEAD / BLIND COVER
// ─────────────────────────────────────────────────────────────────────────────

type FlatHeadAttachment =
    | WeldedIntegral   // full-penetration set-in weld
    | WeldedFillet     // fillet weld (category C per ASME / EN)
    | Bolted           // gasketed bolted cover or blind flange
    | Screwed          // threaded closure

type FlatHeadGeometry = {
    /// Shell bore / nominal inner diameter  [mm]
    InnerDiameter     : float
    /// Plate thickness  e  [mm]
    Thickness         : float
    /// Effective gasket or bolt-circle diameter used in thickness formula  [mm]
    DiameterAtGasket  : float
    Attachment        : FlatHeadAttachment
    /// Shape factor  C  [-]  (per EN 13445-3 Table 10.2 or ASME UG-34)
    ShapeFactor       : float
}

// ─────────────────────────────────────────────────────────────────────────────
// FLANGE
// ─────────────────────────────────────────────────────────────────────────────

type FlangeType =
    | WeldNeck        // WN – long tapered hub
    | SlipOn          // SO – low-hub, fillet-welded
    | LapJoint        // LJ – loose ring over lap stub
    | SocketWeld      // SW – socket bore, fillet-welded
    | Blind           // BL – solid plate
    | RingPlate       // Plate flange / ring flange
    | Integral        // Machined integral with vessel wall

type FlangeFacing =
    | RaisedFace      // RF – standard
    | FlatFace        // FF – used with full-face gasket
    | RTJGroove       // RTJ – oval or octagonal ring groove
    | TongueAndGroove // T&G – tongue side or groove side
    | MaleAndFemale   // M&F – male or female

type FlangeGeometry = {
    FlangeType          : FlangeType
    Facing              : FlangeFacing
    /// Nominal (bore) diameter  [mm]
    NominalDiameter     : float
    /// Flange outer diameter  [mm]
    OuterDiameter       : float
    /// Seating face / groove inner diameter  [mm]
    FaceInnerDiameter   : float
    /// Seating face / groove outer diameter  [mm]
    FaceOuterDiameter   : float
    /// Flange ring thickness  [mm]
    Thickness           : float
    /// Hub length (0 for blind, slip-on, plate)  [mm]
    HubLength           : float
    /// Bolt-circle diameter  [mm]
    BoltCircleDiameter  : float
    BoltHoleCount       : int
    BoltHoleDiameter    : float  // [mm]
    /// Applicable standard, e.g. "ASME B16.5", "EN 1092-1"
    Standard            : string
    /// Pressure-temperature rating, e.g. "CL300", "PN40"
    Rating              : string
}

// ─────────────────────────────────────────────────────────────────────────────
// GASKET
// ─────────────────────────────────────────────────────────────────────────────

type GasketType =
    | FlatSheet             // soft, semi-metallic or metallic sheet
    | SpiralWound           // SWG
    | RTJRing               // RTJ – oval or octagonal metallic ring
    | DoubleCorrugatedMetal
    | Kammprofile           // grooved metal with soft facing layers

type GasketGeometry = {
    GasketType      : GasketType
    InnerDiameter   : float   // [mm]
    OuterDiameter   : float   // [mm]
    /// Uncompressed thickness  [mm]
    Thickness       : float
    /// Gasket factor  m  [-]  (ASME App. 2 / EN 1591)
    GasketFactor    : float
    /// Minimum seating stress  y  [MPa]
    SeatingStress   : float
    /// Effective seating half-width  b  [mm]
    EffectiveWidth  : float
}

// ─────────────────────────────────────────────────────────────────────────────
// BOLT SET
// ─────────────────────────────────────────────────────────────────────────────

type ThreadType = | Metric | UNC | UNF | BSPP | BSPT

type BoltSetGeometry = {
    Count               : int
    /// Nominal bolt diameter  [mm]
    NominalDiameter     : float
    ThreadType          : ThreadType
    /// Thread pitch  [mm]  (or TPI for inch threads)
    ThreadPitch         : float
    /// Bolt-circle diameter  [mm]
    BoltCircleDiameter  : float
    /// Clamping (grip) length  [mm]
    ActiveLength        : float
    MaterialId          : string   // reference to material database
    /// Number of nuts per bolt position (usually 2: stud + 2 nuts)
    NutCount            : int
}

// ─────────────────────────────────────────────────────────────────────────────
// NOZZLE
// ─────────────────────────────────────────────────────────────────────────────

type NozzleProjection =
    | Flush                  // flush with inner surface (set-flush)
    | Protruding of length: float   // protrudes into the vessel [mm]
    | Recessed   of depth:  float   // recessed below outer surface [mm]

type NozzleGeometry = {
    /// Nominal bore  [mm]
    NominalDiameter  : float
    OuterDiameter    : float   // [mm]
    WallThickness    : float   // [mm]
    NeckLength       : float   // [mm] from shell surface to flange face
    Projection       : NozzleProjection
    /// Optional reinforcement pad: (outer diameter, thickness) [mm]
    ReinforcementPad : (float * float) option
    /// Angular position measured from the K-vector of the parent component  [°]
    PolarAngle       : float
    /// Distance along the parent component axis from node I  [mm]
    AxialPosition    : float
}

// ─────────────────────────────────────────────────────────────────────────────
// TUBE SHEET
// ─────────────────────────────────────────────────────────────────────────────

type TubeLayout =
    | Triangular      // 30° triangular pitch (most common)
    | Rotated30       // 60° rotated triangular
    | Square          // 90° square pitch
    | Rotated45       // 45° rotated square

type TubeSheetGeometry = {
    OuterDiameter       : float        // [mm]
    Thickness           : float        // [mm]
    TubeOD              : float        // [mm] tube outer diameter
    TubeWT              : float        // [mm] tube wall thickness
    /// Centre-to-centre tube pitch  [mm]
    TubePitch           : float
    TubeLayout          : TubeLayout
    TubeCount           : int
    TubePassCount       : int          // number of tube-side passes
    /// Ligament efficiency  η  [-]  (ratio of net metal to pitch)
    LigamentEfficiency  : float
    /// True = fixed tube sheet; False = floating or U-tube sheet
    IsFixed             : bool
    /// True = tube sheet is gasketed to shell (TEMA); False = integral weld
    IsGasketed          : bool
}

// ─────────────────────────────────────────────────────────────────────────────
// TUBE BUNDLE  (baffle assembly)
// ─────────────────────────────────────────────────────────────────────────────

type BaffleType =
    | SingleSegmental  of cutFraction : float   // cut fraction of shell ID [-]
    | DoubleSegmental  of cutFraction : float
    | TripleSegmental  of cutFraction : float
    | DiscAndDoughnut  of discOD : float * doughnutID : float   // [mm]
    | HelicalBaffle    of pitchAngle : float    // [°]

type BaffleData = {
    BaffleType     : BaffleType
    OuterDiameter  : float   // [mm]
    Thickness      : float   // [mm]
    /// Centre-to-centre spacing between adjacent baffles  [mm]
    BaffleSpacing  : float
    Count          : int
}

type TieRodData = {
    Count    : int
    Diameter : float   // [mm]
    /// Full length from tube sheet to last spacer or baffle  [mm]
    Length   : float
}

type SpacerData = {
    Count         : int
    InnerDiameter : float   // [mm]
    OuterDiameter : float   // [mm]
    Length        : float   // [mm] length of each spacer tube
}

type ImpingementPlateData = {
    Width     : float   // [mm]
    Length    : float   // [mm]
    Thickness : float   // [mm]
}

type TubeBundleGeometry = {
    Baffles          : BaffleData list
    TieRods          : TieRodData
    Spacers          : SpacerData list
    ImpingementPlate : ImpingementPlateData option
}

// ─────────────────────────────────────────────────────────────────────────────
// EXPANSION BELLOWS
// ─────────────────────────────────────────────────────────────────────────────

type ExpansionBellowsGeometry = {
    InnerDiameter        : float   // [mm] convolution root diameter
    OuterDiameter        : float   // [mm] convolution crown diameter
    TotalLength          : float   // [mm] overall installed length
    /// Convolution height  h  [mm]  (= (OD − ID) / 2)
    ConvolutionHeight    : float
    /// Convolution pitch  q  [mm]
    ConvolutionPitch     : float
    ConvolutionCount     : int
    PlyCount             : int
    PlyThickness         : float   // [mm] single ply
    MaxAxialCompression  : float   // [mm]
    MaxAxialExtension    : float   // [mm]
    MaxLateralDeflection : float   // [mm]
    MaxAngularRotation   : float   // [°]
}

// ─────────────────────────────────────────────────────────────────────────────
// COMPONENT GEOMETRY UNION
// ─────────────────────────────────────────────────────────────────────────────

/// Typed geometry of a pressure vessel component.
/// Each variant carries the data specific to its shape class.
type ComponentGeometry =
    | Cylinder        of CylinderGeometry
    | Cone            of ConeGeometry
    | Head            of HeadGeometry
    | FlatHead        of FlatHeadGeometry
    | Flange          of FlangeGeometry
    | Gasket          of GasketGeometry
    | BoltSet         of BoltSetGeometry
    | Nozzle          of NozzleGeometry
    | TubeSheet       of TubeSheetGeometry
    | TubeBundle      of TubeBundleGeometry
    | ExpansionBellows of ExpansionBellowsGeometry
