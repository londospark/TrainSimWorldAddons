namespace CounterApp

open TSWApi
open TSWApi.Subscription
open CounterApp.CommandMapping
open CounterApp.ApplicationScreen
open CounterApp.ApplicationScreenHelpers
open CounterApp.ApplicationScreenCommands
open global.Elmish

module ApplicationScreenUpdate =

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
                EndpointValues = Map.empty; LastResponseTime = None
                CurrentLoco = None
                PollingValues = Map.empty
                IsSubscriptionActive = false },
            Cmd.ofEffect (fun _ -> disposeSubscription ())

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
                let newModel = { model with BindingsConfig = newConfig }
                let addr = { NodePath = nodePath; EndpointName = endpointName }
                let hasSubscription = currentSubscription.Value.IsSome
                let persistAndSubscribeCmd =
                    Cmd.ofEffect (fun _ ->
                        BindingPersistence.save newConfig
                        match currentSubscription.Value with
                        | Some sub -> sub.Add addr
                        | None -> ())
                let cmds =
                    if hasSubscription then
                        persistAndSubscribeCmd
                    else
                        let allBindings = getLocoBindings newConfig locoName
                        Cmd.batch [ persistAndSubscribeCmd; createSubscriptionCmd config allBindings ]
                { newModel with IsSubscriptionActive = true }, cmds

        | UnbindEndpoint (nodePath, endpointName) ->
            match model.CurrentLoco with
            | Some locoName ->
                let newConfig = BindingPersistence.removeBinding model.BindingsConfig locoName nodePath endpointName
                let key = endpointKey nodePath endpointName
                let addr = { NodePath = nodePath; EndpointName = endpointName }
                let willBeEmpty =
                    currentSubscription.Value
                    |> Option.map (fun sub -> sub.Endpoints |> List.filter (fun a -> a <> addr) |> List.isEmpty)
                    |> Option.defaultValue true
                let persistCmd =
                    Cmd.ofEffect (fun _ ->
                        BindingPersistence.save newConfig
                        currentSubscription.Value |> Option.iter (fun sub ->
                            sub.Remove addr
                            if sub.Endpoints.IsEmpty then disposeSubscription ()))
                { model with
                    BindingsConfig = newConfig
                    PollingValues = Map.remove key model.PollingValues
                    IsSubscriptionActive = not willBeEmpty },
                Cmd.batch [ persistCmd; resetSerialCmd model ]
            | None -> model, Cmd.none

        | DetectLoco ->
            match model.ApiConfig with
            | Some config -> model, detectLocoCmd config
            | None -> model, Cmd.none

        | LocoDetected locoName ->
            if model.CurrentLoco = Some locoName then
                model, Cmd.none
            else
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
                    IsSubscriptionActive = not locoBindings.IsEmpty
                    TreeRoot = []
                    SelectedNode = None
                    EndpointValues = Map.empty },
                Cmd.batch [
                    Cmd.ofEffect (fun _ -> disposeSubscription ())
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
            let port = model.SerialPort
            { model with SerialPort = None },
            Cmd.ofEffect (fun _ -> SerialPortModule.disconnect port)

        | PortsUpdated ports ->
            let newModel = { model with DetectedPorts = ports }
            match PortDetection.classifyPorts ports with
            | PortDetection.SingleArduino arduino when model.SerialPortName.IsNone ->
                { newModel with SerialPortName = Some arduino.PortName }, Cmd.none
            | _ -> newModel, Cmd.none

        | ToggleSerialConnection ->
            match model.SerialConnectionState with
            | ConnectionState.Connected _ ->
                let port = model.SerialPort
                { model with
                    SerialPort = None
                    SerialConnectionState = ConnectionState.Disconnected
                    SerialIsConnecting = false },
                Cmd.ofEffect (fun _ -> SerialPortModule.disconnect port)
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
            model,
            Cmd.ofEffect (fun _ ->
                match model.SerialPort with
                | Some port when port.IsOpen ->
                    SerialPortModule.sendAsync port cmd |> Async.Ignore |> Async.Start
                | _ -> ())
