namespace CounterApp

open System
open System.Net.Http
open Avalonia.Controls
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Types
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Threading
open TSWApi
open TSWApi.Subscription
open CounterApp.CommandMapping
open CounterApp.PortDetection
open global.Elmish

module ApiExplorer =

    // ─── Shared HttpClient & Subscription ───

    let private httpClient = new HttpClient()
    let private currentSubscription : ISubscription option ref = ref None

    // ─── Color constants ───

    module private AppColors =
        let connected = "#00AA00"
        let error = "#FF5555"
        let warning = "#FFAA00"
        let panelBg = "#2A2A2A"
        let border = "#3A3A3A"
        let info = "#55AAFF"

    // ─── Model ───

    type Model =
        { // Serial port
          DetectedPorts: DetectedPort list
          SerialConnectionState: ConnectionState
          SerialIsConnecting: bool
          // API Explorer
          BaseUrl: string
          CommKey: string
          ApiConfig: ApiConfig option
          ConnectionState: ApiConnectionState
          IsConnecting: bool
          TreeRoot: TreeNodeState list
          SelectedNode: TreeNodeState option
          EndpointValues: Map<string, string>
          LastResponseTime: TimeSpan option
          SearchQuery: string
          CurrentLoco: string option
          BindingsConfig: BindingsConfig
          PollingValues: Map<string, string>
          // Command mapping
          ActiveAddon: AddonCommandSet option
          // Shared serial port
          SerialPort: IO.Ports.SerialPort option
          SerialPortName: string option }

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
        // Binding
        | BindEndpoint of nodePath: string * endpointName: string
        | UnbindEndpoint of nodePath: string * endpointName: string
        // Loco detection
        | DetectLoco
        | LocoDetected of string
        | LocoDetectError of string
        // Subscription-based polling
        | EndpointChanged of ValueChange
        // Serial (API Explorer internal)
        | SetSerialPort of string option
        | DisconnectSerial
        // Serial tab (unified)
        | PortsUpdated of DetectedPort list
        | ToggleSerialConnection
        | SerialConnectResult of Result<IO.Ports.SerialPort, SerialError>
        | SendSerialCommand of string

    // ─── Init ───

    let init () =
        { DetectedPorts = []
          SerialConnectionState = ConnectionState.Disconnected
          SerialIsConnecting = false
          BaseUrl = "http://localhost:31270"
          CommKey = ""
          ApiConfig = None
          ConnectionState = ApiConnectionState.Disconnected
          IsConnecting = false
          TreeRoot = []
          SelectedNode = None
          EndpointValues = Map.empty
          LastResponseTime = None
          SearchQuery = ""
          CurrentLoco = None
          BindingsConfig = BindingPersistence.load ()
          PollingValues = Map.empty
          ActiveAddon = Some AWSSunflowerCommands.commandSet
          SerialPort = None
          SerialPortName = None }

    let private stripRootPrefix (path: string) =
        if not (isNull path) && path.StartsWith("Root/") then path.Substring(5) else path

    /// Guard against CLR null strings from JSON deserialization
    let private nullSafe (s: string) = if isNull s then "" else s

    let private effectiveName (n: TSWApi.Types.Node) =
        if not (System.String.IsNullOrEmpty n.NodeName) then n.NodeName
        elif not (System.String.IsNullOrEmpty n.Name) then n.Name
        else ""

    /// Recursively map an API Node to a TreeNodeState, preserving nested children.
    let rec private mapNodeToTreeState (parentPath: string) (n: TSWApi.Types.Node) : TreeNodeState =
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

    let private endpointKey nodePath endpointName = sprintf "%s.%s" nodePath endpointName

    let private getLocoBindings (config: BindingsConfig) (locoName: string) =
        config.Locos
        |> List.tryFind (fun l -> l.LocoName = locoName)
        |> Option.map (fun l -> l.BoundEndpoints)
        |> Option.defaultValue []

    let private isSerialConnected (model: Model) =
        match model.SerialConnectionState with ConnectionState.Connected _ -> true | _ -> false

    let private resetSerialCmd (model: Model) =
        model.ActiveAddon
        |> Option.bind (fun addon -> CommandMapping.resetCommand addon |> Option.map CommandMapping.toWireString)
        |> Option.map (fun wire -> Cmd.ofMsg (SendSerialCommand wire))
        |> Option.defaultValue Cmd.none

    // ─── Async commands ───

    let private timedApiCall (apiCall: Async<ApiResult<'T>>) : Async<'T * TimeSpan> =
        async {
            let startTime = DateTime.Now
            let! result = apiCall
            let elapsed = DateTime.Now - startTime
            match result with
            | Ok value -> return (value, elapsed)
            | Error err -> return failwithf "API error: %A" err
        }

    let private connectCmd (baseUrl: string) (commKey: string) =
        Cmd.OfAsync.either
            (fun () ->
                async {
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
                    let! (info, elapsed) = timedApiCall (TSWApi.ApiClient.getInfo httpClient config)
                    return (keyValue, config, info, elapsed)
                })
            ()
            (fun (key, config, info, elapsed) -> ConnectSuccess(key, config, info, elapsed))
            (fun ex -> ConnectError ex.Message)

    let private loadRootNodesCmd (config: ApiConfig) =
        Cmd.OfAsync.either
            (fun () ->
                async {
                    let! (listResp, elapsed) = timedApiCall (TSWApi.ApiClient.listNodes httpClient config None)
                    let nodes =
                        listResp.Nodes
                        |> Option.defaultValue []
                        |> List.map (mapNodeToTreeState "")
                    return (nodes, elapsed)
                })
            ()
            (fun (nodes, elapsed) -> RootNodesLoaded(nodes, elapsed))
            (fun ex -> RootNodesError ex.Message)

    let private expandNodeCmd (config: ApiConfig) (nodePath: string) =
        Cmd.OfAsync.either
            (fun () ->
                async {
                    let! (listResp, elapsed) = timedApiCall (TSWApi.ApiClient.listNodes httpClient config (Some nodePath))
                    let children =
                        listResp.Nodes
                        |> Option.defaultValue []
                        |> List.map (mapNodeToTreeState nodePath)
                    return (nodePath, children, listResp.Endpoints, elapsed)
                })
            ()
            (fun (p, ch, eps, elapsed) -> NodeExpanded(p, ch, eps, elapsed))
            (fun ex -> ApiError ex.Message)

    let private getValueCmd (config: ApiConfig) (endpointPath: string) =
        Cmd.OfAsync.either
            (fun () ->
                async {
                    let! (getResp, elapsed) = timedApiCall (TSWApi.ApiClient.getValue httpClient config endpointPath)
                    let valueStr =
                        if isNull (getResp.Values :> obj) || getResp.Values.Count = 0 then
                            "(no values returned)"
                        else
                            getResp.Values
                            |> Seq.map (fun kvp -> sprintf "%s: %O" kvp.Key kvp.Value)
                            |> String.concat ", "
                    return (endpointPath, valueStr, elapsed)
                })
            ()
            (fun (p, v, elapsed) -> EndpointValueReceived(p, v, elapsed))
            (fun ex -> ApiError ex.Message)

    let private detectLocoCmd (config: ApiConfig) =
        Cmd.OfAsync.either
            (fun () ->
                async {
                    let! getResult = TSWApi.ApiClient.getValue httpClient config "CurrentDrivableActor.ObjectName"
                    match getResult with
                    | Ok getResp ->
                        if isNull (getResp.Values :> obj) || getResp.Values.Count = 0 then
                            return failwith "No ObjectName returned"
                        else
                            let name = getResp.Values.["ObjectName"] |> string
                            return name
                    | Error err -> return failwithf "Detect loco failed: %A" err
                })
            ()
            LocoDetected
            (fun ex -> LocoDetectError ex.Message)

    let private createSubscriptionCmd (config: ApiConfig) (bindings: BoundEndpoint list) =
        Cmd.ofEffect (fun dispatch ->
            currentSubscription.Value |> Option.iter (fun s -> s.Dispose())
            let subConfig =
                { TSWApi.Subscription.defaultConfig with
                    Interval = TimeSpan.FromMilliseconds(200.0)
                    OnChange = fun vc ->
                        Dispatcher.UIThread.Post(fun () -> dispatch (EndpointChanged vc))
                    OnError = fun _ _ -> () }
            let sub = TSWApi.Subscription.create httpClient config subConfig
            for b in bindings do
                sub.Add { NodePath = b.NodePath; EndpointName = b.EndpointName }
            currentSubscription.Value <- Some sub
        )

    let private disposeSubscription () =
        currentSubscription.Value |> Option.iter (fun s -> s.Dispose())
        currentSubscription.Value <- None

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
            disposeSubscription ()
            { model with
                ApiConfig = None; ConnectionState = ApiConnectionState.Disconnected
                TreeRoot = []; SelectedNode = None
                EndpointValues = Map.empty; LastResponseTime = None
                CurrentLoco = None
                PollingValues = Map.empty },
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

        | BindEndpoint (nodePath, endpointName) ->
            match model.CurrentLoco, model.ApiConfig with
            | None, _ | _, None -> model, Cmd.none
            | Some locoName, Some config ->
                let binding = { NodePath = nodePath; EndpointName = endpointName; Label = endpointKey nodePath endpointName }
                let newConfig = BindingPersistence.addBinding model.BindingsConfig locoName binding
                BindingPersistence.save newConfig
                let newModel = { model with BindingsConfig = newConfig }
                let addr = { NodePath = nodePath; EndpointName = endpointName }
                match currentSubscription.Value with
                | Some sub ->
                    sub.Add addr
                    newModel, Cmd.none
                | None ->
                    let allBindings = getLocoBindings newConfig locoName
                    newModel, createSubscriptionCmd config allBindings

        | UnbindEndpoint (nodePath, endpointName) ->
            match model.CurrentLoco with
            | Some locoName ->
                let newConfig = BindingPersistence.removeBinding model.BindingsConfig locoName nodePath endpointName
                BindingPersistence.save newConfig
                let key = endpointKey nodePath endpointName
                let addr = { NodePath = nodePath; EndpointName = endpointName }
                currentSubscription.Value |> Option.iter (fun sub ->
                    sub.Remove addr
                    if sub.Endpoints.IsEmpty then disposeSubscription ())
                { model with
                    BindingsConfig = newConfig
                    PollingValues = Map.remove key model.PollingValues },
                resetSerialCmd model
            | None -> model, Cmd.none

        | DetectLoco ->
            match model.ApiConfig with
            | Some config -> model, detectLocoCmd config
            | None -> model, Cmd.none

        | LocoDetected locoName ->
            if model.CurrentLoco = Some locoName then
                model, Cmd.none
            else
                disposeSubscription ()
                let newConfig = BindingPersistence.load ()
                let locoBindings = getLocoBindings newConfig locoName
                let subCmd =
                    match model.ApiConfig with
                    | Some config when not locoBindings.IsEmpty -> createSubscriptionCmd config locoBindings
                    | _ -> Cmd.none
                { model with
                    CurrentLoco = Some locoName
                    BindingsConfig = newConfig
                    PollingValues = Map.empty
                    TreeRoot = []
                    SelectedNode = None
                    EndpointValues = Map.empty },
                Cmd.batch [
                    match model.ApiConfig with
                    | Some config -> loadRootNodesCmd config
                    | None -> Cmd.none
                    resetSerialCmd model
                    subCmd
                ]

        | LocoDetectError _ ->
            model, Cmd.none

        | EndpointChanged vc ->
            let key = Subscription.endpointPath vc.Address
            let newModel = { model with PollingValues = Map.add key vc.NewValue model.PollingValues }
            let cmd =
                model.ActiveAddon
                |> Option.bind (fun addon -> CommandMapping.translate addon vc.Address.EndpointName vc.NewValue)
                |> Option.map (fun serialCmd -> Cmd.ofMsg (SendSerialCommand (CommandMapping.toWireString serialCmd)))
                |> Option.defaultValue Cmd.none
            newModel, cmd

        | SetSerialPort portName ->
            { model with SerialPortName = portName }, Cmd.none

        | DisconnectSerial ->
            SerialPortModule.disconnect model.SerialPort
            { model with SerialPort = None }, Cmd.none

        | PortsUpdated ports ->
            let newModel = { model with DetectedPorts = ports }
            // Auto-select Arduino if exactly one found
            match classifyPorts ports with
            | SingleArduino arduino when model.SerialPortName.IsNone ->
                { newModel with SerialPortName = Some arduino.PortName }, Cmd.none
            | _ -> newModel, Cmd.none

        | ToggleSerialConnection ->
            match model.SerialConnectionState with
            | ConnectionState.Connected _ ->
                SerialPortModule.disconnect model.SerialPort
                { model with
                    SerialPort = None
                    SerialConnectionState = ConnectionState.Disconnected
                    SerialIsConnecting = false }, Cmd.none
            | _ ->
                match model.SerialPortName with
                | Some portName ->
                    { model with SerialIsConnecting = true; SerialConnectionState = ConnectionState.Connecting },
                    Cmd.OfAsync.either
                        (fun () -> SerialPortModule.connectAsync portName 9600)
                        ()
                        (fun result -> SerialConnectResult result)
                        (fun ex -> SerialConnectResult (Error (OpenFailed ex.Message)))
                | None -> model, Cmd.none

        | SerialConnectResult result ->
            match result with
            | Ok port ->
                let portName = model.SerialPortName |> Option.defaultValue ""
                { model with
                    SerialPort = Some port
                    SerialConnectionState = ConnectionState.Connected portName
                    SerialIsConnecting = false }, Cmd.none
            | Error error ->
                { model with
                    SerialConnectionState = ConnectionState.Error error
                    SerialIsConnecting = false }, Cmd.none

        | SendSerialCommand cmd ->
            match model.SerialPort with
            | Some port when port.IsOpen ->
                async {
                    let! _ = SerialPortModule.sendAsync port cmd
                    ()
                } |> Async.Start
                model, Cmd.none
            | _ -> model, Cmd.none

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
            Border.background (SolidColorBrush(Color.Parse(AppColors.panelBg)))
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
                                | ApiConnectionState.Connected _ -> SolidColorBrush(Color.Parse(AppColors.connected))
                                | ApiConnectionState.Error _ -> SolidColorBrush(Color.Parse(AppColors.error))
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

                        match model.CurrentLoco with
                        | Some loco ->
                            TextBlock.create [
                                TextBlock.text (sprintf "Loco: %s" loco)
                                TextBlock.fontSize 11.0
                                TextBlock.foreground (SolidColorBrush(Color.Parse(AppColors.info)))
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
            Border.borderBrush (SolidColorBrush(Color.Parse(AppColors.border)))
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
                            // Guard against CLR null from JSON deserialization
                            let endpoints = node.Endpoints |> Option.bind (fun eps -> if isNull (eps :> obj) then None else Some eps)
                            match endpoints with
                            | Some endpoints when endpoints.Length > 0 ->
                                [
                                    TextBlock.create [
                                        TextBlock.text (sprintf "Node: %s" (nullSafe node.Name))
                                        TextBlock.fontSize 16.0
                                        TextBlock.fontWeight FontWeight.Bold
                                    ]
                                    TextBlock.create [
                                        TextBlock.text (sprintf "Path: %s" (nullSafe node.Path))
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
                                        let epName = nullSafe ep.Name
                                        let nodePath = nullSafe node.Path
                                        StackPanel.create [
                                            StackPanel.orientation Orientation.Vertical
                                            StackPanel.margin (0.0, 5.0, 0.0, 5.0)
                                            StackPanel.children [
                                                StackPanel.create [
                                                    StackPanel.orientation Orientation.Horizontal
                                                    StackPanel.spacing 10.0
                                                    StackPanel.children [
                                                        TextBlock.create [
                                                            TextBlock.text epName
                                                            TextBlock.fontSize 12.0
                                                            TextBlock.fontWeight FontWeight.SemiBold
                                                        ]
                                                        if ep.Writable then
                                                            TextBlock.create [
                                                                TextBlock.text "(writable)"
                                                                TextBlock.fontSize 10.0
                                                                TextBlock.foreground (SolidColorBrush(Color.Parse(AppColors.warning)))
                                                            ]
                                                        Button.create [
                                                            Button.content "Get Value"
                                                            Button.onClick (fun _ ->
                                                                dispatch (GetEndpointValue (endpointKey nodePath epName))
                                                            )
                                                            Button.fontSize 10.0
                                                            Button.padding (5.0, 2.0)
                                                        ]
                                                        Button.create [
                                                            Button.content "📌 Bind"
                                                            Button.onClick (fun _ ->
                                                                dispatch (BindEndpoint (nodePath, epName))
                                                            )
                                                            Button.fontSize 10.0
                                                            Button.padding (5.0, 2.0)
                                                            Button.isEnabled model.CurrentLoco.IsSome
                                                        ]
                                                    ]
                                                ]

                                                let fullPath = endpointKey nodePath epName
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

    let private bindingsPanel (model: Model) (dispatch: Dispatch<Msg>) =
        let currentBindings =
            match model.CurrentLoco with
            | Some locoName -> getLocoBindings model.BindingsConfig locoName
            | None -> []
        Border.create [
            Border.dock Dock.Bottom
            Border.borderBrush (SolidColorBrush(Color.Parse(AppColors.border)))
            Border.borderThickness (0.0, 1.0, 0.0, 0.0)
            Border.maxHeight 200.0
            Border.child (
                DockPanel.create [
                    DockPanel.children [
                        StackPanel.create [
                            StackPanel.dock Dock.Top
                            StackPanel.orientation Orientation.Horizontal
                            StackPanel.margin (10.0, 5.0)
                            StackPanel.spacing 10.0
                            StackPanel.children [
                                TextBlock.create [
                                    TextBlock.text (sprintf "Active Bindings (%d)" currentBindings.Length)
                                    TextBlock.fontSize 12.0
                                    TextBlock.fontWeight FontWeight.Bold
                                ]
                                TextBlock.create [
                                    TextBlock.text (
                                        if currentSubscription.Value |> Option.map (fun s -> s.IsActive) |> Option.defaultValue false
                                        then "● Live"
                                        else "○ Idle"
                                    )
                                    TextBlock.fontSize 10.0
                                    TextBlock.foreground (
                                        if currentSubscription.Value |> Option.map (fun s -> s.IsActive) |> Option.defaultValue false
                                        then SolidColorBrush(Color.Parse(AppColors.connected))                                        else SolidColorBrush Colors.Gray
                                    )
                                ]
                            ]
                        ]
                        ScrollViewer.create [
                            ScrollViewer.content (
                                StackPanel.create [
                                    StackPanel.orientation Orientation.Vertical
                                    StackPanel.margin (10.0, 0.0)
                                    StackPanel.spacing 3.0
                                    StackPanel.children (
                                        if currentBindings.IsEmpty then
                                            [
                                                TextBlock.create [
                                                    TextBlock.text "No bindings. Use 📌 Bind on endpoints above."
                                                    TextBlock.fontSize 11.0
                                                    TextBlock.foreground (SolidColorBrush Colors.Gray)
                                                ]
                                            ]
                                        else
                                            currentBindings |> List.map (fun b ->
                                                let key = endpointKey b.NodePath b.EndpointName
                                                let value = Map.tryFind key model.PollingValues |> Option.defaultValue "—"
                                                StackPanel.create [
                                                    StackPanel.orientation Orientation.Horizontal
                                                    StackPanel.spacing 8.0
                                                    StackPanel.children [
                                                        TextBlock.create [
                                                            TextBlock.text (sprintf "%s = %s" b.Label value)
                                                            TextBlock.fontSize 11.0
                                                            TextBlock.verticalAlignment VerticalAlignment.Center
                                                            TextBlock.width 400.0
                                                        ]
                                                        Button.create [
                                                            Button.content "✕"
                                                            Button.onClick (fun _ ->
                                                                dispatch (UnbindEndpoint (b.NodePath, b.EndpointName))
                                                            )
                                                            Button.fontSize 10.0
                                                            Button.padding (4.0, 1.0)
                                                        ]
                                                    ]
                                                ] :> IView
                                            )
                                    )
                                ]
                            )
                        ]
                    ]
                ]
            )
        ]

    // ─── Serial Port Side Panel ───

    let private serialPortPanel (model: Model) (dispatch: Dispatch<Msg>) =
        Border.create [
            Border.dock Dock.Right
            Border.width 200.0
            Border.background (SolidColorBrush(Color.Parse(AppColors.panelBg)))
            Border.borderBrush (SolidColorBrush(Color.Parse(AppColors.border)))
            Border.borderThickness (1.0, 0.0, 0.0, 0.0)
            Border.padding 10.0
            Border.child (
                StackPanel.create [
                    StackPanel.orientation Orientation.Vertical
                    StackPanel.spacing 8.0
                    StackPanel.children [
                        // Header
                        TextBlock.create [
                            TextBlock.text "🔌 Serial Port"
                            TextBlock.fontSize 14.0
                            TextBlock.fontWeight FontWeight.Bold
                            TextBlock.margin (0.0, 0.0, 0.0, 4.0)
                        ]

                        // COM port dropdown
                        ComboBox.create [
                            ComboBox.placeholderText "Select port..."
                            ComboBox.horizontalAlignment HorizontalAlignment.Stretch
                            ComboBox.dataItems (model.DetectedPorts |> List.map portDisplayName)
                            ComboBox.selectedItem (
                                model.SerialPortName
                                |> Option.bind (fun name -> model.DetectedPorts |> List.tryFind (fun p -> p.PortName = name))
                                |> Option.map portDisplayName
                                |> Option.defaultValue ""
                            )
                            ComboBox.onSelectedItemChanged (fun item ->
                                let displayName = string item
                                if String.IsNullOrEmpty displayName then dispatch (SetSerialPort None)
                                else
                                    let port = model.DetectedPorts |> List.tryFind (fun p -> portDisplayName p = displayName)
                                    dispatch (SetSerialPort (port |> Option.map (fun p -> p.PortName)))
                            )
                            ComboBox.fontSize 11.0
                        ]

                        // Connect/Disconnect button
                        Button.create [
                            Button.content (
                                match model.SerialConnectionState with
                                | ConnectionState.Connected _ -> "Disconnect"
                                | ConnectionState.Connecting -> "Connecting..."
                                | _ -> "Connect"
                            )
                            Button.onClick (fun _ -> dispatch ToggleSerialConnection)
                            Button.isEnabled (
                                match model.SerialConnectionState with
                                | ConnectionState.Connecting -> false
                                | _ -> model.SerialPortName.IsSome || isSerialConnected model
                            )
                            Button.horizontalAlignment HorizontalAlignment.Stretch
                            Button.fontSize 11.0
                            Button.foreground (
                                match model.SerialConnectionState with
                                | ConnectionState.Connected _ -> SolidColorBrush(Color.Parse(AppColors.connected))
                                | ConnectionState.Error _ -> SolidColorBrush(Color.Parse(AppColors.error))
                                | _ -> SolidColorBrush Colors.White
                            )
                        ]

                        // Status indicator
                        StackPanel.create [
                            StackPanel.orientation Orientation.Horizontal
                            StackPanel.spacing 6.0
                            StackPanel.children [
                                TextBlock.create [
                                    TextBlock.text "●"
                                    TextBlock.fontSize 10.0
                                    TextBlock.foreground (
                                        match model.SerialConnectionState with
                                        | ConnectionState.Connected _ -> SolidColorBrush(Color.Parse(AppColors.connected))
                                        | ConnectionState.Error _ -> SolidColorBrush(Color.Parse(AppColors.error))
                                        | ConnectionState.Connecting -> SolidColorBrush(Color.Parse(AppColors.warning))
                                        | _ -> SolidColorBrush Colors.Gray
                                    )
                                ]
                                TextBlock.create [
                                    TextBlock.text (
                                        match model.SerialConnectionState with
                                        | ConnectionState.Connected p -> p
                                        | ConnectionState.Connecting -> "Connecting..."
                                        | ConnectionState.Disconnected -> "Not connected"
                                        | ConnectionState.Error (PortInUse p) -> sprintf "%s in use" p
                                        | ConnectionState.Error (PortNotFound p) -> sprintf "%s missing" p
                                        | ConnectionState.Error (OpenFailed _) -> "Open failed"
                                        | ConnectionState.Error (SendFailed _) -> "Send failed"
                                        | ConnectionState.Error Disconnected -> "Disconnected"
                                    )
                                    TextBlock.fontSize 10.0
                                    TextBlock.foreground (SolidColorBrush Colors.Gray)
                                ]
                            ]
                        ]

                        // Separator
                        Border.create [
                            Border.height 1.0
                            Border.background (SolidColorBrush(Color.Parse(AppColors.border)))
                            Border.margin (0.0, 4.0)
                        ]

                        // Sunflower buttons
                        Button.create [
                            Button.content "🌻 Set Sunflower"
                            Button.onClick (fun _ -> dispatch (SendSerialCommand "s"))
                            Button.isEnabled (isSerialConnected model)
                            Button.horizontalAlignment HorizontalAlignment.Stretch
                            Button.fontSize 11.0
                            Button.padding (5.0, 6.0)
                        ]
                        Button.create [
                            Button.content "✕ Clear Sunflower"
                            Button.onClick (fun _ -> dispatch (SendSerialCommand "c"))
                            Button.isEnabled (isSerialConnected model)
                            Button.horizontalAlignment HorizontalAlignment.Stretch
                            Button.fontSize 11.0
                            Button.padding (5.0, 6.0)
                        ]
                    ]
                ]
            )
        ]

    // ─── Public unified view (called from Program.fs) ───

    let mainView (model: Model) (dispatch: Dispatch<Msg>) =
        DockPanel.create [
            DockPanel.children [
                // Right: Serial port panel
                serialPortPanel model dispatch
                // Bottom: Status bar
                statusBar model
                // Bottom (above status): Bindings panel
                bindingsPanel model dispatch
                // Top: Connection panel
                connectionPanel model dispatch
                // Left: Tree browser
                treeBrowserPanel model dispatch
                // Center: Endpoint viewer (fills remaining)
                endpointViewerPanel model dispatch
            ]
        ]
