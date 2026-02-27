namespace CounterApp

open Avalonia.Controls
open Avalonia.FuncUI.DSL
open CounterApp.ApplicationScreen
open CounterApp.ConnectionPanel
open CounterApp.StatusBar
open CounterApp.TreeBrowser
open CounterApp.EndpointViewer
open CounterApp.BindingsPanel
open CounterApp.SerialPortPanel
open global.Elmish

module MainView =

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
