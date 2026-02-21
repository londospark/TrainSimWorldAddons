namespace CounterApp

open System
open System.IO.Ports
open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Themes.Fluent
open Avalonia.FuncUI.Hosts
open Avalonia.Controls
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.Layout
open Avalonia.Threading

module Main =

    let view () =
        Component(fun ctx ->
            let serialPorts = ctx.useState []
            let currentSerialPort = ctx.useState None
            let isConnected = ctx.useState false
            let serialRef = ctx.useState(None, renderOnChange = false)
            
            
            ctx.useEffect(
                handler = (fun _ ->
                    let port = new SerialPort()
                    serialRef.Set (Some port)
                    
                    let timer = DispatcherTimer()
                    timer.Interval <- TimeSpan.FromMilliseconds 1000.0
                    
                    timer.Tick.Add(fun _ ->
                        let currentPorts = SerialPort.GetPortNames() |> List.ofArray
                        
                        if currentPorts <> serialPorts.Current then
                            serialPorts.Set currentPorts
                    )
                    
                    timer.Start()
                    
                    serialPorts.Set (SerialPort.GetPortNames() |> List.ofArray)
                    
                    { new IDisposable with
                        member _.Dispose() =
                            timer.Stop()
                            if port.IsOpen then port.Close()
                            port.Dispose()
                    }
                ),
                triggers = [ EffectTrigger.AfterInit ]
            )
            
            let toggleConnection () =
                match currentSerialPort.Current, serialRef.Current with
                | Some portName, Some port when not isConnected.Current ->
                    try
                        port.PortName <- portName
                        port.BaudRate <- 9600
                        port.Open()
                        isConnected.Set true
                        printfn $"Connected to %s{portName}"
                    with
                    | :? UnauthorizedAccessException ->
                        printfn $"Error: Port {portName} is already in use by another app!"
                    | ex ->
                        printfn $"Failed to open port: %s{ex.Message}"
                
                | _, Some port ->
                    if port.IsOpen then port.Close()
                    isConnected.Set false
                    printfn "Disconnected"
                    
                | _ -> ()
                
            let send s =
                match isConnected.Current, serialRef.Current with
                | true, Some port ->
                    port.WriteLine s
                | _ -> ()
                    

            DockPanel.create [
                DockPanel.children [
                    Button.create [
                        Button.dock Dock.Bottom
                        Button.onClick (fun _ -> send "s")
                        Button.content "Set sunflower"
                        Button.isEnabled currentSerialPort.Current.IsSome
                        Button.horizontalAlignment HorizontalAlignment.Stretch
                        Button.horizontalContentAlignment HorizontalAlignment.Center
                    ]
                    Button.create [
                        Button.dock Dock.Bottom
                        Button.onClick (fun _ -> send "c")
                        Button.content "Clear sunflower"
                        Button.isEnabled currentSerialPort.Current.IsSome
                        Button.horizontalAlignment HorizontalAlignment.Stretch
                        Button.horizontalContentAlignment HorizontalAlignment.Center
                    ]
                    ComboBox.create [
                        ComboBox.dock Dock.Top
                        ComboBox.placeholderText "Select a COM port."
                        ComboBox.horizontalAlignment HorizontalAlignment.Stretch
                        ComboBox.horizontalContentAlignment HorizontalAlignment.Center
                        ComboBox.dataItems serialPorts.Current
                        ComboBox.onSelectedItemChanged (fun item -> currentSerialPort.Set(Some <| string item))
                    ]
                    Button.create [
                        Button.content (if isConnected.Current then "Disconnect" else "Connect")
                        Button.isEnabled currentSerialPort.Current.IsSome
                        Button.onClick (fun _ -> toggleConnection ())
                        if isConnected.Current then Button.background "Green"
                    ]
                    TextBlock.create [
                        TextBlock.dock Dock.Top
                        TextBlock.fontSize 48.0
                        TextBlock.verticalAlignment VerticalAlignment.Center
                        TextBlock.horizontalAlignment HorizontalAlignment.Center
                        TextBlock.text (currentSerialPort.Current |> Option.defaultValue "No Port")
                    ]
                ]
            ]
        )

type MainWindow() =
    inherit HostWindow()
    do
        base.Title <- "Counter Example"
        base.Content <- Main.view ()

type App() =
    inherit Application()

    override this.Initialize() =
        this.Styles.Add (FluentTheme())
        this.RequestedThemeVariant <- Styling.ThemeVariant.Dark

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktopLifetime ->
            desktopLifetime.MainWindow <- MainWindow()
        | _ -> ()

module Program =

    [<EntryPoint>]
    let main(args: string[]) =
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .UseSkia()
            .StartWithClassicDesktopLifetime(args)
