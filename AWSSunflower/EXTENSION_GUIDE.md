# Extension Guide: How to Add Features

This guide shows practical examples of adding new features to your refactored app.

---

## Example 1: Add Baud Rate Selection

### Step 1: Add to Types.fs
```fsharp
// Add to end of Types.fs
type BaudRate = 
    | Rate9600 = 9600
    | Rate19200 = 19200
    | Rate38400 = 38400
    | Rate57600 = 57600
```

### Step 2: Update SerialPort.fs
```fsharp
// In SerialPortModule, update connectAsync signature:
let connectAsync (portName: string) (baudRate: int) : Async<Result<SerialPort, SerialError>> =
    // Already flexible! Just pass the int
```

### Step 3: Add Component in Components.fs
```fsharp
// Add new component function
let baudRateSelector (selectedBaud: int) (onBaudChanged: int -> unit) =
    ComboBox.create [
        ComboBox.dock Dock.Top
        ComboBox.placeholderText "Select Baud Rate"
        ComboBox.dataItems [9600; 19200; 38400; 57600]
        ComboBox.selectedItem selectedBaud
        ComboBox.onSelectedItemChanged (fun item ->
            onBaudChanged (unbox<int> item)
        )
    ]

// Update mainLayout signature to include this
```

### Step 4: Update Program.fs Main.view
```fsharp
let selectedBaudRate = ctx.useState 9600  // Add state

// In toggleConnection, use selected baud rate:
let! result = connectAsync portName selectedBaudRate.Current

// When calling mainLayout, pass:
baudRateSelector selectedBaudRate.Current (fun baud -> selectedBaudRate.Set baud)
```

---

## Example 2: Add Command History

### Step 1: Update Types.fs
```fsharp
type CommandRecord = {
    Command: string
    Timestamp: DateTime
    Status: Result<unit, string>
}
```

### Step 2: Update Program.fs
```fsharp
let commandHistory = ctx.useState []  // Add to state

let sendCommand cmd =
    match serialPortRef.Current with
    | Some port ->
        async {
            let! result = sendAsync port cmd
            match result with
            | Ok () ->
                let record: CommandRecord = {
                    Command = cmd
                    Timestamp = DateTime.Now
                    Status = Ok ()
                }
                commandHistory.Set (commandHistory.Current @ [record])
                addToast $"Sent: {cmd}" false
            | Error error ->
                let record: CommandRecord = {
                    Command = cmd
                    Timestamp = DateTime.Now
                    Status = Error (error.ToString())
                }
                commandHistory.Set (commandHistory.Current @ [record])
                addToast errorMsg true
        } |> Async.Start
    | None -> ()
```

### Step 3: Add UI Component in Components.fs
```fsharp
let commandHistoryPanel (commands: CommandRecord list) =
    ListBox.create [
        ListBox.dock Dock.Right
        ListBox.width 250.0
        ListBox.dataItems commands
        ListBox.itemTemplate (
            DataTemplate(fun (cmd: CommandRecord) ->
                TextBlock.create [
                    TextBlock.text (sprintf "[%s] %s" 
                        (cmd.Timestamp.ToString("HH:mm:ss")) 
                        cmd.Command)
                    TextBlock.foreground (
                        match cmd.Status with
                        | Ok () -> SolidColorBrush(Color.Parse "#00AA00")
                        | Error _ -> SolidColorBrush(Color.Parse "#FF5555")
                    )
                ] :> obj
            )
        )
    ]
```

---

## Example 3: Add Connection Timeout

### Step 1: Update SerialPort.fs
```fsharp
let connectAsyncWithTimeout (portName: string) (baudRate: int) (timeoutMs: int) : 
    Async<Result<SerialPort, SerialError>> =
    async {
        try
            let port = new SerialPort()
            port.PortName <- portName
            port.BaudRate <- baudRate
            port.Handshake <- Handshake.None
            
            do! Async.SwitchToThreadPool()
            
            // Wrap with timeout
            let connectAsync = async {
                port.Open()
                return Ok port
            }
            
            let! result = Async.Catch(
                Async.WithTimeout(timeoutMs, connectAsync)
            )
            
            do! Async.SwitchToContext(Avalonia.Threading.Dispatcher.UIThread)
            
            return result
        with
        | :? TimeoutException -> 
            return Error (OpenFailed "Connection timeout")
        | :? UnauthorizedAccessException ->
            return Error (PortInUse portName)
        | ex ->
            return Error (OpenFailed ex.Message)
    }
```

### Step 2: Use in Program.fs
```fsharp
async {
    let! result = connectAsyncWithTimeout portName 9600 5000  // 5 second timeout
    // Rest of logic same
} |> Async.Start
```

---

## Example 4: Add Receive/Read Data

### Step 1: Update Types.fs
```fsharp
type ReceivedMessage = {
    Data: string
    ReceivedAt: DateTime
}
```

