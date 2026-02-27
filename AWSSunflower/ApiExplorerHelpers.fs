namespace CounterApp

open TSWApi

module ApiExplorerHelpers =

    let stripRootPrefix (path: string) =
        if not (isNull path) && path.StartsWith("Root/") then path.Substring(5) else path

    /// Guard against CLR null strings from JSON deserialization
    let nullSafe (s: string) = if isNull s then "" else s

    let effectiveName (n: TSWApi.Types.Node) =
        if not (System.String.IsNullOrEmpty n.NodeName) then n.NodeName
        elif not (System.String.IsNullOrEmpty n.Name) then n.Name
        else ""

    /// Recursively map an API Node to a TreeNodeState, preserving nested children.
    let rec mapNodeToTreeState (parentPath: string) (n: TSWApi.Types.Node) : TreeNodeState =
        let name = effectiveName n
        let path =
            if not (System.String.IsNullOrEmpty n.NodePath) then stripRootPrefix n.NodePath
            else if parentPath = "" then name
            else parentPath + "/" + name
        let children =
            match n.Nodes with
            | Some nodes when nodes.Length > 0 ->
                Some (nodes |> List.map (mapNodeToTreeState path))
            | Some _ -> Some []
            | None -> None
        { Path = path; Name = name; IsExpanded = false
          Children = children; Endpoints = n.Endpoints }

    let endpointKey nodePath endpointName = sprintf "%s.%s" nodePath endpointName

    let getLocoBindings (config: BindingsConfig) (locoName: string) =
        config.Locos
        |> List.tryFind (fun l -> l.LocoName = locoName)
        |> Option.map (fun l -> l.BoundEndpoints)
        |> Option.defaultValue []

    let isSerialConnected (model: ApiExplorer.Model) =
        match model.SerialConnectionState with ConnectionState.Connected _ -> true | _ -> false

    let rec updateTreeNode path updater (nodes: TreeNodeState list) =
        nodes |> List.map (fun (n: TreeNodeState) ->
            if n.Path = path then updater n
            else
                match n.Children with
                | Some kids -> { n with Children = Some (updateTreeNode path updater kids) }
                | None -> n)

    let rec findNode path (nodes: TreeNodeState list) =
        nodes |> List.tryPick (fun n ->
            if n.Path = path then Some n
            else match n.Children with Some kids -> findNode path kids | None -> None)

    let rec filterTree (query: string) (nodes: TreeNodeState list) : TreeNodeState list =
        if System.String.IsNullOrWhiteSpace(query) then
            nodes
        else
            let lowerQuery = query.ToLowerInvariant()
            nodes |> List.choose (fun node ->
                let nameMatch = node.Name.ToLowerInvariant().Contains(lowerQuery)
                let filteredChildren =
                    match node.Children with
                    | Some children -> filterTree query children
                    | None -> []
                if nameMatch || filteredChildren.Length > 0 then
                    let updatedChildren =
                        match node.Children with
                        | Some _ when filteredChildren.Length > 0 -> Some filteredChildren
                        | _ -> node.Children
                    Some { node with Children = updatedChildren }
                else
                    None
            )
