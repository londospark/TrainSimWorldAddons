namespace CounterApp

open System
open System.Net.Http
open Avalonia.Controls
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Types
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Threading
open TSWApi
open TSWApi.Types

module ApiExplorer =

    // ─── Minimal Elmish infrastructure ───

    type Dispatch<'msg> = 'msg -> unit
    type Sub<'msg> = Dispatch<'msg> -> unit
    type Cmd<'msg> = Sub<'msg> list

    module Cmd =
        let none : Cmd<'msg> = []
        let batch (cmds: Cmd<'msg> list) : Cmd<'msg> = List.concat cmds

        module OfAsync =
            let either (task: 'a -> Async<'b>) (arg: 'a) (ofSuccess: 'b -> 'msg) (ofError: exn -> 'msg) : Cmd<'msg> =
                [ fun dispatch ->
                    async {
                        try
                            let! result = task arg
                            dispatch (ofSuccess result)
                        with ex ->
                            dispatch (ofError ex)
                    } |> Async.Start ]

    // ─── Shared HttpClient ───

    let private httpClient = new HttpClient()

    // ─── Model ───

    type Model =
        { BaseUrl: string
          CommKey: string
          ApiConfig: ApiConfig option
          ConnectionState: ApiConnectionState
          IsConnecting: bool
          TreeRoot: TreeNodeState list
          SelectedNode: TreeNodeState option
          EndpointValues: Map<string, string>
          LastResponseTime: TimeSpan option
          SearchQuery: string }

    // ─── Messages ───

    type Msg =
        | SetBaseUrl of string
        | SetCommKey of string
        | SetSearchQuery of string
        | Connect
        | Disconnect
        | ConnectSuccess of commKey: string * config: ApiConfig * info: InfoResponse * elapsed: TimeSpan
        | ConnectError of string
        | RootNodesLoaded of nodes: TreeNodeState list * elapsed: TimeSpan
        | RootNodesError of string
        | ExpandNode of path: string
        | NodeExpanded of path: string * children: TreeNodeState list * endpoints: Endpoint list option * elapsed: TimeSpan
        | CollapseNode of path: string
        | ToggleExpand of path: string
        | SelectNode of TreeNodeState
        | GetEndpointValue of path: string
        | EndpointValueReceived of path: string * value: string * elapsed: TimeSpan
        | ApiError of string

    // ─── Init ───

    let init () =
        { BaseUrl = "http://localhost:31270"
          CommKey = ""
          ApiConfig = None
          ConnectionState = ApiConnectionState.Disconnected
          IsConnecting = false
          TreeRoot = []
          SelectedNode = None
          EndpointValues = Map.empty
          LastResponseTime = None
          SearchQuery = "" }

    let private stripRootPrefix (path: string) =
        if not (isNull path) && path.StartsWith("Root/") then path.Substring(5) else path

    let private effectiveName (n: TSWApi.Types.Node) =
        if not (System.String.IsNullOrEmpty n.NodeName) then n.NodeName
        elif not (System.String.IsNullOrEmpty n.Name) then n.Name
        else ""

    // ─── Async commands ───

    let private connectCmd (baseUrl: string) (commKey: string) =
        Cmd.OfAsync.either
            (fun () ->
                async {
                    let startTime = DateTime.Now
                    let keyValue =
                        if String.IsNullOrWhiteSpace(commKey) then
                            let myGamesPath =
                                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                                |> fun docs -> IO.Path.Combine(docs, "My Games")
                            match TSWApi.Http.discoverCommKey myGamesPath with
                            | Ok key -> CommKey.value key
                            | Error err -> failwithf "CommKey discovery failed: %A" err
                        else commKey
                    let config =
                        match TSWApi.Http.createConfigWithUrl baseUrl keyValue with
                        | Ok c -> c
                        | Error err -> failwithf "Invalid configuration: %A" err
                    let! infoResult = TSWApi.ApiClient.getInfo httpClient config
                    let elapsed = DateTime.Now - startTime
                    match infoResult with
                    | Ok info -> return (keyValue, config, info, elapsed)
                    | Error err -> return failwithf "API error: %A" err
                })
            ()
            (fun (key, config, info, elapsed) -> ConnectSuccess(key, config, info, elapsed))
            (fun ex -> ConnectError ex.Message)

    let private loadRootNodesCmd (config: ApiConfig) =
        Cmd.OfAsync.either
            (fun () ->
                async {
                    let startTime = DateTime.Now
                    let! listResult = TSWApi.ApiClient.listNodes httpClient config None
                    let elapsed = DateTime.Now - startTime
                    match listResult with
                    | Ok listResp ->
                        let nodes =
                            listResp.Nodes
                            |> Option.defaultValue []
                            |> List.map (fun n ->
                                { Path = stripRootPrefix n.NodePath; Name = effectiveName n; IsExpanded = false
                                  Children = None; Endpoints = n.Endpoints })
                        return (nodes, elapsed)
                    | Error err -> return failwithf "List failed: %A" err
                })
            ()
            (fun (nodes, elapsed) -> RootNodesLoaded(nodes, elapsed))
            (fun ex -> RootNodesError ex.Message)

    let private expandNodeCmd (config: ApiConfig) (nodePath: string) =
        Cmd.OfAsync.either
            (fun () ->
                async {
                    let startTime = DateTime.Now
                    let! listResult = TSWApi.ApiClient.listNodes httpClient config (Some nodePath)
                    let elapsed = DateTime.Now - startTime
                    match listResult with
                    | Ok listResp ->
                        let children =
                            listResp.Nodes
                            |> Option.defaultValue []
                            |> List.map (fun n ->
                                let name = effectiveName n
                                let path = if not (System.String.IsNullOrEmpty n.NodePath) then stripRootPrefix n.NodePath
                                           else nodePath + "/" + name
                                { Path = path; Name = name; IsExpanded = false
                                  Children = None; Endpoints = n.Endpoints })
                        return (nodePath, children, listResp.Endpoints, elapsed)
                    | Error err -> return failwithf "Expand failed: %A" err
                })
            ()
            (fun (p, ch, eps, elapsed) -> NodeExpanded(p, ch, eps, elapsed))
            (fun ex -> ApiError ex.Message)

    let private getValueCmd (config: ApiConfig) (endpointPath: string) =
        Cmd.OfAsync.either
            (fun () ->
                async {
                    let startTime = DateTime.Now
                    let! getResult = TSWApi.ApiClient.getValue httpClient config endpointPath
                    let elapsed = DateTime.Now - startTime
                    match getResult with
                    | Ok getResp ->
                        let valueStr =
                            if isNull (getResp.Values :> obj) || getResp.Values.Count = 0 then
                                "(no values returned)"
                            else
                                getResp.Values
                                |> Seq.map (fun kvp -> sprintf "%s: %O" kvp.Key kvp.Value)
                                |> String.concat ", "
                        return (endpointPath, valueStr, elapsed)
                    | Error err -> return failwithf "Get failed: %A" err
                })
            ()
            (fun (p, v, elapsed) -> EndpointValueReceived(p, v, elapsed))
            (fun ex -> ApiError ex.Message)

    // ─── Tree helpers ───

    let rec private updateTreeNode path updater (nodes: TreeNodeState list) =
        nodes |> List.map (fun (n: TreeNodeState) ->
            if n.Path = path then updater n
            else
                match n.Children with
                | Some kids -> { n with Children = Some (updateTreeNode path updater kids) }
                | None -> n)

    let rec private findNode path (nodes: TreeNodeState list) =
        nodes |> List.tryPick (fun n ->
            if n.Path = path then Some n
            else match n.Children with Some kids -> findNode path kids | None -> None)

    // ─── Update (pure function) ───

    let rec update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
        match msg with
        | SetBaseUrl url -> { model with BaseUrl = url }, Cmd.none
        | SetCommKey key -> { model with CommKey = key }, Cmd.none
        | SetSearchQuery query -> { model with SearchQuery = query }, Cmd.none

        | Connect ->
            { model with IsConnecting = true; ConnectionState = ApiConnectionState.Connecting },
            connectCmd model.BaseUrl model.CommKey

        | ConnectSuccess (key, config, info, elapsed) ->
            { model with
                CommKey = key; ApiConfig = Some config
                ConnectionState = ApiConnectionState.Connected info
                IsConnecting = false; LastResponseTime = Some elapsed },
            loadRootNodesCmd config

        | ConnectError errorMsg ->
            { model with ConnectionState = ApiConnectionState.Error errorMsg; IsConnecting = false },
            Cmd.none

        | Disconnect ->
            { model with
                ApiConfig = None; ConnectionState = ApiConnectionState.Disconnected
                TreeRoot = []; SelectedNode = None
                EndpointValues = Map.empty; LastResponseTime = None },
            Cmd.none

        | RootNodesLoaded (nodes, elapsed) ->
            { model with TreeRoot = nodes; LastResponseTime = Some elapsed }, Cmd.none

        | RootNodesError errorMsg ->
            { model with ConnectionState = ApiConnectionState.Error errorMsg }, Cmd.none

        | ExpandNode path ->
            match model.ApiConfig with
            | Some config -> model, expandNodeCmd config path
            | None -> model, Cmd.none

        | NodeExpanded (path, children, endpoints, elapsed) ->
            { model with
                TreeRoot = updateTreeNode path (fun n -> 
                    { n with IsExpanded = true; Children = Some children; Endpoints = endpoints }) model.TreeRoot
                LastResponseTime = Some elapsed }, Cmd.none

        | CollapseNode path ->
            { model with
                TreeRoot = updateTreeNode path (fun n -> { n with IsExpanded = false }) model.TreeRoot },
            Cmd.none

        | ToggleExpand path ->
            match findNode path model.TreeRoot with
            | Some node when node.IsExpanded -> update (CollapseNode path) model
            | Some node when node.Children.IsNone -> update (ExpandNode path) model
            | Some _ ->
                { model with TreeRoot = updateTreeNode path (fun n -> { n with IsExpanded = true }) model.TreeRoot },
                Cmd.none
            | None -> model, Cmd.none

        | SelectNode node ->
            { model with SelectedNode = Some node; EndpointValues = Map.empty }, Cmd.none

        | GetEndpointValue path ->
            match model.ApiConfig with
            | Some config -> model, getValueCmd config path
            | None -> model, Cmd.none

        | EndpointValueReceived (path, value, elapsed) ->
            { model with
                EndpointValues = Map.add path value model.EndpointValues
                LastResponseTime = Some elapsed }, Cmd.none

        | ApiError errorMsg ->
            { model with ConnectionState = ApiConnectionState.Error errorMsg }, Cmd.none

    // ─── View ───

    let private connectionPanel (model: Model) (dispatch: Dispatch<Msg>) =
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
                    TextBox.text model.BaseUrl
                    TextBox.onTextChanged (SetBaseUrl >> dispatch)
                    TextBox.isEnabled (match model.ConnectionState with ApiConnectionState.Disconnected -> true | _ -> false)
                ]

                TextBlock.create [
                    TextBlock.text "CommKey (optional - will auto-discover):"
                    TextBlock.fontSize 12.0
                ]
                TextBox.create [
                    TextBox.text model.CommKey
                    TextBox.onTextChanged (SetCommKey >> dispatch)
                    TextBox.isEnabled (match model.ConnectionState with ApiConnectionState.Disconnected -> true | _ -> false)
                ]

                Button.create [
                    Button.content (
                        match model.ConnectionState with
                        | ApiConnectionState.Disconnected -> "Connect"
                        | ApiConnectionState.Connecting -> "Connecting..."
                        | ApiConnectionState.Connected _ -> "Disconnect"
                        | ApiConnectionState.Error _ -> "Retry"
                    )
                    Button.onClick (fun _ ->
                        match model.ConnectionState with
                        | ApiConnectionState.Disconnected | ApiConnectionState.Error _ -> dispatch Connect
                        | ApiConnectionState.Connected _ -> dispatch Disconnect
                        | _ -> ()
                    )
                    Button.isEnabled (not model.IsConnecting)
                    Button.horizontalAlignment HorizontalAlignment.Stretch
                ]
            ]
        ]

    let private statusBar (model: Model) =
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
                                match model.ConnectionState with
                                | ApiConnectionState.Disconnected -> "Status: Disconnected"
                                | ApiConnectionState.Connecting -> "Status: Connecting..."
                                | ApiConnectionState.Connected info -> sprintf "Status: Connected to %s (Build %d)" info.Meta.GameName info.Meta.GameBuildNumber
                                | ApiConnectionState.Error msg -> sprintf "Status: Error - %s" msg
                            )
                            TextBlock.fontSize 11.0
                            TextBlock.foreground (
                                match model.ConnectionState with
                                | ApiConnectionState.Connected _ -> SolidColorBrush(Color.Parse("#00AA00"))
                                | ApiConnectionState.Error _ -> SolidColorBrush(Color.Parse("#FF5555"))
                                | _ -> SolidColorBrush Colors.White
                            )
                        ]

                        match model.LastResponseTime with
                        | Some time ->
                            TextBlock.create [
                                TextBlock.text (sprintf "Last response: %.0fms" time.TotalMilliseconds)
                                TextBlock.fontSize 11.0
                            ]
                        | None -> ()
                    ]
                ]
            )
        ]

    let rec private renderTreeNode (dispatch: Dispatch<Msg>) (node: TreeNodeState) : IView =
        StackPanel.create [
            StackPanel.orientation Orientation.Vertical
            StackPanel.children [
                let arrow = if node.IsExpanded then "▼" else "▶"
                Button.create [
                    Button.content (sprintf "%s %s" arrow node.Name)
                    Button.onClick (fun _ ->
                        dispatch (ToggleExpand node.Path)
                        dispatch (SelectNode node)
                    )
                    Button.horizontalAlignment HorizontalAlignment.Stretch
                    Button.horizontalContentAlignment HorizontalAlignment.Left
                    Button.padding (5.0, 3.0)
                    Button.fontSize 12.0
                ]

                if node.IsExpanded then
                    match node.Children with
                    | Some children when children.Length > 0 ->
                        StackPanel.create [
                            StackPanel.orientation Orientation.Vertical
                            StackPanel.margin (20.0, 0.0, 0.0, 0.0)
                            StackPanel.children (children |> List.map (renderTreeNode dispatch))
                        ]
                    | _ -> ()
            ]
        ] :> IView

    let rec private filterTree (query: string) (nodes: TreeNodeState list) : TreeNodeState list =
        if String.IsNullOrWhiteSpace(query) then
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

    let private treeBrowserPanel (model: Model) (dispatch: Dispatch<Msg>) =
        Border.create [
            Border.dock Dock.Left
            Border.width 300.0
            Border.borderBrush (SolidColorBrush(Color.Parse("#3A3A3A")))
            Border.borderThickness (0.0, 0.0, 1.0, 0.0)
            Border.child (
                DockPanel.create [
                    DockPanel.children [
                        TextBox.create [
                            TextBox.dock Dock.Top
                            TextBox.watermark "Search nodes..."
                            TextBox.text model.SearchQuery
                            TextBox.onTextChanged (SetSearchQuery >> dispatch)
                            TextBox.margin 5.0
                            TextBox.fontSize 12.0
                        ]
                        ScrollViewer.create [
                            ScrollViewer.content (
                                StackPanel.create [
                                    StackPanel.orientation Orientation.Vertical
                                    StackPanel.children (
                                        let filteredNodes = filterTree model.SearchQuery model.TreeRoot
                                        filteredNodes |> List.map (renderTreeNode dispatch)
                                    )
                                ]
                            )
                        ]
                    ]
                ]
            )
        ]

    let private endpointViewerPanel (model: Model) (dispatch: Dispatch<Msg>) =
        ScrollViewer.create [
            ScrollViewer.content (
                StackPanel.create [
                    StackPanel.orientation Orientation.Vertical
                    StackPanel.margin 10.0
                    StackPanel.spacing 10.0
                    StackPanel.children (
                        match model.SelectedNode with
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
                                        TextBlock.text (sprintf "Node: %s" node.Name)
                                        TextBlock.fontSize 16.0
                                        TextBlock.fontWeight FontWeight.Bold
                                    ]
                                    TextBlock.create [
                                        TextBlock.text (sprintf "Path: %s" node.Path)
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
                                                                dispatch (GetEndpointValue (sprintf "%s/%s" node.Path ep.Name))
                                                            )
                                                            Button.fontSize 10.0
                                                            Button.padding (5.0, 2.0)
                                                        ]
                                                    ]
                                                ]

                                                let fullPath = sprintf "%s/%s" node.Path ep.Name
                                                match Map.tryFind fullPath model.EndpointValues with
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
                                        TextBlock.text (sprintf "Node: %s" node.Name)
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

    // ─── Component host with MVU dispatch loop ───

    let view () =
        Component(fun ctx ->
            let model = ctx.useState (init ())

            let rec dispatch (msg: Msg) =
                Dispatcher.UIThread.Post(Action(fun () ->
                    try
                        let newModel, cmds = update msg model.Current
                        model.Set newModel
                        for sub in cmds do
                            sub dispatch
                    with ex ->
                        eprintfn "[MVU] Update error for %A: %s" msg ex.Message
                ))

            DockPanel.create [
                DockPanel.children [
                    statusBar model.Current
                    connectionPanel model.Current dispatch
                    treeBrowserPanel model.Current dispatch
                    endpointViewerPanel model.Current dispatch
                ]
            ]
        )
