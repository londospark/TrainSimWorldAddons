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
                                | ApiConnectionState.Connected info -> $"Status: Connected to {info.Meta.GameName} (Build {info.Meta.GameBuildNumber})"
                                | ApiConnectionState.Error msg -> $"Status: Error - {msg}"
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
                                TextBlock.text $"Last response: %.0f{time.TotalMilliseconds}ms"
                                TextBlock.fontSize 11.0
                            ]
                        | None -> ()

                        match model.CurrentLoco with
                        | Some loco ->
                            TextBlock.create [
                                TextBlock.text $"Loco: {loco}"
                                TextBlock.fontSize 11.0
                                TextBlock.foreground (SolidColorBrush(Color.Parse(AppColors.info)))
                            ]
                        | None -> ()
                    ]
                ]
            )
        ]
