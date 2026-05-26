namespace Vessel

// ─────────────────────────────────────────────────────────────────────────────
// GRAPH EDGE  (connection between two components)
// ─────────────────────────────────────────────────────────────────────────────

/// Which node of a component an edge is attached to.
type EdgeNodeTag = | NodeI | NodeJ

/// Physical or functional nature of the connection between two components.
type ConnectionType =
    | ShellToHead              // cylinder / cone end capped by a dished head
    | ShellToTubeSheet         // shell attached to a fixed or floating tube sheet
    | ShellToCone              // concentric / eccentric reducer between two shells
    | ConeToShell              // same joint, reversed direction
    | NozzleOnShell            // branch nozzle welded to a cylindrical shell
    | NozzleOnHead             // branch nozzle welded to a head
    | FlangeToFlange           // bolted flanged joint (gasket lives between)
    | GasketInJoint            // explicit gasket component between two flanges
    | BoltSetInJoint           // bolt set joining two flanges / flat covers
    | TubesToTubeSheet         // tube bundle to a tube sheet (welded or expanded)
    | BundleInsideShell        // tube bundle assembly housed inside a shell
    | ExpansionJointInline     // bellows connecting two coaxial shells or nozzles
    | PipelineAttachment       // external pipeline or piping component at a nozzle
    | SaddleOrSupportAttachment // saddle, skirt, lug, leg support point
    | HeadToFloatingTubeSheet  // floating head cover attached to a floating tube sheet
    | Custom of description: string

/// A directed edge in the vessel graph.
/// The direction Source → Target follows the flow of pressure or structural load path
/// (e.g. main shell is the source; its nozzle is the target).
type GraphEdge = {
    Id                : string
    SourceComponentId : string
    SourceNode        : EdgeNodeTag
    TargetComponentId : string
    TargetNode        : EdgeNodeTag
    ConnectionType    : ConnectionType
    /// Weld joint data when the connection is a welded joint;
    /// None for bolted, slip-fit, or expanded connections.
    WeldData          : WeldStandardData option
    Notes             : string
}

// ─────────────────────────────────────────────────────────────────────────────
// VESSEL GRAPH
// ─────────────────────────────────────────────────────────────────────────────

/// Directed graph model of a pressure vessel or multi-vessel assembly.
///
/// Components are vertices; GraphEdges are directed arcs that represent
/// mechanical connections, weld joints, or process connections.
///
/// The graph supports:
///   - multiple "branches"  (e.g. several nozzles on one shell)
///   - special connections  (tube bundle ↔ tube sheets, floating head, bellows …)
///   - pipeline attachments at nozzles
///
/// Aggregate properties (TotalDryWeight, OverallCentreOfGravity) are
/// computed values that must be refreshed whenever the component list changes.
type VesselGraph = {

    // ── Identity ──────────────────────────────────────────────────────────────
    Id          : string
    Name        : string
    Description : string
    /// Primary calculation standard applied to this vessel,
    /// e.g. "EN 13445", "ASME VIII Div.1", "ASME VIII Div.2", "AD 2000", "PD 5500"
    Standard    : string
    Revision    : string
    Date        : System.DateTime

    // ── Design basis ──────────────────────────────────────────────────────────
    ConditionSet : DesignConditionSet

    // ── Graph topology ────────────────────────────────────────────────────────
    /// All components keyed by Component.Id.
    Components : Map<string, Component>
    /// Directed edges (connections / joints) between components.
    Edges      : GraphEdge list

    // ── Aggregate (computed) properties ───────────────────────────────────────
    /// Sum of Component.Weight over all components  [kg].
    TotalDryWeight          : float
    /// Weighted centroid of all components in global coordinates  [mm].
    OverallCentreOfGravity  : Point3D
}

// ─────────────────────────────────────────────────────────────────────────────
// GRAPH TRAVERSAL & QUERY HELPERS
// ─────────────────────────────────────────────────────────────────────────────

