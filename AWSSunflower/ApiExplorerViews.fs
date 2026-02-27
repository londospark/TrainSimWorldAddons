namespace CounterApp

open System
open Avalonia.Controls
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Types
open Avalonia.Layout
open Avalonia.Media
open TSWApi
open CounterApp.PortDetection
open CounterApp.ApiExplorer
open CounterApp.ApiExplorerHelpers
open CounterApp.ApiExplorerCommands
open global.Elmish

module ApiExplorerViews =

    // ─── Color constants ───

    module private AppColors =
        let connected = "#00AA00"
        let error = "#FF5555"
        let warning = "#FFAA00"
        let panelBg = "#2A2A2A"
        let border = "#3A3A3A"
        let info = "#55AAFF"

    // ─── View functions ───

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
