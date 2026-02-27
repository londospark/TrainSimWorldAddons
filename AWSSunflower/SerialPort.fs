namespace CounterApp

open System

module SerialPortModule =

    /// Get list of available COM ports
    let getAvailablePorts () : string list =
        IO.Ports.SerialPort.GetPortNames() |> List.ofArray

    /// Connect to a serial port asynchronously
    let connectAsync (portName: string) (baudRate: int) : Async<Result<IO.Ports.SerialPort, SerialError>> =
        async {
            let uiContext = Threading.SynchronizationContext.Current
            try
                let port = new IO.Ports.SerialPort()
                port.PortName <- portName
                port.BaudRate <- baudRate
                port.Handshake <- IO.Ports.Handshake.None
                port.ReadTimeout <- 1000
                port.WriteTimeout <- 1000
                
                do! Async.SwitchToThreadPool()
                port.Open()
                do! Async.SwitchToContext(uiContext)
                
                return Ok port
            with
            | :? UnauthorizedAccessException ->
                return Error (PortInUse portName)
            | :? System.IO.FileNotFoundException ->
                return Error (PortNotFound portName)
            | ex ->
                return Error (OpenFailed ex.Message)
        }

    /// Send data over serial port asynchronously
    let sendAsync (port: IO.Ports.SerialPort) (data: string) : Async<Result<unit, SerialError>> =
        async {
            let uiContext = Threading.SynchronizationContext.Current
            try
                if not port.IsOpen then
                    return Error Disconnected
                else
                    do! Async.SwitchToThreadPool()
                    port.WriteLine data
                    do! Async.SwitchToContext(uiContext)
                    return Ok ()
            with
            | ex ->
                return Error (SendFailed ex.Message)
        }

    /// Disconnect and clean up a serial port
    let disconnect (port: IO.Ports.SerialPort option) : unit =
        match port with
        | Some port when port.IsOpen ->
            try
                port.Close()
            with _ -> ()
            port.Dispose()
        | Some port ->
            port.Dispose()
        | None -> ()

    /// Start polling for available ports, calling the callback when the list changes
    let startPortPolling (onUpdate: string list -> unit) : IDisposable =
        let timer = Avalonia.Threading.DispatcherTimer()
        timer.Interval <- TimeSpan.FromMilliseconds 1000.0
        
        let mutable lastPorts = []
        
        timer.Tick.Add(fun _ ->
            let currentPorts = getAvailablePorts ()
            if currentPorts <> lastPorts then
                lastPorts <- currentPorts
                onUpdate currentPorts
        )
        
        timer.Start()
        
        { new IDisposable with
            member _.Dispose() =
                timer.Stop()
        }