module VesselGraph =

    // ── Component queries ────────────────────────────────────────────────────

    /// Direct successors (children) of a component in the directed graph.
    let children (componentId: string) (graph: VesselGraph) : Component list =
        graph.Edges
        |> List.filter (fun e -> e.SourceComponentId = componentId)
        |> List.choose (fun e -> Map.tryFind e.TargetComponentId graph.Components)

    /// Direct predecessors (parents) of a component in the directed graph.
    let parents (componentId: string) (graph: VesselGraph) : Component list =
        graph.Edges
        |> List.filter (fun e -> e.TargetComponentId = componentId)
        |> List.choose (fun e -> Map.tryFind e.SourceComponentId graph.Components)

    /// All edges incident on a component (as source or target).
    let edgesOf (componentId: string) (graph: VesselGraph) : GraphEdge list =
        graph.Edges
        |> List.filter (fun e ->
            e.SourceComponentId = componentId || e.TargetComponentId = componentId)

    /// Root components: those with no incoming edges (entry points of the graph).
    /// Typically the main shell course or a top-level nozzle neck.
    let roots (graph: VesselGraph) : Component list =
        let hasIncoming id =
            graph.Edges |> List.exists (fun e -> e.TargetComponentId = id)
        graph.Components
        |> Map.toList
        |> List.map snd
        |> List.filter (fun c -> not (hasIncoming c.Id))

    /// Leaf components: those with no outgoing edges (terminal points of the graph).
    let leaves (graph: VesselGraph) : Component list =
        let hasOutgoing id =
            graph.Edges |> List.exists (fun e -> e.SourceComponentId = id)
        graph.Components
        |> Map.toList
        |> List.map snd
        |> List.filter (fun c -> not (hasOutgoing c.Id))

    // ── Design condition resolution ──────────────────────────────────────────

    /// Resolve the effective DesignCondition for a given component,
    /// applying any component-level ConditionOverride on top of the vessel-level values.
    let effectiveCondition
        (conditionId  : string)
        (component    : Component)
        (conditionSet : DesignConditionSet)
        : DesignCondition option =
        match DesignConditionSet.tryFind conditionId conditionSet with
        | None            -> None
        | Some baseCondition ->
            match component.ConditionOverrides
                  |> List.tryFind (fun o -> o.ConditionId = conditionId) with
            | None    -> Some baseCondition
            | Some ov ->
                Some { baseCondition with
                         Pi = ov.Pi |> Option.orElse baseCondition.Pi
                         Ti = ov.Ti |> Option.orElse baseCondition.Ti
                         Pe = ov.Pe |> Option.orElse baseCondition.Pe
                         Te = ov.Te |> Option.orElse baseCondition.Te }

    // ── Aggregate property computation ───────────────────────────────────────

    /// Recompute TotalDryWeight and OverallCentreOfGravity from the current
    /// component map.  Returns an updated VesselGraph record.
    let recomputeAggregates (graph: VesselGraph) : VesselGraph =
        let components = graph.Components |> Map.toList |> List.map snd
        let totalWeight =
            components |> List.sumBy (fun c -> c.Weight)
        let cog =
            if totalWeight < 1e-9 then
                Point3D.origin
            else
                let sumX = components |> List.sumBy (fun c -> c.Weight * c.CentreOfGravity.X)
                let sumY = components |> List.sumBy (fun c -> c.Weight * c.CentreOfGravity.Y)
                let sumZ = components |> List.sumBy (fun c -> c.Weight * c.CentreOfGravity.Z)
                { X = sumX / totalWeight
                  Y = sumY / totalWeight
                  Z = sumZ / totalWeight }
        { graph with
            TotalDryWeight         = totalWeight
            OverallCentreOfGravity = cog }

    // ── Graph manipulation ───────────────────────────────────────────────────

    /// Add a component to the graph and recompute aggregates.
    let addComponent (component: Component) (graph: VesselGraph) : VesselGraph =
        { graph with Components = Map.add component.Id component graph.Components }
        |> recomputeAggregates

    /// Add a directed edge to the graph.
    let addEdge (edge: GraphEdge) (graph: VesselGraph) : VesselGraph =
        { graph with Edges = graph.Edges @ [ edge ] }

    /// Remove a component by ID and all incident edges.
    let removeComponent (componentId: string) (graph: VesselGraph) : VesselGraph =
        { graph with
            Components = Map.remove componentId graph.Components
            Edges      = graph.Edges
                         |> List.filter (fun e ->
                             e.SourceComponentId <> componentId &&
                             e.TargetComponentId <> componentId) }
        |> recomputeAggregates

    // ── Weld queries ─────────────────────────────────────────────────────────

    /// Collect all weld joint data present in graph edges (welded connections).
    let allEdgeWelds (graph: VesselGraph) : (GraphEdge * WeldStandardData) list =
        graph.Edges
        |> List.choose (fun e ->
            e.WeldData |> Option.map (fun w -> e, w))

    /// Collect all weld lines from all components, tagged with the component.
    let allComponentWelds (graph: VesselGraph)
        : (Component * WeldLine) list =
        graph.Components
        |> Map.toList
        |> List.map snd
        |> List.collect (fun c ->
            [ yield! c.MeridionalWelds      |> List.map (fun w -> c, w)
              yield! c.CircumferentialWelds  |> List.map (fun w -> c, w) ])
