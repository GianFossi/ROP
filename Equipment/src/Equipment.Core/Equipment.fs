namespace Equipment.Core

open System.Collections.Generic
open ROP

/// <summary>Reference to a port of a component.</summary>
type PortRef = { Owner: ComponentId; Port: string }

/// <summary>
/// Edge of the equipment graph: port A of one component mated to port B of another (normals opposed, frames
/// coincident, then displaced by <c>Offset</c> expressed in the frame of port A: angular clocking, axial gap…).
/// The graph may contain cycles and several edges between the same two components.
/// </summary>
type Connection =
    { Id: ConnectionId
      A: PortRef
      B: PortRef
      Offset: Frame }

/// <summary>
/// The equipment: an undirected port graph of <see cref="Component"/>s. Positions are not stored per component:
/// they derive from <c>Anchors</c> (explicitly placed components, normally just one) and the mating of connected
/// ports, so editing a dimension or an offset moves the whole branch automatically (parametric assembly).
/// Edges that close a loop are verified geometrically instead of being used to place anything.
/// </summary>
type Equipment =
    { Name: string
      /// In insertion order (defines traversal order and reports).
      Components: Component list
      Connections: Connection list
      Anchors: Map<ComponentId, Frame> }

/// <summary>Result of resolving the positions of the graph.</summary>
type Placement =
    { /// Local → world frame of every placed component.
      Frames: Map<ComponentId, Frame>
      /// Connections used to place a component (the spanning forest).
      TreeEdges: Set<ConnectionId>
      /// Connections that close a loop (verified, not used for placing).
      ClosingEdges: ConnectionId list
      /// Components not reachable from any anchor.
      Unplaced: ComponentId list }

