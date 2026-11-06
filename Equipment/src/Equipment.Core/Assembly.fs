namespace Equipment.Core

open System.Collections.Generic
open ROP

/// <summary>Node of the spanning tree used to explore/draw the equipment.</summary>
type TreeNode =
    { Component: Component
      /// World frame of the component.
      Frame: Frame
      /// The connection to the parent (None for an anchor).
      Via: Connection option
      Children: TreeNode list
      /// Loop-closing connections that start at this component (they lead to an already-visited node).
      ClosingEdges: Connection list }

/// <summary>Whole-graph queries: validation, spanning tree, loops, paths.</summary>
module Assembly =

    /// Resolves positions and reports problems: Failure if some component cannot be placed, warnings for loops that do not close.
    let validate (eq: Equipment) : Returns<Placement, Issue> =
        let p = Equipment.resolve eq
        match p.Unplaced with
        | [] -> Returns.warnmany (Equipment.closureIssues eq p) p
        | ids -> Returns.failmany (ids |> List.map (fun id -> NotPlaced (Equipment.tagOf id eq)))

    /// Spanning forest (one tree per anchor) with the loop-closing edges attached to their A-side component.
    let tree (eq: Equipment) (p: Placement) : TreeNode list =
        let visited = HashSet<ComponentId>()
        let closingOf id =
            p.ClosingEdges
            |> List.choose (fun cid -> Equipment.tryConnection cid eq)
            |> List.filter (fun c -> c.A.Owner = id)
        let rec build (id: ComponentId) (via: Connection option) : TreeNode option =
            match Equipment.tryComponent id eq, Map.tryFind id p.Frames with
            | Some comp, Some frame when visited.Add id ->
                let children =
                    Equipment.connectionsOf id eq
                    |> List.filter (fun c -> p.TreeEdges.Contains c.Id && (match via with Some v -> v.Id <> c.Id | None -> true))
                    |> List.choose (fun c ->
                        let other = if c.A.Owner = id then c.B.Owner else c.A.Owner
                        build other (Some c))
                Some { Component = comp; Frame = frame; Via = via; Children = children; ClosingEdges = closingOf id }
            | _ -> None
        eq.Components
        |> List.filter (fun c -> eq.Anchors.ContainsKey c.Id)
        |> List.choose (fun c -> build c.Id None)

    /// Depth-first sequence of all nodes of a tree.
    let rec flatten (n: TreeNode) : TreeNode seq =
        seq {
            yield n
            for c in n.Children do
                yield! flatten c
        }

    /// Number of independent loops of the graph: E - V + (connected components). 0 = pure tree, 1 = e.g. one U-tube bundle, …
    let cycleRank (eq: Equipment) : int =
        let parent = Dictionary<ComponentId, ComponentId>()
        let rec find x =
            let p = parent[x]
            if p = x then x
            else
                let r = find p
                parent[x] <- r
                r
        for c in eq.Components do parent[c.Id] <- c.Id
        let mutable comps = eq.Components.Length
        for conn in eq.Connections do
            let ra, rb = find conn.A.Owner, find conn.B.Owner
            if ra <> rb then
                parent[ra] <- rb
                comps <- comps - 1
        eq.Connections.Length - eq.Components.Length + comps

    /// Shortest chain of components between two components (breadth-first), inclusive.
    let path (a: ComponentId) (b: ComponentId) (eq: Equipment) : ComponentId list option =
        let prev = Dictionary<ComponentId, ComponentId>()
        let q = Queue<ComponentId>()
        prev[a] <- a
        q.Enqueue a
        while q.Count > 0 && not (prev.ContainsKey b) do
            let u = q.Dequeue()
            for v in Equipment.neighbours u eq do
                if not (prev.ContainsKey v) then
                    prev[v] <- u
                    q.Enqueue v
        if not (prev.ContainsKey b) then None
        else
            let rec back x acc = if x = a then x :: acc else back prev[x] (x :: acc)
            Some (back b [])

    /// Components reachable from <paramref name="id"/> going away from <paramref name="parent"/> in the spanning tree.
    let descendants (id: ComponentId) (eq: Equipment) (p: Placement) : ComponentId list =
        tree eq p
        |> Seq.collect flatten
        |> Seq.tryFind (fun n -> n.Component.Id = id)
        |> Option.map (fun n -> flatten n |> Seq.map (fun x -> x.Component.Id) |> List.ofSeq)
        |> Option.defaultValue []
