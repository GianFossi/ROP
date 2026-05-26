namespace Vessel

// ── Condition type ────────────────────────────────────────────────────────────

/// Nature of the design/operating scenario.
type ConditionType =
    | Design          // governing design basis
    | NormalOperating // continuous normal service
    | Hydrotest       // water pressure test
    | PneumaticTest   // gas pressure test
    | StartUp         // pressurisation / heat-up transient
    | ShutDown        // depressurisation / cool-down transient
    | Misoperation    // credible off-design event
    | Upset           // short-duration deviation
    | Emergency       // abnormal condition requiring protection response

// ── Load combination ──────────────────────────────────────────────────────────

/// Load combination multipliers for structural design checks.
/// Used per EN 13445-3 Annex B and ASME VIII Div.2 Table 5.3.
type LoadCombination = {
    Name                : string
    /// Pressure load factor (P)
    PressureFactor      : float
    /// Thermal / differential-temperature load factor (T)
    ThermalFactor       : float
    /// Permanent (dead) load factor – self-weight, insulation, contents (G)
    PermanentLoadFactor : float
    /// Variable / live load factor (Q)
    LiveLoadFactor      : float
    /// Wind load factor (W)
    WindFactor          : float
    /// Seismic / earthquake load factor (E)
    SeismicFactor       : float
}

// ── Design condition ──────────────────────────────────────────────────────────

/// A single design or operating condition defined at vessel level.
/// Internal-side quantities carry the suffix 'i'; external-side carry 'e'.
type DesignCondition = {
    Id            : string
    Name          : string
    ConditionType : ConditionType
    /// Internal (pressure-side) design pressure  [MPa], positive = above atmospheric
    Pi            : float option
    /// Internal (pressure-side) design temperature [°C]
    Ti            : float option
    /// External design pressure (vacuum or external overpressure) [MPa]
    Pe            : float option
    /// External design temperature [°C]
    Te            : float option
    /// Load combinations to be checked under this condition
    LoadCombinations : LoadCombination list
    Notes         : string
}

// ── Component-level override ──────────────────────────────────────────────────

/// Partial override of a vessel condition for a specific component.
/// Only the non-None fields replace the corresponding vessel-level value.
/// Used, for example, to assign a local metal temperature to a shell course
/// that differs from the vessel-level design temperature (e.g. in heat exchangers).
type ConditionOverride = {
    /// ID of the DesignCondition being overridden
    ConditionId : string
    Pi          : float option
    Ti          : float option
    Pe          : float option
    Te          : float option
}

// ── Vessel-level condition set ────────────────────────────────────────────────

/// The complete set of design conditions defined for the vessel.
/// Individual components reference condition IDs from this set
/// and may apply ConditionOverrides for component-specific deviations.
type DesignConditionSet = {
    Id         : string
    Name       : string
    Conditions : DesignCondition list
}

module DesignConditionSet =

    /// Look up a condition by ID.
    let tryFind (id: string) (set: DesignConditionSet) : DesignCondition option =
        set.Conditions |> List.tryFind (fun c -> c.Id = id)
