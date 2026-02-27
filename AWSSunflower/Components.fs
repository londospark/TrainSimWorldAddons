namespace CounterApp

open System
open Avalonia
open Avalonia.Controls
open Avalonia.FuncUI.Types
open Avalonia.FuncUI.DSL
open Avalonia.Layout
open Avalonia.Media

module Components =

    /// Port selector combobox component
    let portSelector (ports: string list) (selectedPort: string option) (onSelectionChanged: string option -> unit) =
        ComboBox.create [
            ComboBox.dock Dock.Top
            ComboBox.placeholderText "Select a COM port."
            ComboBox.horizontalAlignment HorizontalAlignment.Stretch
            ComboBox.horizontalContentAlignment HorizontalAlignment.Center
            ComboBox.dataItems ports
            ComboBox.selectedItem (selectedPort |> Option.defaultValue "")
            ComboBox.onSelectedItemChanged (fun item ->
                let portName = string item
                if String.IsNullOrEmpty portName then
                    onSelectionChanged None
                else
                    onSelectionChanged (Some portName)
            )
        ]

    /// Connection button with dynamic text and styling
    let connectionButton (connectionState: ConnectionState) (isConnecting: bool) (onToggleConnection: unit -> unit) =
        let (buttonText, buttonColor, isEnabled) =
            match connectionState with
            | ConnectionState.Connected portName -> 
                ("Disconnect", Color.Parse("#00AA00"), not isConnecting)
            | ConnectionState.Connecting ->
                ("Connecting...", Color.Parse("#FFAA00"), false)
            | ConnectionState.Disconnected ->
                ("Connect", Color.Parse("#FFFFFF"), not isConnecting)
            | ConnectionState.Error _ ->
                ("Connect", Color.Parse("#FF5555"), true)

        Button.create [
            Button.dock Dock.Top
            Button.horizontalAlignment HorizontalAlignment.Stretch
            Button.horizontalContentAlignment HorizontalAlignment.Center
            Button.content buttonText
            Button.isEnabled isEnabled
            Button.onClick (fun _ -> onToggleConnection ())
            Button.foreground (SolidColorBrush buttonColor)
            Button.padding (Thickness(5.0, 10.0, 5.0, 10.0))
            Button.fontSize 14.0
        ]

    /// Action buttons (Set/Clear sunflower)
    let actionButtons (isConnected: bool) (onSetSunflower: unit -> unit) (onClearSunflower: unit -> unit) =
        StackPanel.create [
            StackPanel.dock Dock.Bottom
            StackPanel.orientation Orientation.Vertical
            StackPanel.spacing 5.0
            StackPanel.children [
                Button.create [
                    Button.onClick (fun _ -> onSetSunflower ())
                    Button.content "Set sunflower"
                    Button.isEnabled isConnected
                    Button.horizontalAlignment HorizontalAlignment.Stretch
                    Button.horizontalContentAlignment HorizontalAlignment.Center
                    Button.padding (Thickness(5.0, 10.0, 5.0, 10.0))
                    Button.fontSize 12.0
                ]
                Button.create [
                    Button.onClick (fun _ -> onClearSunflower ())
                    Button.content "Clear sunflower"
                    Button.isEnabled isConnected
                    Button.horizontalAlignment HorizontalAlignment.Stretch
                    Button.horizontalContentAlignment HorizontalAlignment.Center
                    Button.padding (Thickness(5.0, 10.0, 5.0, 10.0))
                    Button.fontSize 12.0
                ]
            ]
        ]

    /// Port display showing current selected/connected port
    let portDisplay (connectionState: ConnectionState) =
        let displayText =
            match connectionState with
            | ConnectionState.Connected portName -> $"{portName} (Connected)"
            | ConnectionState.Connecting -> "Connecting..."
            | ConnectionState.Disconnected -> "No Port Selected"
            | ConnectionState.Error (PortInUse portName) -> $"{portName} (In Use)"
            | ConnectionState.Error (PortNotFound portName) -> $"{portName} (Not Found)"
            | ConnectionState.Error (OpenFailed _) -> "Connection Failed"
            | ConnectionState.Error (SendFailed _) -> "Send Failed"
            | ConnectionState.Error Disconnected -> "Disconnected"

        let textColor =
            match connectionState with
            | ConnectionState.Connected _ -> Color.Parse("#00AA00")
            | ConnectionState.Connecting -> Color.Parse("#FFAA00")
            | ConnectionState.Disconnected -> Color.Parse("#FFFFFF")
            | ConnectionState.Error _ -> Color.Parse("#FF5555")

        TextBlock.create [
            TextBlock.dock Dock.Top
            TextBlock.fontSize 36.0
            TextBlock.fontWeight FontWeight.Bold
            TextBlock.verticalAlignment VerticalAlignment.Center
            TextBlock.horizontalAlignment HorizontalAlignment.Center
            TextBlock.text displayText
            TextBlock.foreground (SolidColorBrush textColor)
            TextBlock.margin (Thickness(0.0, 20.0, 0.0, 20.0))
        ]

    /// Error toast notification component
    let errorToast (toasts: Toast list) (onDismiss: Guid -> unit) =
        if toasts.IsEmpty then
            TextBlock.create [] :> IView
        else
            StackPanel.create [
                StackPanel.dock Dock.Top
                StackPanel.orientation Orientation.Vertical
                StackPanel.horizontalAlignment HorizontalAlignment.Right
                StackPanel.verticalAlignment VerticalAlignment.Top
                StackPanel.margin (Thickness(10.0))
                StackPanel.spacing 5.0
                StackPanel.children (
                    [
                        for toast in toasts ->
                            Border.create [
                                Border.background (SolidColorBrush(Color.Parse("#CC0000")))
                                Border.borderBrush (SolidColorBrush(Color.Parse("#FF0000")))
                                Border.borderThickness (Thickness 2.0)
                                Border.cornerRadius (CornerRadius 5.0)
                                Border.padding (Thickness(10.0))
                                Border.maxWidth 300.0
                                Border.child (
                                    StackPanel.create [
                                        StackPanel.orientation Orientation.Vertical
                                        StackPanel.spacing 8.0
                                        StackPanel.children [
                                            TextBlock.create [
                                                TextBlock.text toast.Message
                                                TextBlock.foreground (SolidColorBrush Colors.White)
                                                TextBlock.fontSize 12.0
                                                TextBlock.textWrapping TextWrapping.Wrap
                                            ]
                                            Button.create [
                                                Button.content "Dismiss"
                                                Button.onClick (fun _ -> onDismiss toast.Id)
                                                Button.horizontalAlignment HorizontalAlignment.Right
                                                Button.padding (Thickness(8.0, 4.0, 8.0, 4.0))
                                                Button.fontSize 10.0
                                            ]
                                        ]
                                    ]
                                )
                            ]
                    ]
                )
            ]

    /// Main layout combining all components
    let mainLayout 
        (ports: string list)
        (connectionState: ConnectionState)
        (isConnecting: bool)
        (toasts: Toast list)
        (onPortSelected: string option -> unit)
        (onToggleConnection: unit -> unit)
        (onSetSunflower: unit -> unit)
        (onClearSunflower: unit -> unit)
        (onDismissToast: Guid -> unit) =
        
        DockPanel.create [
            DockPanel.children [
                // Error toasts
                errorToast toasts onDismissToast
                
                // Port selector
                portSelector ports 
                    (match connectionState with ConnectionState.Connected p -> Some p | _ -> None)
                    onPortSelected
                
                // Connection button
                connectionButton connectionState isConnecting onToggleConnection
                
                // Port display
                portDisplay connectionState
                
                // Action buttons
                actionButtons 
                    (match connectionState with ConnectionState.Connected _ -> true | _ -> false)
                    onSetSunflower
                    onClearSunflower
            ]
        ]