### Step 2: Update SerialPort.fs
```fsharp
let startReadingAsync (port: SerialPort) (onDataReceived: string -> unit) : IDisposable =
    let cts = new System.Threading.CancellationTokenSource()
    let token = cts.Token
    
    Async.Start(
        async {
            while not token.IsCancellationRequested do
                try
                    if port.IsOpen && port.BytesToRead > 0 then
                        let line = port.ReadLine()
                        do! Async.SwitchToContext(Avalonia.Threading.Dispatcher.UIThread)
                        onDataReceived line
                        do! Async.SwitchToThreadPool()
                    else
                        do! Async.Sleep 100
                with
                | _ -> ()
        },
        token
    )
    
    { new IDisposable with
        member _.Dispose() =
            cts.Cancel()
            cts.Dispose()
    }
```

### Step 3: Use in Program.fs
```fsharp
let receivedMessages = ctx.useState []
let readingHandle = ctx.useState(None, renderOnChange = false)

// When connected:
let handle = startReadingAsync port (fun data ->
    let msg: ReceivedMessage = {
        Data = data
        ReceivedAt = DateTime.Now
    }
    receivedMessages.Set (receivedMessages.Current @ [msg])
)
readingHandle.Set (Some handle)

// On disconnect:
readingHandle.Current |> Option.iter (fun h -> h.Dispose())
readingHandle.Set None
```

---

## Example 5: Add Logging to File

### Step 1: Add to Types.fs
```fsharp
type LogLevel = Info | Warning | Error
type LogEntry = {
    Level: LogLevel
    Message: string
    Timestamp: DateTime
}
```

### Step 2: Create Logger Module in new file Logger.fs
```fsharp
namespace CounterApp

open System
open System.IO

module Logger =
    let logFile = "./logs.txt"
    
    let ensureLogDir () =
        let dir = Path.GetDirectoryName logFile
        if not (Directory.Exists dir) then
            Directory.CreateDirectory dir |> ignore
    
    let log (level: LogLevel) (message: string) =
        ensureLogDir ()
        let entry = sprintf "[%s] %s - %s" 
            (DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
            (match level with Info -> "INFO" | Warning -> "WARN" | Error -> "ERROR")
            message
        File.AppendAllText(logFile, entry + Environment.NewLine)
```

### Step 3: Use in Program.fs
```fsharp
open CounterApp.Logger

// In toggleConnection:
Logger.log Info $"Attempting to connect to {portName}"

// On success:
Logger.log Info $"Successfully connected to {portName}"

// On error:
Logger.log Error $"Failed to connect: {errorMsg}"
```

### Step 4: Update fsproj (add Logger.fs before Program.fs)
```xml
<ItemGroup>
    <Compile Include="Types.fs" />
    <Compile Include="SerialPort.fs" />
    <Compile Include="Logger.fs" />
    <Compile Include="Components.fs" />
    <Compile Include="Program.fs"/>
</ItemGroup>
```

---

## Example 6: When to Upgrade to MVU (Option B)

You'll know it's time when your `Main.view` starts looking like this:

```fsharp
let serialPorts = ctx.useState []
let selectedPort = ctx.useState None
let connectionState = ctx.useState Disconnected
let isConnecting = ctx.useState false
let serialPortRef = ctx.useState(None, renderOnChange = false)
let toasts = ctx.useState []
let selectedBaudRate = ctx.useState 9600
let commandHistory = ctx.useState []
let receivedMessages = ctx.useState []
let isLogging = ctx.useState false
let logFilter = ctx.useState Info
// ... more state ...
```

**Signs it's time to refactor to MVU:**
- More than 10 state variables
- Multiple related state updates (e.g., when connect succeeds, update 4 different states)
- Hard to track which handlers update which state
- Want to add undo/redo functionality

**Simple upgrade path:**
```fsharp
type Message =
    | SelectPort of string
    | SetBaudRate of int
    | Connect
    | Disconnect
    | SendCommand of string
    | AddToast of string * bool
    | DismissToast of Guid
    | UpdatePorts of string list
    | SetConnectionState of ConnectionState
    // ... more messages ...

type State = {
    serialPorts: string list
    selectedPort: string option
    connectionState: ConnectionState
    // ... all current state fields ...
}

let update message state =
    match message with
    | SelectPort p -> { state with selectedPort = Some p }
    | SetBaudRate b -> { state with selectedBaudRate = b }
    | Connect -> // async logic returns new state
    // ...

let view state dispatch =
    // Components now dispatch messages instead of calling handlers
```

But **don't upgrade prematurely**. The current lifted state pattern is perfect for the scope you're at now.

---

## Testing Your Extensions

Once you add features, verify:

```bash
cd X:\Code\TrainSimWorldAddons\AWSSunflower
dotnet build
# Should succeed with no errors

# Run the app:
dotnet run
```

Key things to test:
- ✅ Port selection and connection
- ✅ Sending commands
- ✅ Error messages appear as toasts
- ✅ Toasts auto-dismiss after 5 seconds
- ✅ Disconnect works properly
- ✅ No UI freezes during async operations

