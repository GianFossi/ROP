namespace Vessel

/// Single entry in a temperature-dependent material property table.
type PropertyTableEntry = {
    /// Temperature [°C]
    Temperature : float
    /// Property value in SI units (MPa, 1/°C, W/(m·°C), J/(kg·°C), …)
    Value       : float
}

/// Temperature-dependent mechanical and thermal properties.
/// Scalar properties are assumed temperature-independent; tabular ones are interpolated.
type MaterialPropertyTables = {
    /// Yield (proof) strength  Re / Rp0.2  [MPa]
    Sy              : PropertyTableEntry list
    /// Ultimate tensile strength  Rm         [MPa]
    Su              : PropertyTableEntry list
    /// Young's modulus  E                    [MPa]
    ElasticModulus  : PropertyTableEntry list
    /// Poisson's ratio  ν                    [-]
    PoissonRatio    : float
    /// Mean coefficient of thermal expansion  α  [1/°C]
    Alpha           : PropertyTableEntry list
    /// Thermal conductivity  λ               [W/(m·°C)]
    Conductivity    : PropertyTableEntry list
    /// Density  ρ                            [kg/mm³]
    Density         : float
    /// Specific heat capacity  cp             [J/(kg·°C)]
    SpecificHeat    : PropertyTableEntry list
}

/// Product form that may affect allowable stress or thickness limits.
type ProductForm =
    | Plate
    | Pipe
    | Tube
    | Bar
    | Forging
    | Casting
    | Fitting
    | Sheet

/// Full material record, referencing a property dataset potentially stored in a database.
type Material = {
    Id          : string
    Name        : string
    /// Applicable standard, e.g. "EN 10028-2", "ASME SA-516", "ASTM A106"
    Standard    : string
    /// Material grade / type, e.g. "P265GH", "Grade 70", "Gr. B"
    Grade       : string
    ProductForm : ProductForm
    Properties  : MaterialPropertyTables
}

module MaterialProperties =

    /// Linear interpolation for a sorted pair of table entries.
    let private lerp (t: float) (lo: PropertyTableEntry) (hi: PropertyTableEntry) =
        if hi.Temperature = lo.Temperature then lo.Value
        else lo.Value + (hi.Value - lo.Value) * (t - lo.Temperature) / (hi.Temperature - lo.Temperature)

    /// Retrieve the interpolated value of a property at the given temperature [°C].
    /// Clamps to the boundary values when the temperature is outside the table range.
    let valueAt (temperature: float) (table: PropertyTableEntry list) : float =
        match table with
        | []       -> 0.0
        | [ e ]    -> e.Value
        | entries  ->
            let sorted = entries |> List.sortBy (fun e -> e.Temperature)
            match sorted |> List.tryFindIndex (fun e -> e.Temperature >= temperature) with
            | None    -> (List.last sorted).Value
            | Some 0  -> (List.head sorted).Value
            | Some i  -> lerp temperature sorted.[i - 1] sorted.[i]
