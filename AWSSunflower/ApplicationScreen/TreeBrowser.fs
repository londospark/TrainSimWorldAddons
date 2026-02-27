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

module TreeBrowser =

    let rec renderTreeNode (dispatch: Dispatch<Msg>) (node: TreeNodeState) : IView =
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

    let treeBrowserPanel (model: Model) (dispatch: Dispatch<Msg>) =
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
