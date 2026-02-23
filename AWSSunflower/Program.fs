namespace CounterApp

open System
open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Themes.Fluent
open Avalonia.FuncUI.Hosts
open Avalonia.Controls
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.Layout
open Avalonia.Threading
open CounterApp.SerialPortModule
open CounterApp.Components

module Main =

    let view () =
        Component(fun ctx ->
            // State management (lifted state pattern)
            let serialPorts = ctx.useState []
            let selectedPort = ctx.useState None
            let connectionState = ctx.useState ConnectionState.Disconnected
            let isConnecting = ctx.useState false
            let serialPortRef = ctx.useState(None, renderOnChange = false)
            let toasts = ctx.useState []
            
            // Port polling setup
            ctx.useEffect(
                handler = (fun _ ->
                    let polling = startPortPolling (fun ports ->
                        serialPorts.Set ports
                    )
                    
                    { new IDisposable with
                        member _.Dispose() =
                            polling.Dispose()
                    }
                ),
                triggers = [ EffectTrigger.AfterInit ]
            )
            
            // Auto-dismiss toasts after 5 seconds
            ctx.useEffect(
                handler = (fun _ ->
                    if toasts.Current.Length > 0 then
                        let timer = DispatcherTimer()
                        timer.Interval <- TimeSpan.FromSeconds 5.0
                        timer.Tick.Add(fun _ ->
                            if toasts.Current.Length > 0 then
                                let remaining = toasts.Current |> List.tail
                                toasts.Set remaining
                                if remaining.Length = 0 then
                                    timer.Stop()
                        )
                        timer.Start()
                        { new IDisposable with
                            member _.Dispose() = timer.Stop()
                        }
                    else
                        { new IDisposable with
                            member _.Dispose() = ()
                        }
                ),
                triggers = [ EffectTrigger.AfterChange toasts ]
            )
            
            /// Add a toast notification
            let addToast message isError =
                let newToast: Toast = {
                    Id = Guid.NewGuid()
                    Message = message
                    IsError = isError
                    CreatedAt = DateTime.Now
                }
                toasts.Set (toasts.Current @ [newToast])
            
            /// Dismiss a specific toast
            let dismissToast (id: Guid) =
                toasts.Set (toasts.Current |> List.filter (fun t -> t.Id <> id))
            
            /// Toggle connection to serial port
            let toggleConnection () =
                match connectionState.Current, selectedPort.Current with
                | ConnectionState.Connected _, _ ->
                    // Disconnect
                    isConnecting.Set false
                    serialPortRef.Set None |> ignore
                    connectionState.Set ConnectionState.Disconnected
                    addToast "Disconnected from port" false
                    
                | ConnectionState.Disconnected, Some portName ->
                    // Connect
                    isConnecting.Set true
                    async {
                        let! result = connectAsync portName 9600
                        match result with
                        | Ok port ->
                            serialPortRef.Set (Some port)
                            connectionState.Set (ConnectionState.Connected portName)
                            isConnecting.Set false
                            addToast $"Connected to {portName}" false
                        | Error error ->
                            connectionState.Set (ConnectionState.Error error)
                            isConnecting.Set false
                            let errorMsg =
                                match error with
                                | PortInUse portName -> $"Port {portName} is already in use"
                                | PortNotFound portName -> $"Port {portName} not found"
                                | OpenFailed msg -> $"Failed to open port: {msg}"
                                | SendFailed msg -> $"Send failed: {msg}"
                                | Disconnected -> "Port disconnected"
                            addToast errorMsg true
                    } |> Async.StartImmediate
                    
                | _ -> ()
            
            /// Send command over serial port
            let sendCommand cmd =
                match serialPortRef.Current with
                | Some port ->
                    async {
                        let! result = sendAsync port cmd
                        match result with
                        | Ok () ->
                            addToast $"Sent: {cmd}" false
                        | Error error ->
                            let errorMsg =
                                match error with
                                | SendFailed msg -> $"Send failed: {msg}"
                                | Disconnected -> "Port is disconnected"
                                | _ -> "Unknown error"
                            addToast errorMsg true
                            connectionState.Set ConnectionState.Disconnected
                    } |> Async.StartImmediate
                | None -> ()
            
            // Render main layout with all components
            mainLayout
                serialPorts.Current
                connectionState.Current
                isConnecting.Current
                toasts.Current
                (fun port -> selectedPort.Set port)
                toggleConnection
                (fun () -> sendCommand "s")
                (fun () -> sendCommand "c")
                dismissToast
        )

type MainWindow() =
    inherit HostWindow()
    do
        base.Title <- "AWS Sunflower"
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
