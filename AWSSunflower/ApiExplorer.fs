namespace CounterApp

open System
open System.Net.Http
open Avalonia
open Avalonia.Controls
open Avalonia.FuncUI
open Avalonia.FuncUI.Types
open Avalonia.FuncUI.DSL
open Avalonia.Layout
open Avalonia.Media
open TSWApi
open TSWApi.Types

module ApiExplorer =

    /// Create HttpClient (shared instance)
    let private httpClient = new HttpClient()

    /// API Explorer component
    let view (addToast: string -> bool -> unit) =
        Component(fun ctx ->
            // State management
            let baseUrl = ctx.useState "http://localhost:31270"
            let commKey = ctx.useState ""
            let apiConfig = ctx.useState<ApiConfig option> None
            let connectionState = ctx.useState ApiConnectionState.Disconnected
            let isConnecting = ctx.useState false
            let treeRoot = ctx.useState<TreeNodeState list> []
            let selectedNode = ctx.useState<TreeNodeState option> None
            let endpointValues = ctx.useState<Map<string, string>> Map.empty
            let lastResponseTime = ctx.useState<TimeSpan option> None
            
            /// Connect to the API
            let connect () =
                isConnecting.Set true
                connectionState.Set ApiConnectionState.Connecting
                async {
                    let startTime = DateTime.Now
                    
                    // First try to discover CommKey if not provided
                    let! keyResult =
                        if String.IsNullOrWhiteSpace(commKey.Current) then
                            async {
                                let myGamesPath = 
                                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                                    |> fun docs -> IO.Path.Combine(docs, "My Games")
                                let result = TSWApi.Http.discoverCommKey myGamesPath
                                match result with
                                | Ok key -> 
                                    commKey.Set (CommKey.value key)
                                    return Ok key
                                | Error (AuthError msg) ->
                                    return Error msg
                                | Error (NetworkError ex) ->
                                    return Error $"Network error: {ex.Message}"
                                | Error (HttpError (status, body)) ->
                                    return Error $"HTTP {status}: {body}"
                                | Error (ParseError msg) ->
                                    return Error $"Parse error: {msg}"
                                | Error (ConfigError msg) ->
                                    return Error $"Config error: {msg}"
                            }
                        else
                            async { 
                                match CommKey.create commKey.Current with
                                | Ok key -> return Ok key
                                | Error (AuthError msg) -> return Error msg
                                | Error err -> return Error "Invalid CommKey format"
                            }
                    
                    match keyResult with
                    | Error msg ->
                        connectionState.Set (ApiConnectionState.Error msg)
                        isConnecting.Set false
                        addToast msg true
                    | Ok key ->
                        // Create config and try getInfo
                        match TSWApi.Http.createConfigWithUrl baseUrl.Current (CommKey.value key) with
                        | Error err ->
                            connectionState.Set (ApiConnectionState.Error "Invalid configuration")
                            isConnecting.Set false
                            addToast "Invalid configuration" true
                        | Ok config ->
                            let! infoResult = TSWApi.ApiClient.getInfo httpClient config
                            
                            let elapsed = DateTime.Now - startTime
                            lastResponseTime.Set (Some elapsed)
                            
                            match infoResult with
                            | Ok info ->
                                apiConfig.Set (Some config)
                                connectionState.Set (ApiConnectionState.Connected info)
                                isConnecting.Set false
                                addToast $"Connected to {info.Meta.GameName}" false
                                
                                // Load root nodes
                                let! listResult = TSWApi.ApiClient.listNodes httpClient config None
                                match listResult with
                                | Ok listResp ->
                                    let rootNodes =
                                        listResp.Nodes
                                        |> Option.defaultValue []
                                        |> List.map (fun n ->
                                            { Path = n.NodePath
                                              Name = n.NodeName
                                              IsExpanded = false
                                              Children = None
                                              Endpoints = n.Endpoints })
                                    treeRoot.Set rootNodes
                                | Error _ ->
                                    addToast "Failed to load root nodes" true
                            | Error err ->
                                let msg =
                                    match err with
                                    | NetworkError ex -> $"Network error: {ex.Message}"
                                    | HttpError (status, body) -> $"HTTP {status}: {body}"
                                    | AuthError msg -> $"Auth error: {msg}"
                                    | ParseError msg -> $"Parse error: {msg}"
                                    | ConfigError msg -> $"Config error: {msg}"
                                connectionState.Set (ApiConnectionState.Error msg)
                                isConnecting.Set false
                                addToast msg true
                } |> Async.StartImmediate
            
            /// Disconnect from the API
            let disconnect () =
                apiConfig.Set None
                connectionState.Set ApiConnectionState.Disconnected
                treeRoot.Set []
                selectedNode.Set None
                endpointValues.Set Map.empty
                lastResponseTime.Set None
                addToast "Disconnected" false
            
            /// Expand a tree node (lazy load children)
            let rec expandNode (nodePath: string) =
                match connectionState.Current, apiConfig.Current with
                | ApiConnectionState.Connected _, Some config ->
                    async {
                        let startTime = DateTime.Now
                        let! listResult = TSWApi.ApiClient.listNodes httpClient config (Some nodePath)
                        let elapsed = DateTime.Now - startTime
                        lastResponseTime.Set (Some elapsed)
                        
                        match listResult with
                        | Ok listResp ->
                            let children =
                                listResp.Nodes
                                |> Option.defaultValue []
                                |> List.map (fun n ->
                                    { Path = n.NodePath
                                      Name = n.NodeName
                                      IsExpanded = false
                                      Children = None
                                      Endpoints = n.Endpoints })
                            
                            // Update tree by finding and updating the node
                            let rec updateTree (nodes: TreeNodeState list) =
                                nodes |> List.map (fun n ->
                                    if n.Path = nodePath then
                                        { n with IsExpanded = true; Children = Some children }
                                    else
                                        match n.Children with
                                        | Some kids -> { n with Children = Some (updateTree kids) }
                                        | None -> n
                                )
                            
                            treeRoot.Set (updateTree treeRoot.Current)
                        | Error err ->
                            let msg =
                                match err with
                                | NetworkError ex -> $"Network error: {ex.Message}"
                                | HttpError (status, body) -> $"HTTP {status}: {body}"
                                | AuthError msg -> $"Auth error: {msg}"
                                | ParseError msg -> $"Parse error: {msg}"
                                | ConfigError msg -> $"Config error: {msg}"
                            addToast msg true
                    } |> Async.StartImmediate
                | _ -> ()
            
            /// Collapse a tree node
            let collapseNode (nodePath: string) =
                let rec updateTree (nodes: TreeNodeState list) =
                    nodes |> List.map (fun n ->
                        if n.Path = nodePath then
                            { n with IsExpanded = false }
                        else
                            match n.Children with
                            | Some kids -> { n with Children = Some (updateTree kids) }
                            | None -> n
                    )
                treeRoot.Set (updateTree treeRoot.Current)
            
            /// Select a node and load its endpoint values
            let selectNode (node: TreeNodeState) =
                selectedNode.Set (Some node)
                
                // If node has endpoints, we can fetch values
                match node.Endpoints with
                | Some endpoints when endpoints.Length > 0 ->
                    // Clear previous values
                    endpointValues.Set Map.empty
                | _ -> ()
            
            /// Get value for an endpoint
            let getEndpointValue (endpointPath: string) =
                match connectionState.Current, apiConfig.Current with
                | ApiConnectionState.Connected _, Some config ->
                    async {
                        let startTime = DateTime.Now
                        let! getResult = TSWApi.ApiClient.getValue httpClient config endpointPath
                        let elapsed = DateTime.Now - startTime
                        lastResponseTime.Set (Some elapsed)
                        
                        match getResult with
                        | Ok getResp ->
                            // Update the values map - store using the endpoint path as key
                            let valueStr =
                                getResp.Values
                                |> Seq.map (fun kvp -> $"{kvp.Key}: {kvp.Value}")
                                |> String.concat ", "
                            let newValues = Map.add endpointPath valueStr endpointValues.Current
                            endpointValues.Set newValues
                        | Error err ->
                            let msg =
                                match err with
                                | NetworkError ex -> $"Network error: {ex.Message}"
                                | HttpError (status, body) -> $"HTTP {status}: {body}"
                                | AuthError msg -> $"Auth error: {msg}"
                                | ParseError msg -> $"Parse error: {msg}"
                                | ConfigError msg -> $"Config error: {msg}"
                            addToast msg true
                    } |> Async.StartImmediate
                | _ -> ()
            
            /// Render a tree node recursively
            let rec renderTreeNode (node: TreeNodeState) : IView =
                StackPanel.create [
                    StackPanel.orientation Orientation.Vertical
                    StackPanel.children [
                        // Node header (clickable)
                        let arrow = if node.IsExpanded then "▼" else "▶"
                        Button.create [
                            Button.content $"{arrow} {node.Name}"
                            Button.onClick (fun _ ->
                                if node.IsExpanded then
                                    collapseNode node.Path
                                else
                                    // Load children if not loaded yet
                                    if node.Children.IsNone then
                                        expandNode node.Path
                                    else
                                        let rec updateTree (nodes: TreeNodeState list) =
                                            nodes |> List.map (fun n ->
                                                if n.Path = node.Path then
                                                    { n with IsExpanded = true }
                                                else
                                                    match n.Children with
                                                    | Some kids -> { n with Children = Some (updateTree kids) }
                                                    | None -> n
                                            )
                                        treeRoot.Set (updateTree treeRoot.Current)
                                selectNode node
                            )
                            Button.horizontalAlignment HorizontalAlignment.Stretch
                            Button.horizontalContentAlignment HorizontalAlignment.Left
                            Button.padding (5.0, 3.0)
                            Button.fontSize 12.0
                        ]
                        
                        // Children (if expanded)
                        if node.IsExpanded then
                            match node.Children with
                            | Some children when children.Length > 0 ->
                                StackPanel.create [
                                    StackPanel.orientation Orientation.Vertical
                                    StackPanel.margin (20.0, 0.0, 0.0, 0.0)
                                    StackPanel.children (
                                        children |> List.map renderTreeNode
                                    )
                                ]
                            | _ -> ()
                    ]
                ] :> IView
            
            /// Connection panel
            let connectionPanel =
                StackPanel.create [
                    StackPanel.dock Dock.Top
                    StackPanel.orientation Orientation.Vertical
                    StackPanel.margin 10.0
                    StackPanel.spacing 5.0
                    StackPanel.children [
                        TextBlock.create [
                            TextBlock.text "Base URL:"
                            TextBlock.fontSize 12.0
                        ]
                        TextBox.create [
                            TextBox.text baseUrl.Current
                            TextBox.onTextChanged (fun txt -> baseUrl.Set txt)
                            TextBox.isEnabled (match connectionState.Current with ApiConnectionState.Disconnected -> true | _ -> false)
                        ]
                        
                        TextBlock.create [
                            TextBlock.text "CommKey (optional - will auto-discover):"
                            TextBlock.fontSize 12.0
                        ]
                        TextBox.create [
                            TextBox.text commKey.Current
                            TextBox.onTextChanged (fun txt -> commKey.Set txt)
                            TextBox.isEnabled (match connectionState.Current with ApiConnectionState.Disconnected -> true | _ -> false)
                        ]
                        
                        Button.create [
                            Button.content (
                                match connectionState.Current with
                                | ApiConnectionState.Disconnected -> "Connect"
                                | ApiConnectionState.Connecting -> "Connecting..."
                                | ApiConnectionState.Connected _ -> "Disconnect"
                                | ApiConnectionState.Error _ -> "Retry"
                            )
                            Button.onClick (fun _ ->
                                match connectionState.Current with
                                | ApiConnectionState.Disconnected | ApiConnectionState.Error _ -> connect ()
                                | ApiConnectionState.Connected _ -> disconnect ()
                                | _ -> ()
                            )
                            Button.isEnabled (not isConnecting.Current)
                            Button.horizontalAlignment HorizontalAlignment.Stretch
                        ]
                    ]
                ]
            
            /// Status bar
            let statusBar =
                Border.create [
                    Border.dock Dock.Bottom
                    Border.background (SolidColorBrush(Color.Parse("#2A2A2A")))
                    Border.padding 10.0
                    Border.child (
                        StackPanel.create [
                            StackPanel.orientation Orientation.Horizontal
                            StackPanel.spacing 20.0
                            StackPanel.children [
                                TextBlock.create [
                                    TextBlock.text (
                                        match connectionState.Current with
                                        | ApiConnectionState.Disconnected -> "Status: Disconnected"
                                        | ApiConnectionState.Connecting -> "Status: Connecting..."
                                        | ApiConnectionState.Connected info -> $"Status: Connected to {info.Meta.GameName} (Build {info.Meta.GameBuildNumber})"
                                        | ApiConnectionState.Error msg -> $"Status: Error - {msg}"
                                    )
                                    TextBlock.fontSize 11.0
                                    TextBlock.foreground (
                                        match connectionState.Current with
                                        | ApiConnectionState.Connected _ -> SolidColorBrush(Color.Parse("#00AA00"))
                                        | ApiConnectionState.Error _ -> SolidColorBrush(Color.Parse("#FF5555"))
                                        | _ -> SolidColorBrush Colors.White
                                    )
                                ]
                                
                                match lastResponseTime.Current with
                                | Some time ->
                                    TextBlock.create [
                                        TextBlock.text $"Last response: {time.TotalMilliseconds:F0}ms"
                                        TextBlock.fontSize 11.0
                                    ]
                                | None -> ()
                            ]
                        ]
                    )
                ]
            
            /// Tree browser panel
            let treeBrowserPanel =
                Border.create [
                    Border.dock Dock.Left
                    Border.width 300.0
                    Border.borderBrush (SolidColorBrush(Color.Parse("#3A3A3A")))
                    Border.borderThickness (0.0, 0.0, 1.0, 0.0)
                    Border.child (
                        ScrollViewer.create [
                            ScrollViewer.content (
                                StackPanel.create [
                                    StackPanel.orientation Orientation.Vertical
                                    StackPanel.children (
                                        treeRoot.Current |> List.map renderTreeNode
                                    )
                                ]
                            )
                        ]
                    )
                ]
            
            /// Endpoint viewer panel
            let endpointViewerPanel =
                ScrollViewer.create [
                    ScrollViewer.content (
                        StackPanel.create [
                            StackPanel.orientation Orientation.Vertical
                            StackPanel.margin 10.0
                            StackPanel.spacing 10.0
                            StackPanel.children (
                                match selectedNode.Current with
                                | None ->
                                    [
                                        TextBlock.create [
                                            TextBlock.text "Select a node to view endpoints"
                                            TextBlock.fontSize 14.0
                                            TextBlock.foreground (SolidColorBrush Colors.Gray)
                                        ]
                                    ]
                                | Some node ->
                                    match node.Endpoints with
                                    | Some endpoints when endpoints.Length > 0 ->
                                        [
                                            TextBlock.create [
                                                TextBlock.text $"Node: {node.Name}"
                                                TextBlock.fontSize 16.0
                                                TextBlock.fontWeight FontWeight.Bold
                                            ]
                                            TextBlock.create [
                                                TextBlock.text $"Path: {node.Path}"
                                                TextBlock.fontSize 11.0
                                                TextBlock.foreground (SolidColorBrush Colors.Gray)
                                            ]
                                            TextBlock.create [
                                                TextBlock.text "Endpoints:"
                                                TextBlock.fontSize 14.0
                                                TextBlock.fontWeight FontWeight.Bold
                                                TextBlock.margin (0.0, 10.0, 0.0, 5.0)
                                            ]
                                            
                                            yield! endpoints |> List.map (fun ep ->
                                                StackPanel.create [
                                                    StackPanel.orientation Orientation.Vertical
                                                    StackPanel.margin (0.0, 5.0, 0.0, 5.0)
                                                    StackPanel.children [
                                                        StackPanel.create [
                                                            StackPanel.orientation Orientation.Horizontal
                                                            StackPanel.spacing 10.0
                                                            StackPanel.children [
                                                                TextBlock.create [
                                                                    TextBlock.text ep.Name
                                                                    TextBlock.fontSize 12.0
                                                                    TextBlock.fontWeight FontWeight.SemiBold
                                                                ]
                                                                if ep.Writable then
                                                                    TextBlock.create [
                                                                        TextBlock.text "(writable)"
                                                                        TextBlock.fontSize 10.0
                                                                        TextBlock.foreground (SolidColorBrush(Color.Parse("#FFAA00")))
                                                                    ]
                                                                Button.create [
                                                                    Button.content "Get Value"
                                                                    Button.onClick (fun _ ->
                                                                        let fullPath = $"{node.Path}/{ep.Name}"
                                                                        getEndpointValue fullPath
                                                                    )
                                                                    Button.fontSize 10.0
                                                                    Button.padding (5.0, 2.0)
                                                                ]
                                                            ]
                                                        ]
                                                        
                                                        // Show value if fetched
                                                        let fullPathForLookup = $"{node.Path}/{ep.Name}"
                                                        match Map.tryFind fullPathForLookup endpointValues.Current with
                                                        | Some value ->
                                                            TextBox.create [
                                                                TextBox.text value
                                                                TextBox.isReadOnly true
                                                                TextBox.fontSize 11.0
                                                                TextBox.margin (0.0, 5.0, 0.0, 0.0)
                                                            ]
                                                        | None -> ()
                                                    ]
                                                ] :> IView
                                            )
                                        ]
                                    | _ ->
                                        [
                                            TextBlock.create [
                                                TextBlock.text $"Node: {node.Name}"
                                                TextBlock.fontSize 16.0
                                                TextBlock.fontWeight FontWeight.Bold
                                            ]
                                            TextBlock.create [
                                                TextBlock.text "No endpoints on this node"
                                                TextBlock.fontSize 12.0
                                                TextBlock.foreground (SolidColorBrush Colors.Gray)
                                            ]
                                        ]
                            )
                        ]
                    )
                ]
            
            // Main layout
            DockPanel.create [
                DockPanel.children [
                    statusBar
                    connectionPanel
                    treeBrowserPanel
                    endpointViewerPanel
                ]
            ]
        )
