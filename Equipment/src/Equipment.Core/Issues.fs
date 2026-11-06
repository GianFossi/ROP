namespace Equipment.Core

open System

/// <summary>Stable identity of a component (survives renames and edits; used by connections and persistence).</summary>
[<Struct>]
type ComponentId =
    | ComponentId of Guid
    static member New() = ComponentId (Guid.NewGuid())
    member x.Value = match x with ComponentId g -> g
    override x.ToString() = x.Value.ToString("N").Substring(0, 8)

/// <summary>Stable identity of a connection between two ports.</summary>
[<Struct>]
type ConnectionId =
    | ConnectionId of Guid
    static member New() = ConnectionId (Guid.NewGuid())
    member x.Value = match x with ConnectionId g -> g
    override x.ToString() = x.Value.ToString("N").Substring(0, 8)

/// <summary>
/// Everything the model can report. Used as the message type of <c>Returns&lt;_, Issue&gt;</c>:
/// errors in <c>Failure</c>, non-blocking observations as warnings in <c>Success</c>.
/// </summary>
type Issue =
    | InvalidParameter of subject: string * detail: string
    | DuplicateTag of tag: string
    | ComponentNotFound of ComponentId
    | ComponentTagNotFound of tag: string
    | ConnectionNotFound of ConnectionId
    | PortNotFound of tag: string * port: string
    | PortCapacityExceeded of tag: string * port: string
    | DuplicateConnection of tagA: string * portA: string * tagB: string * portB: string
    | SelfConnection of tag: string * port: string
    | NotPlaced of tag: string
    | ClosingMismatch of connection: ConnectionId * description: string * distanceMm: float * angleDeg: float
    | Reanchored of tag: string
    | NotSupplied of tag: string
    | Unconnected of tag: string

module Issue =
    let describe (i: Issue) : string =
        match i with
        | InvalidParameter (s, d) -> sprintf "Invalid parameter '%s': %s" s d
        | DuplicateTag t -> sprintf "Tag '%s' is already used by another component." t
        | ComponentNotFound id -> sprintf "Component %O not found." id
        | ComponentTagNotFound t -> sprintf "Component '%s' not found." t
        | ConnectionNotFound id -> sprintf "Connection %O not found." id
        | PortNotFound (t, p) -> sprintf "Component '%s' has no port '%s'." t p
        | PortCapacityExceeded (t, p) -> sprintf "Port '%s' of '%s' cannot accept more connections." p t
        | DuplicateConnection (a, pa, b, pb) -> sprintf "'%s'.%s is already connected to '%s'.%s." a pa b pb
        | SelfConnection (t, p) -> sprintf "A port cannot be connected to a port of the same component ('%s'.%s)." t p
        | NotPlaced t -> sprintf "Component '%s' is not connected to any anchored component: its position is undefined." t
        | ClosingMismatch (_, d, dist, ang) ->
            sprintf "Closing connection %s does not close geometrically: gap %.3f mm, misalignment %.4f°." d dist ang
        | Reanchored t -> sprintf "Component '%s' became the anchor of a detached branch (kept at its current position)." t
        | NotSupplied t -> sprintf "Component '%s' is not part of the supply (excluded from weight and envelope)." t
        | Unconnected t -> sprintf "Component '%s' has no connections." t

    let isGeometric = function ClosingMismatch _ -> true | _ -> false
