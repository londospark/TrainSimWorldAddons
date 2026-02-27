namespace CounterApp

open Avalonia.Controls
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Types
open Avalonia.Layout
open Avalonia.Media
open TSWApi
open CounterApp.ApplicationScreen
open CounterApp.ApplicationScreenHelpers
open global.Elmish

module EndpointViewer =

    let renderEndpoint (model: Model) (dispatch: Dispatch<Msg>) (nodePath: string) (ep: Endpoint) : IView =
        let epName = nullSafe ep.Name
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
                                TextBlock.foreground (AppColors.warning)
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

    let endpointViewerPanel (model: Model) (dispatch: Dispatch<Msg>) =
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

                                    yield! endpoints |> List.map (renderEndpoint model dispatch (nullSafe node.Path))
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
