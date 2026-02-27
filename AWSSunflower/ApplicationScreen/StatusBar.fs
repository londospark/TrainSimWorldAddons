namespace CounterApp

open Avalonia.Controls
open Avalonia.FuncUI.DSL
open Avalonia.Layout
open Avalonia.Media
open TSWApi
open CounterApp.ApplicationScreen
open CounterApp.ApplicationScreenHelpers

module StatusBar =

    let statusBar (model: Model) =
        Border.create [
            Border.dock Dock.Bottom
            Border.background (AppColors.panelBg)
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
                                | ApiConnectionState.Connected _ -> AppColors.connected
                                | ApiConnectionState.Error _ -> AppColors.error
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
                                TextBlock.foreground (AppColors.info)
                            ]
                        | None -> ()
                    ]
                ]
            )
        ]
