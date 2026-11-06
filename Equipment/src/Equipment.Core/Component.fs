namespace Equipment.Core

open ROP

/// <summary>Material reference. Density is all the geometry core needs; <c>Code</c> is the link to the Materials library.</summary>
type MaterialRef =
    { Code: string
      /// kg/m³
      Density: float }

module MaterialRef =
    /// Carbon steel, 7850 kg/m³.
    let carbonSteel = { Code = "CS"; Density = 7850.0 }
    /// Austenitic stainless steel, 8000 kg/m³.
    let stainless = { Code = "SS"; Density = 8000.0 }
    let create code density = { Code = code; Density = density }

/// <summary>
/// A pressure part (or support/accessory) of the equipment: a parametric <see cref="Shape"/> + material + flags.
/// Its position is NOT stored here: it comes from the connections of the equipment graph.
/// </summary>
type Component =
    { Id: ComponentId
      /// Unique, human-readable mark ("S-1", "TS-1", "N3"…).
      Tag: string
      Description: string
      Shape: Shape
      Material: MaterialRef
      /// false = purchased/supplied by others: excluded from weight and envelope of the supply.
      Supplied: bool
      /// Free key/value data (design code, NDE, notes…) kept through persistence.
      Attributes: Map<string, string> }

module Component =
    /// Creates a validated component with a fresh id.
    let create (tag: string) (shape: Shape) (material: MaterialRef) : Returns<Component, Issue> =
        let errors =
            [ if System.String.IsNullOrWhiteSpace tag then yield InvalidParameter ("Tag", "must not be empty")
              if not (material.Density > 0.0) then yield InvalidParameter ("Material.Density", "must be > 0")
              yield! Shape.validate shape ]
        if errors.IsEmpty then
            Returns.ok
                { Id = ComponentId.New()
                  Tag = tag.Trim()
                  Description = Shape.kindName shape
                  Shape = shape
                  Material = material
                  Supplied = true
                  Attributes = Map.empty }
        else Returns.failmany errors

    let withId id (c: Component) = { c with Id = id }
    let withDescription d (c: Component) = { c with Description = d }
    let notSupplied (c: Component) = { c with Supplied = false }
    let withAttribute k v (c: Component) = { c with Attributes = Map.add k v c.Attributes }

    let ports (c: Component) = Shape.ports c.Shape
    let tryPort (name: string) (c: Component) = ports c |> List.tryFind (fun p -> p.PortName = name)

    /// Material volume (mm³): solids minus voids.
    let materialVolume (b: Body) =
        (b.Solids |> List.sumBy (fun p -> p.Volume)) - (b.Voids |> List.sumBy (fun p -> p.Volume))

    /// Mass in kg.
    let mass (c: Component) =
        Part.massKg c.Material.Density (materialVolume (Shape.body c.Shape))
