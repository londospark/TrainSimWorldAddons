namespace CounterApp

open Avalonia.Controls
open Avalonia.FuncUI.DSL
open Avalonia.Layout
open TSWApi
open CounterApp.ApplicationScreen
open global.Elmish

module ConnectionPanel =

    let connectionPanel (model: Model) (dispatch: Dispatch<Msg>) =
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
