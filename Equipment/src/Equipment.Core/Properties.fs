namespace Equipment.Core

open System.Collections.Generic
open ROP

/// <summary>Mass properties of one placed component.</summary>
type ComponentProperties =
    { Id: ComponentId
      Tag: string
      Supplied: bool
      /// kg
      Mass: float
      /// Material volume, mm³ (solids minus voids).
      MaterialVolume: float
      /// World centroid of the material (None if the component has no material).
      Centroid: Vec3 option }

/// <summary>Fluid chamber: set of cavities that communicate through connected, open ports.</summary>
type Chamber =
    { Index: int
      Cavities: (ComponentId * string) list
      /// Gross internal volume, mm³ (no deduction yet for tubes or other internals occupying the chamber).
      Volume: float
      Centroid: Vec3 option }

type EquipmentProperties =
    { Components: ComponentProperties list
      /// Weight of the supply (components flagged as supplied), kg.
      SuppliedMass: float
      /// Mass of components excluded from the supply, kg.
      NotSuppliedMass: float
      /// World centre of gravity of the supply.
      Centroid: Vec3 option
      /// Axis-aligned envelope of the supplied components in the requested frame.
      Envelope: Aabb option
      /// Volume of the envelope box, mm³ (shipping volume).
      EnvelopeVolume: float
      Chambers: Chamber list }

module Properties =

    let private materialPart (c: Component) (b: Body) =
        let vs = b.Solids |> List.sumBy (fun p -> p.Volume)
        let vv = b.Voids |> List.sumBy (fun p -> p.Volume)
        let m = (b.Solids |> List.fold (fun acc p -> acc + (p.Volume * p.Centroid)) Vec3.Zero)
                + (b.Voids |> List.fold (fun acc p -> acc + (-p.Volume * p.Centroid)) Vec3.Zero)
        let v = vs - vv
        v, (if abs v > 1e-300 then Some ((1.0 / v) * m) else None)

    /// Partition of all cavities of the placed components into fluid chambers (union–find over open ports).
    let chambers (eq: Equipment) (p: Placement) (bodies: IDictionary<ComponentId, Body>) : Chamber list =
        let parent = Dictionary<(ComponentId * string), (ComponentId * string)>()
        let rec find x =
            let px = parent[x]
            if px = x then x
            else
                let r = find px
                parent[x] <- r
                r
        let union a b =
            if parent.ContainsKey a && parent.ContainsKey b then
                let ra, rb = find a, find b
                if ra <> rb then parent[ra] <- rb
        for c in eq.Components do
            if p.Frames.ContainsKey c.Id then
                for cav in bodies[c.Id].Cavities do
                    parent[(c.Id, cav.CavityName)] <- (c.Id, cav.CavityName)
        let opens (r: PortRef) =
            bodies.TryGetValue r.Owner
            |> function
                | true, b -> b.Ports |> List.tryFind (fun x -> x.PortName = r.Port) |> Option.bind (fun x -> x.Opens)
                | _ -> None
        for conn in eq.Connections do
            match opens conn.A, opens conn.B with
            | Some ca, Some cb -> union (conn.A.Owner, ca) (conn.B.Owner, cb)
            | _ -> ()
        let groups = parent.Keys |> Seq.toList |> List.groupBy find
        let order = eq.Components |> List.mapi (fun i c -> c.Id, i) |> Map.ofList
        groups
        |> List.map (fun (_, members) ->
            let members = members |> List.sortBy (fun (id, n) -> order[id], n)
            let mutable vol = 0.0
            let mutable mom = Vec3.Zero
            for (id, name) in members do
                let cav = bodies[id].Cavities |> List.find (fun x -> x.CavityName = name)
                let w = Frame.apply p.Frames[id] cav.Space.Centroid
                vol <- vol + cav.Space.Volume
                mom <- mom + (cav.Space.Volume * w)
            members, vol, (if vol > 1e-9 then Some ((1.0 / vol) * mom) else None))
        |> List.filter (fun (_, vol, _) -> vol > 1e-9)
        |> List.sortBy (fun (m, _, _) -> let (id, n) = List.head m in order[id], n)
        |> List.mapi (fun i (m, vol, c) -> { Index = i + 1; Cavities = m; Volume = vol; Centroid = c })

    /// Mass, centre of gravity, envelope (in <paramref name="shippingFrame"/> axes; identity = world) and chambers.
    /// Only components with a resolved position take part.
    let computeIn (shippingFrame: Frame) (eq: Equipment) (p: Placement) : EquipmentProperties =
        let bodies = Dictionary<ComponentId, Body>()
        for c in eq.Components do
            if p.Frames.ContainsKey c.Id then bodies[c.Id] <- Shape.body c.Shape
        let comps =
            [ for c in eq.Components do
                match bodies.TryGetValue c.Id with
                | true, b ->
                    let v, cl = materialPart c b
                    yield
                        { Id = c.Id
                          Tag = c.Tag
                          Supplied = c.Supplied
                          Mass = Part.massKg c.Material.Density v
                          MaterialVolume = v
                          Centroid = cl |> Option.map (Frame.apply p.Frames[c.Id]) }
                | _ -> () ]
        let supplied = comps |> List.filter (fun c -> c.Supplied)
        let massS = supplied |> List.sumBy (fun c -> c.Mass)
        let cog =
            if massS > 0.0 then
                let m = supplied |> List.fold (fun acc c -> match c.Centroid with Some x -> acc + (c.Mass * x) | None -> acc) Vec3.Zero
                Some ((1.0 / massS) * m)
            else None
        let toShipping = Frame.inverse shippingFrame
        let envelope =
            [ for c in eq.Components do
                if c.Supplied then
                    match bodies.TryGetValue c.Id with
                    | true, b ->
                        let f = Frame.compose toShipping p.Frames[c.Id]
                        for part in b.Solids do
                            match Hull.boundsOf f part.Hull with
                            | Some box -> yield box
                            | None -> ()
                    | _ -> () ]
            |> Aabb.ofSeq
        { Components = comps
          SuppliedMass = massS
          NotSuppliedMass = comps |> List.filter (fun c -> not c.Supplied) |> List.sumBy (fun c -> c.Mass)
          Centroid = cog
          Envelope = envelope
          EnvelopeVolume = envelope |> Option.map (fun b -> b.Volume) |> Option.defaultValue 0.0
          Chambers = chambers eq p bodies }

    /// Properties in world axes.
    let compute (eq: Equipment) (p: Placement) = computeIn Frame.identity eq p

    /// Validates the assembly then computes the properties (warnings from validation are preserved).
    let ofEquipment (eq: Equipment) : Returns<EquipmentProperties, Issue> =
        Assembly.validate eq |> Returns.map (compute eq)
