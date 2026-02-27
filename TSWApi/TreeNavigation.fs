namespace TSWApi

open TSWApi.Types

/// Tree navigation helpers for the TSW6 node hierarchy.
module TreeNavigation =

    /// Split a slash-separated node path into segments.
    let parseNodePath (path: string) : string list =
        if System.String.IsNullOrEmpty(path) then []
        else path.Split('/') |> Array.toList

    /// Join path segments with slashes.
    let buildNodePath (segments: string list) : string =
        System.String.Join("/", segments)

    /// Check if a node matches a name, falling back to Name when NodeName is empty.
    let private nameMatches (name: string) (n: Node) =
        n.NodeName = name || (System.String.IsNullOrEmpty(n.NodeName) && n.Name = name)

    /// Navigate a node tree to find a node at the given path segments.
    let rec getNodeAtPath (nodes: Node list) (path: string list) : Node option =
        match path with
        | [] -> None
        | [ name ] ->
            nodes |> List.tryFind (nameMatches name)
        | name :: rest ->
            nodes
            |> List.tryFind (nameMatches name)
            |> Option.bind (fun n ->
                match n.Nodes with
                | Some children -> getNodeAtPath children rest
                | None -> None)

    /// Find an endpoint by name on a node.
    let findEndpoint (node: Node) (endpointName: string) : Endpoint option =
        match node.Endpoints with
        | Some endpoints -> endpoints |> List.tryFind (fun e -> e.Name = endpointName)
        | None -> None

    /// Get the child nodes of a node (empty list if leaf).
    let getChildNodes (node: Node) : Node list =
        match node.Nodes with
        | Some children -> children
        | None -> []
