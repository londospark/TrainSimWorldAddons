namespace CounterApp

open System
open Avalonia.Controls
open Avalonia.FuncUI.DSL
open Avalonia.Layout
open Avalonia.Media
open CounterApp.PortDetection
open CounterApp.ApplicationScreen
open CounterApp.ApplicationScreenHelpers
open global.Elmish

module SerialPortPanel =

    let serialStatus (model: Model) =
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

    let serialPortPanel (model: Model) (dispatch: Dispatch<Msg>) =
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
                        serialStatus model

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