module Equipment =

    /// Tolerances used to accept a closing connection as geometrically consistent.
    module Tolerance =
        let distanceMm = 0.01
        let angleDeg = 0.01

    let empty (name: string) : Equipment =
        { Name = name; Components = []; Connections = []; Anchors = Map.empty }

    // ------------------------------------------------------------------------------------------------ queries
    let tryComponent (id: ComponentId) (eq: Equipment) = eq.Components |> List.tryFind (fun c -> c.Id = id)
    let tryComponentByTag (tag: string) (eq: Equipment) = eq.Components |> List.tryFind (fun c -> c.Tag = tag)
    let tryConnection (id: ConnectionId) (eq: Equipment) = eq.Connections |> List.tryFind (fun c -> c.Id = id)

    let component' (id: ComponentId) (eq: Equipment) : Returns<Component, Issue> =
        match tryComponent id eq with
        | Some c -> Returns.ok c
        | None -> Returns.fail (ComponentNotFound id)

    let componentByTag (tag: string) (eq: Equipment) : Returns<Component, Issue> =
        match tryComponentByTag tag eq with
        | Some c -> Returns.ok c
        | None -> Returns.fail (ComponentTagNotFound tag)

    let tagOf (id: ComponentId) (eq: Equipment) =
        match tryComponent id eq with Some c -> c.Tag | None -> string id

    let connectionsOf (id: ComponentId) (eq: Equipment) =
        eq.Connections |> List.filter (fun c -> c.A.Owner = id || c.B.Owner = id)

    /// Components directly connected to the given one (distinct, in connection order).
    let neighbours (id: ComponentId) (eq: Equipment) : ComponentId list =
        connectionsOf id eq
        |> List.map (fun c -> if c.A.Owner = id then c.B.Owner else c.A.Owner)
        |> List.distinct

    let ref (c: Component) (port: string) : PortRef = { Owner = c.Id; Port = port }

    let private describeEdge (eq: Equipment) (c: Connection) =
        sprintf "%s.%s ↔ %s.%s" (tagOf c.A.Owner eq) c.A.Port (tagOf c.B.Owner eq) c.B.Port

    // ------------------------------------------------------------------------------------------------ placement
    let private portTable (eq: Equipment) =
        let cache = Dictionary<ComponentId, Map<string, Frame>>()
        fun (id: ComponentId) (name: string) ->
            let table =
                match cache.TryGetValue id with
                | true, t -> Some t
                | _ ->
                    match tryComponent id eq with
                    | Some c ->
                        let t = Shape.ports c.Shape |> List.map (fun p -> p.PortName, p.Frame) |> Map.ofList
                        cache[id] <- t
                        Some t
                    | None -> None
            table |> Option.bind (Map.tryFind name)

    /// Frame of the component at the other end of <paramref name="conn"/>, predicted from the frame of the component at one end.
    let private predict (portFrame: ComponentId -> string -> Frame option) (conn: Connection) (fromA: bool) (fu: Frame) =
        if fromA then
            match portFrame conn.A.Owner conn.A.Port, portFrame conn.B.Owner conn.B.Port with
            | Some pa, Some pb ->
                let bPortWorld = Frame.compose (Frame.compose (Frame.compose fu pa) conn.Offset) Frame.flip
                Some (conn.B.Owner, Frame.compose bPortWorld (Frame.inverse pb))
            | _ -> None
        else
            match portFrame conn.A.Owner conn.A.Port, portFrame conn.B.Owner conn.B.Port with
            | Some pa, Some pb ->
                let bPortWorld = Frame.compose fu pb
                let aPortWorld = Frame.compose (Frame.compose bPortWorld Frame.flip) (Frame.inverse conn.Offset)
                Some (conn.A.Owner, Frame.compose aPortWorld (Frame.inverse pa))
            | _ -> None

    /// Resolves the position of every component reachable from an anchor (breadth-first over the graph).
    let resolve (eq: Equipment) : Placement =
        let portFrame = portTable eq
        let adjacency = Dictionary<ComponentId, ResizeArray<Connection>>()
        let add id c =
            match adjacency.TryGetValue id with
            | true, l -> l.Add c
            | _ -> adjacency[id] <- ResizeArray [ c ]
        for c in eq.Connections do
            add c.A.Owner c
            if c.B.Owner <> c.A.Owner then add c.B.Owner c
        let frames = Dictionary<ComponentId, Frame>()
        let seen = HashSet<ConnectionId>()
        let tree = HashSet<ConnectionId>()
        let closing = ResizeArray<ConnectionId>()
        let queue = Queue<ComponentId>()
        for c in eq.Components do
            match Map.tryFind c.Id eq.Anchors with
            | Some f ->
                frames[c.Id] <- f
                queue.Enqueue c.Id
            | None -> ()
        while queue.Count > 0 do
            let u = queue.Dequeue()
            let fu = frames[u]
            match adjacency.TryGetValue u with
            | true, edges ->
                for conn in edges do
                    if seen.Add conn.Id then
                        let fromA = (conn.A.Owner = u)
                        match predict portFrame conn fromA fu with
                        | Some (v, fv) ->
                            if frames.ContainsKey v then closing.Add conn.Id
                            else
                                frames[v] <- fv
                                tree.Add conn.Id |> ignore
                                queue.Enqueue v
                        | None -> ()
            | _ -> ()
        { Frames = frames |> Seq.map (fun kv -> kv.Key, kv.Value) |> Map.ofSeq
          TreeEdges = Set.ofSeq tree
          ClosingEdges = List.ofSeq closing
          Unplaced = eq.Components |> List.filter (fun c -> not (frames.ContainsKey c.Id)) |> List.map (fun c -> c.Id) }

    /// (distance mm, angle °) between where a closing connection wants the component and where it actually is.
    let closureDeviation (eq: Equipment) (placement: Placement) (conn: Connection) : (float * float) option =
        let portFrame = portTable eq
        match Map.tryFind conn.A.Owner placement.Frames, Map.tryFind conn.B.Owner placement.Frames with
        | Some fa, Some fb ->
            match predict portFrame conn true fa with
            | Some (_, predicted) -> Some (Frame.deviation predicted fb)
            | None -> None
        | _ -> None

    /// Warnings for every closing connection that does not close within tolerance.
    let closureIssues (eq: Equipment) (placement: Placement) : Issue list =
        [ for id in placement.ClosingEdges do
            match tryConnection id eq with
            | Some conn ->
                match closureDeviation eq placement conn with
                | Some (d, a) when d > Tolerance.distanceMm || a > Tolerance.angleDeg ->
                    yield ClosingMismatch (id, describeEdge eq conn, d, a)
                | _ -> ()
            | None -> () ]

    // ------------------------------------------------------------------------------------------------ editing
    let private withClosureWarnings (eq: Equipment) (value: 'a) : Returns<'a, Issue> =
        Returns.warnmany (closureIssues eq (resolve eq)) value

    /// Components that were placed before an edit but lost their path to an anchor are anchored where they were.
    let private reanchorOrphans (before: Placement) (eq: Equipment) : Equipment * Issue list =
        let mutable current = eq
        let issues = ResizeArray<Issue>()
        let mutable go = true
        while go do
            let p = resolve current
            let orphan =
                current.Components
                |> List.tryFind (fun c -> List.contains c.Id p.Unplaced && Map.containsKey c.Id before.Frames)
            match orphan with
            | Some c ->
                current <- { current with Anchors = Map.add c.Id before.Frames[c.Id] current.Anchors }
                issues.Add(Reanchored c.Tag)
            | None -> go <- false
        current, List.ofSeq issues

    let private addRaw (c: Component) (eq: Equipment) : Returns<Equipment, Issue> =
        if eq.Components |> List.exists (fun x -> x.Tag = c.Tag) then Returns.fail (DuplicateTag c.Tag)
        elif eq.Components |> List.exists (fun x -> x.Id = c.Id) then Returns.fail (InvalidParameter ("Id", "component id already present"))
        else
            Returns.ok
                { eq with
                    Components = eq.Components @ [ c ]
                    Anchors = if eq.Anchors.IsEmpty then Map.add c.Id Frame.identity eq.Anchors else eq.Anchors }

    /// Adds a component. The first component becomes the anchor at the origin; later ones stay unplaced until connected.
    let addComponent (c: Component) (eq: Equipment) : Returns<Equipment, Issue> =
        let isFirst = eq.Anchors.IsEmpty
        addRaw c eq |> Returns.map id |> fun r -> if isFirst then r else Returns.bind (fun e -> Returns.warn (Unconnected c.Tag) e) r

    /// Connects two ports; the new connection id is returned with the equipment.
    let connect (a: PortRef) (b: PortRef) (offset: Frame) (eq: Equipment) : Returns<Equipment * ConnectionId, Issue> =
        returns {
            let! ca = component' a.Owner eq
            let! cb = component' b.Owner eq
            do! if a.Owner = b.Owner then Returns.fail (SelfConnection (ca.Tag, a.Port)) else Returns.ok ()
            let! pa =
                Component.tryPort a.Port ca
                |> Option.map Returns.ok
                |> Option.defaultValue (Returns.fail (PortNotFound (ca.Tag, a.Port)))
            and! pb =
                Component.tryPort b.Port cb
                |> Option.map Returns.ok
                |> Option.defaultValue (Returns.fail (PortNotFound (cb.Tag, b.Port)))
            let uses (r: PortRef) =
                eq.Connections |> List.filter (fun c -> c.A = r || c.B = r) |> List.length
            do! if eq.Connections |> List.exists (fun c -> (c.A = a && c.B = b) || (c.A = b && c.B = a))
                then Returns.fail (DuplicateConnection (ca.Tag, a.Port, cb.Tag, b.Port))
                else Returns.ok ()
            do! match pa.MaxConnections with
                | Some m when uses a >= m -> Returns.fail (PortCapacityExceeded (ca.Tag, a.Port))
                | _ -> Returns.ok ()
            do! match pb.MaxConnections with
                | Some m when uses b >= m -> Returns.fail (PortCapacityExceeded (cb.Tag, b.Port))
                | _ -> Returns.ok ()
            let conn = { Id = ConnectionId.New(); A = a; B = b; Offset = offset }
            let eq' = { eq with Connections = eq.Connections @ [ conn ] }
            return! withClosureWarnings eq' (eq', conn.Id)
        }

    /// Same as <see cref="connect"/> but addressing components by tag.
    let connectTags (tagA: string) (portA: string) (tagB: string) (portB: string) (offset: Frame) (eq: Equipment) =
        returns {
            let! ca = componentByTag tagA eq
            and! cb = componentByTag tagB eq
            return! connect (ref ca portA) (ref cb portB) offset eq
        }

    /// Adds a new component and mates its port to an existing one in a single step.
    let addConnected (c: Component) (ownPort: string) (existingTag: string) (existingPort: string) (offset: Frame) (eq: Equipment) =
        returns {
            let! eq1 = addRaw c eq
            let! eq2, id = connectTags c.Tag ownPort existingTag existingPort offset eq1
            return eq2, id
        }

    /// Removes a connection. Branches that lose their path to an anchor are anchored where they currently are.
    let disconnect (id: ConnectionId) (eq: Equipment) : Returns<Equipment, Issue> =
        match tryConnection id eq with
        | None -> Returns.fail (ConnectionNotFound id)
        | Some _ ->
            let before = resolve eq
            let eq1 = { eq with Connections = eq.Connections |> List.filter (fun c -> c.Id <> id) }
            let eq2, issues = reanchorOrphans before eq1
            Returns.warnmany (issues @ closureIssues eq2 (resolve eq2)) eq2

    /// Removes a component and all its connections (detached branches keep their position).
    let removeComponent (id: ComponentId) (eq: Equipment) : Returns<Equipment, Issue> =
        match tryComponent id eq with
        | None -> Returns.fail (ComponentNotFound id)
        | Some _ ->
            let before = resolve eq
            let eq1 =
                { eq with
                    Components = eq.Components |> List.filter (fun c -> c.Id <> id)
                    Connections = eq.Connections |> List.filter (fun c -> c.A.Owner <> id && c.B.Owner <> id)
                    Anchors = Map.remove id eq.Anchors }
            let eq2, issues = reanchorOrphans before eq1
            Returns.warnmany (issues @ closureIssues eq2 (resolve eq2)) eq2

    /// Replaces a component by an edited copy (same id). Connections must still refer to existing ports; the whole
    /// branch is repositioned automatically because positions derive from the connections.
    let updateComponent (id: ComponentId) (edit: Component -> Component) (eq: Equipment) : Returns<Equipment, Issue> =
        match tryComponent id eq with
        | None -> Returns.fail (ComponentNotFound id)
        | Some old ->
            let c = { edit old with Id = id }
            let errors =
                [ yield! Shape.validate c.Shape
                  if System.String.IsNullOrWhiteSpace c.Tag then yield InvalidParameter ("Tag", "must not be empty")
                  if not (c.Material.Density > 0.0) then yield InvalidParameter ("Material.Density", "must be > 0")
                  if eq.Components |> List.exists (fun x -> x.Id <> id && x.Tag = c.Tag) then yield DuplicateTag c.Tag
                  let ports = Shape.ports c.Shape |> List.map (fun p -> p.PortName) |> Set.ofList
                  for conn in connectionsOf id eq do
                      for r in [ conn.A; conn.B ] do
                          if r.Owner = id && not (ports.Contains r.Port) then yield PortNotFound (c.Tag, r.Port) ]
                |> List.distinct
            if not errors.IsEmpty then Returns.failmany errors
            else
                let eq' = { eq with Components = eq.Components |> List.map (fun x -> if x.Id = id then c else x) }
                withClosureWarnings eq' eq'

    let replaceShape (id: ComponentId) (shape: Shape) (eq: Equipment) =
        updateComponent id (fun c -> { c with Shape = shape }) eq

    /// Changes the offset of a connection (moves/rotates everything hanging from it on the far side).
    let setConnectionOffset (id: ConnectionId) (offset: Frame) (eq: Equipment) : Returns<Equipment, Issue> =
        match tryConnection id eq with
        | None -> Returns.fail (ConnectionNotFound id)
        | Some _ ->
            let eq' = { eq with Connections = eq.Connections |> List.map (fun c -> if c.Id = id then { c with Offset = offset } else c) }
            withClosureWarnings eq' eq'

    /// Pins a component at an explicit world frame (moving the whole equipment if it is the only anchor).
    let setAnchor (id: ComponentId) (frame: Frame) (eq: Equipment) : Returns<Equipment, Issue> =
        match tryComponent id eq with
        | None -> Returns.fail (ComponentNotFound id)
        | Some _ ->
            let eq' = { eq with Anchors = Map.add id frame eq.Anchors }
            withClosureWarnings eq' eq'

    /// Removes the explicit position of an anchor (the component must remain reachable from another anchor).
    let releaseAnchor (id: ComponentId) (eq: Equipment) : Returns<Equipment, Issue> =
        match tryComponent id eq with
        | None -> Returns.fail (ComponentNotFound id)
        | Some c ->
            let eq' = { eq with Anchors = Map.remove id eq.Anchors }
            let p = resolve eq'
            if List.contains id p.Unplaced then Returns.fail (NotPlaced c.Tag) else withClosureWarnings eq' eq'

    let rename (name: string) (eq: Equipment) = { eq with Name = name }
