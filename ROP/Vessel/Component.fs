namespace Vessel

// ─────────────────────────────────────────────────────────────────────────────
// WALL PROTECTION  (corrosion / erosion / lining)
// ─────────────────────────────────────────────────────────────────────────────

/// Describes how one face (inner or outer) of the component wall is protected
/// against corrosion, erosion, or chemical attack.
type WallProtection =
    /// Uniform corrosion allowance added to the calculated thickness  [mm]
    | CorrosionAllowance  of ca        : float
    /// Metallurgically bonded cladding layer (roll-bonded, explosion-bonded, …)
    | Cladding            of materialId : string * thickness : float
    /// Weld-deposited overlay (e.g. stainless weld overlay on carbon steel shell)
    | WeldOverlay         of materialId : string * thickness : float
    | NoProtection

// ─────────────────────────────────────────────────────────────────────────────
// COMPONENT SIDE
// ─────────────────────────────────────────────────────────────────────────────

/// Classification of which "environment" the outer face of the component faces.
type ComponentSide =
    | PressureSide    // outer face exposed to process or contained pressure
    | Atmospheric     // outer face exposed to ambient atmosphere
    | Submerged       // outer face below a liquid level (e.g. buried, submerged)
    | Insulated       // outer face covered by thermal insulation

// ─────────────────────────────────────────────────────────────────────────────
// WELD LINES
// ─────────────────────────────────────────────────────────────────────────────

/// Orientation of a weld seam with respect to the component's axis.
type WeldOrientation =
    /// Runs parallel to the component axis (longitudinal seam weld – ASME Cat A / EN JA).
    | Meridional
    /// Runs around the circumference perpendicular to the axis (girth weld – ASME Cat B / EN JB).
    | Circumferential

/// A weld seam on the surface of a component.
type WeldLine = {
    Id            : string
    Orientation   : WeldOrientation
    /// Distance from node I along the component axis to the weld  [mm].
    /// Used primarily for circumferential welds (locates the girth weld).
    AxialPosition : float
    /// Angular position from the K-vector  [°], 0–360.
    /// None = full circumference (a complete girth weld spans 360°).
    PolarAngle    : float option
    WeldData      : WeldStandardData
    Notes         : string
}

// ─────────────────────────────────────────────────────────────────────────────
// NODAL LOADS
// ─────────────────────────────────────────────────────────────────────────────

/// Forces and moments applied at one of the component's nodes (global frame).
type NodalLoad = {
    /// "I" or "J", or a named sub-node (e.g. nozzle tip).
    NodeTag     : string
    Fx          : float   // [N]
    Fy          : float   // [N]
    Fz          : float   // [N]
    Mx          : float   // [N·mm]
    My          : float   // [N·mm]
    Mz          : float   // [N·mm]
    /// ID of the DesignCondition this load belongs to.
    ConditionId : string
}

// ─────────────────────────────────────────────────────────────────────────────
// COMPONENT  (vertex of the directed graph)
// ─────────────────────────────────────────────────────────────────────────────

/// A single pressure vessel component – the fundamental vertex in the vessel graph.
type Component = {

    // ── Identity ──────────────────────────────────────────────────────────────
    Id    : string
    Name  : string
    Notes : string

    // ── Geometry & spatial position ───────────────────────────────────────────
    /// Reference frame: I = start node, J = end node, K = section orientation vector.
    Frame    : ComponentFrame
    Geometry : ComponentGeometry

    // ── Design basis ──────────────────────────────────────────────────────────
    /// IDs of DesignConditions (from the vessel DesignConditionSet) that govern this component.
    DesignConditionIds : string list
    /// Component-specific overrides for individual condition parameters.
    ConditionOverrides : ConditionOverride list

    // ── Material & protection ─────────────────────────────────────────────────
    /// Reference to the material database record.
    MaterialId       : string
    /// Protection on the inner (wetted / process) surface.
    InnerProtection  : WallProtection
    /// Protection on the outer (atmospheric / external) surface.
    OuterProtection  : WallProtection
    Side             : ComponentSide

    // ── Applied loads ─────────────────────────────────────────────────────────
    NodalLoads : NodalLoad list

    // ── Mass & inertia ────────────────────────────────────────────────────────
    /// Dry weight of the component (steel only, no fluid)  [kg].
    Weight          : float
    /// Centre of gravity in global coordinates  [mm].
    CentreOfGravity : Point3D

    // ── Volumes [mm³] ─────────────────────────────────────────────────────────
    /// Fluid-side volume enclosed by the inner surface.
    InternalVolume  : float
    /// Volume enclosed by the outer surface (envelope including wall).
    ExternalVolume  : float
    /// Metal volume  = ExternalVolume − InternalVolume  (for weight calculation).
    ComponentVolume : float

    // ── Surfaces [mm²] ────────────────────────────────────────────────────────
    /// Wetted / process-side surface area.
    InternalSurface : float
    /// External (insulated or bare) surface area.
    ExternalSurface : float

    // ── Welds ─────────────────────────────────────────────────────────────────
    /// Longitudinal / meridional seam welds on this component (ASME Cat A / EN JA).
    MeridionalWelds      : WeldLine list
    /// Girth / circumferential welds on this component (ASME Cat B / EN JB).
    CircumferentialWelds : WeldLine list
}
