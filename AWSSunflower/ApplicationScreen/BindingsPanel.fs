namespace CounterApp

open Avalonia.Controls
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Types
open Avalonia.Layout
open Avalonia.Media
open CounterApp.ApplicationScreen
open CounterApp.ApplicationScreenHelpers
open CounterApp.ApplicationScreenCommands
open global.Elmish

module BindingsPanel =

    let renderBinding (model: Model) (dispatch: Dispatch<Msg>) (b: BoundEndpoint) : IView =
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

    let bindingsPanel (model: Model) (dispatch: Dispatch<Msg>) =
        let currentBindings =
            model.CurrentLoco
            |> Option.map (getLocoBindings model.BindingsConfig)
            |> Option.defaultValue []
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
                                        then SolidColorBrush(Color.Parse(AppColors.connected))
                                        else SolidColorBrush Colors.Gray
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
                                            currentBindings |> List.map (renderBinding model dispatch)
                                    )
                                ]
                            )
                        ]
                    ]
                ]
            )
        ]
