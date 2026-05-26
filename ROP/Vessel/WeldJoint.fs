namespace Vessel

// ── EN 13445 ──────────────────────────────────────────────────────────────────

/// Weld joint category per EN 13445-4 §6.
type EN13445JointCategory =
    | JA   // Longitudinal (meridional) butt joints in shells, cones, heads
    | JB   // Circumferential butt joints between shells/cones/heads
    | JC   // Branch / nozzle attachment welds
    | JD   // Non-pressure-retaining attachment welds (supports, clips, …)

/// Non-destructive examination level per EN 13445-5 §8, governing the joint coefficient z.
type EN13445NDELevel =
    | Level_a   // 100 % volumetric + 100 % surface  →  z = 1.00
    | Level_b   // Partial volumetric                →  z = 0.85
    | Level_c   // Spot / visual only                →  z ≤ 0.70

type EN13445WeldData = {
    Category         : EN13445JointCategory
    /// Joint coefficient  z: 1.00 / 0.85 / 0.70
    JointCoefficient : float
    NDELevel         : EN13445NDELevel
}

// ── ASME VIII Div.1 ───────────────────────────────────────────────────────────

/// Joint category per ASME VIII Div.1 UW-3.
type ASMEJointCategory =
    | CatA   // Longitudinal welds in shells, cones, nozzle necks; all butt welds in heads & flat plates
    | CatB   // Circumferential welds joining shells/cones/heads/nozzles
    | CatC   // Joints connecting flanges, flat heads, tube sheets to shells or nozzles
    | CatD   // Joints connecting nozzles/communicating chambers to shells, spheres, transitions

/// Weld joint type per ASME VIII Div.1 Table UW-12.
type ASMEJointType =
    | Type1   // Double-welded butt joint (full penetration from both sides)
    | Type2   // Single-welded butt joint with backing strip left in place
    | Type3   // Single-welded butt joint without backing strip
    | Type4   // Double full-fillet lap joint
    | Type5   // Single full-fillet lap joint with plug welds
    | Type6   // Single full-fillet corner joint

/// Radiographic / ultrasonic examination extent per ASME UW-11.
type ASME_NDE =
    | Full    // Full RT / UT → E = 1.00
    | Spot    // Spot RT / UT → E = 0.85
    | NoNDE   // No volumetric NDE → E = 0.70 (or 0.60 for Type 3)

type ASMEDiv1WeldData = {
    JointCategory   : ASMEJointCategory
    JointType       : ASMEJointType
    Examination     : ASME_NDE
    /// Joint efficiency  E: 1.00 / 0.85 / 0.70
    JointEfficiency : float
}

// ── ASME VIII Div.2 ───────────────────────────────────────────────────────────

/// Div.2 weld joint data. Full volumetric examination is required for Cat A and B joints;
/// the joint factor is normally 1.0 for qualified procedures.
type ASMEDiv2WeldData = {
    JointCategory   : ASMEJointCategory
    /// Weld joint factor (typically 1.0 with mandatory RT/UT for Cat A & B)
    WeldJointFactor : float
}

// ── AD 2000 Merkblatt ─────────────────────────────────────────────────────────

/// Weld class per AD 2000 Merkblatt HP 5/3.
type AD2000WeldClass =
    | WC1   // Highest quality – 100 % RT / UT
    | WC2   // High quality    – partial examination
    | WC3   // Standard quality – visual / MPI/PT only
    | WC4   // Lowest class (fillets, non-load-carrying)

type AD2000WeldData = {
    WeldClass       : AD2000WeldClass
    /// Joint efficiency factor  v: 1.0 / 0.85 / 0.7
    JointEfficiency : float
}

// ── PD 5500 ───────────────────────────────────────────────────────────────────

/// Examination category per PD 5500 Table 5.6.1-1.
type PD5500JointCategory =
    | Cat1   // Full radiography → z = 1.00
    | Cat2   // Partial radiography → z = 0.85
    | Cat3   // No radiography → z = 0.70 (or 0.60 for certain joint types)

type PD5500WeldData = {
    Category    : PD5500JointCategory
    /// Joint factor  z: 1.00 / 0.85 / 0.70 / 0.60
    JointFactor : float
}

// ── Union ─────────────────────────────────────────────────────────────────────

/// Weld standard data attached to a weld line on a component or to a graph edge.
/// Exactly one standard's data is carried per weld; multi-standard projects
/// create separate WeldLine / GraphEdge instances for each governing standard.
type WeldStandardData =
    | EN13445Weld of EN13445WeldData
    | ASMEDiv1Weld of ASMEDiv1WeldData
    | ASMEDiv2Weld of ASMEDiv2WeldData
    | AD2000Weld   of AD2000WeldData
    | PD5500Weld   of PD5500WeldData

module WeldStandardData =

    /// Extract the joint efficiency / coefficient regardless of standard.
    let jointEfficiency (w: WeldStandardData) : float =
        match w with
        | EN13445Weld d -> d.JointCoefficient
        | ASMEDiv1Weld d -> d.JointEfficiency
        | ASMEDiv2Weld d -> d.WeldJointFactor
        | AD2000Weld   d -> d.JointEfficiency
        | PD5500Weld   d -> d.JointFactor
