# F# Idiomaticity Audit — AWSSunflower Application

**Date:** 2026-02-28  
**Author:** Tom Rolt (UI Dev)  
**Scope:** All files in AWSSunflower/ and AWSSunflower.Tests/  
**Stack:** .NET 10, F#, Avalonia 11.3, FuncUI 1.5.2, Elmish 4.3.0

---

## Finding 1: Side Effects in the Update Function (CRITICAL)

**Severity:** High — violates the core Elmish contract  
**Files:** ApplicationScreen/Update.fs, ApplicationScreen/Commands.fs

The `update` function performs side effects directly instead of returning them as `Cmd` values. This is the single biggest Elmish anti-pattern in the codebase. Six separate handlers call I/O, mutate refs, or start fire-and-forget asyncs inline.

### 1a. `Disconnect` calls `disposeSubscription()` directly

**Current (Update.fs:35):**
```fsharp
| Disconnect ->
    disposeSubscription ()
    { model with
        ApiConfig = None; ConnectionState = ApiConnectionState.Disconnected
        TreeRoot = []; SelectedNode = None
        EndpointValues = Map.empty; LastResponseTime = None
        CurrentLoco = None
        PollingValues = Map.empty },
    Cmd.none
```

**Idiomatic:**
```fsharp
| Disconnect ->
    { model with
        ApiConfig = None; ConnectionState = ApiConnectionState.Disconnected
        TreeRoot = []; SelectedNode = None
        EndpointValues = Map.empty; LastResponseTime = None
        CurrentLoco = None
        PollingValues = Map.empty },
    Cmd.ofEffect (fun _ -> disposeSubscription ())
```

### 1b. `BindEndpoint` calls `BindingPersistence.save` and mutates subscription

**Current (Update.fs:91–107):**
```fsharp
| BindEndpoint (nodePath, endpointName) ->
    match model.CurrentLoco, model.ApiConfig with
    | None, _ | _, None -> model, Cmd.none
    | Some locoName, Some config ->
        let binding = { NodePath = nodePath; EndpointName = endpointName; Label = endpointKey nodePath endpointName }
        let newConfig = BindingPersistence.addBinding model.BindingsConfig locoName binding
        BindingPersistence.save newConfig                   // ← side effect
        let newModel = { model with BindingsConfig = newConfig }
        let addr = { NodePath = nodePath; EndpointName = endpointName }
        match currentSubscription.Value with
        | Some sub ->
            sub.Add addr                                     // ← mutation
            newModel, Cmd.none
        | None ->
            let allBindings = getLocoBindings newConfig locoName
            newModel, createSubscriptionCmd config allBindings
```

**Idiomatic:**
```fsharp
| BindEndpoint (nodePath, endpointName) ->
    match model.CurrentLoco, model.ApiConfig with
    | None, _ | _, None -> model, Cmd.none
    | Some locoName, Some config ->
        let binding = { NodePath = nodePath; EndpointName = endpointName; Label = endpointKey nodePath endpointName }
        let newConfig = BindingPersistence.addBinding model.BindingsConfig locoName binding
        let newModel = { model with BindingsConfig = newConfig }
        let addr = { NodePath = nodePath; EndpointName = endpointName }
        let saveCmd = Cmd.ofEffect (fun _ -> BindingPersistence.save newConfig)
        let subCmd =
            match currentSubscription.Value with
            | Some sub -> Cmd.ofEffect (fun _ -> sub.Add addr)
            | None ->
                let allBindings = getLocoBindings newConfig locoName
                createSubscriptionCmd config allBindings
        newModel, Cmd.batch [ saveCmd; subCmd ]
```

**Why it's better:** The update function becomes a pure function of `msg → model → (model, Cmd)`. Side effects happen only when the Elmish runtime processes the Cmd. This makes the update function testable without triggering real I/O — tests currently can't exercise `BindEndpoint` without a real SQLite database.

### 1c. `UnbindEndpoint` calls save + mutates subscription inline

**Current (Update.fs:108–122):**
```fsharp
| UnbindEndpoint (nodePath, endpointName) ->
    match model.CurrentLoco with
    | Some locoName ->
        let newConfig = BindingPersistence.removeBinding model.BindingsConfig locoName nodePath endpointName
        BindingPersistence.save newConfig                   // ← side effect
        let key = endpointKey nodePath endpointName
        let addr = { NodePath = nodePath; EndpointName = endpointName }
        currentSubscription.Value |> Option.iter (fun sub ->  // ← mutation
            sub.Remove addr
            if sub.Endpoints.IsEmpty then disposeSubscription ())
        { model with ... }, resetSerialCmd model
    | None -> model, Cmd.none
```

**Idiomatic:**
```fsharp
| UnbindEndpoint (nodePath, endpointName) ->
    match model.CurrentLoco with
    | Some locoName ->
        let newConfig = BindingPersistence.removeBinding model.BindingsConfig locoName nodePath endpointName
        let key = endpointKey nodePath endpointName
        let addr = { NodePath = nodePath; EndpointName = endpointName }
        let saveCmd = Cmd.ofEffect (fun _ -> BindingPersistence.save newConfig)
        let subCmd = Cmd.ofEffect (fun _ ->
            currentSubscription.Value |> Option.iter (fun sub ->
                sub.Remove addr
                if sub.Endpoints.IsEmpty then disposeSubscription ()))
        { model with
            BindingsConfig = newConfig
            PollingValues = Map.remove key model.PollingValues },
        Cmd.batch [ saveCmd; subCmd; resetSerialCmd model ]
    | None -> model, Cmd.none
```

### 1d. `DisconnectSerial` and `ToggleSerialConnection` (connected branch)

**Current (Update.fs:171–173, 183–189):**
```fsharp
| DisconnectSerial ->
    SerialPortModule.disconnect model.SerialPort    // ← side effect
    { model with SerialPort = None }, Cmd.none

| ToggleSerialConnection ->
    match model.SerialConnectionState with
    | ConnectionState.Connected _ ->
        SerialPortModule.disconnect model.SerialPort    // ← side effect
        { model with ... }, Cmd.none
```

**Idiomatic:**
```fsharp
| DisconnectSerial ->
    { model with SerialPort = None },
    Cmd.ofEffect (fun _ -> SerialPortModule.disconnect model.SerialPort)

| ToggleSerialConnection ->
    match model.SerialConnectionState with
    | ConnectionState.Connected _ ->
        { model with
            SerialPort = None
            SerialConnectionState = ConnectionState.Disconnected
            SerialIsConnecting = false },
        Cmd.ofEffect (fun _ -> SerialPortModule.disconnect model.SerialPort)
```

### 1e. `SendSerialCommand` uses `Async.Start` inside update

**Current (Update.fs:214–222):**
```fsharp
| SendSerialCommand cmd ->
    match model.SerialPort with
    | Some port when port.IsOpen ->
        async {
            let! _ = SerialPortModule.sendAsync port cmd
            ()
        } |> Async.Start
        model, Cmd.none
    | _ -> model, Cmd.none
```

**Idiomatic:**
```fsharp
| SendSerialCommand cmd ->
    match model.SerialPort with
    | Some port when port.IsOpen ->
        model, Cmd.OfAsync.attempt (fun () -> SerialPortModule.sendAsync port cmd) () (fun _ -> ())
        // Or simpler, since we truly don't care about the result:
        model, Cmd.ofEffect (fun _ ->
            async { let! _ = SerialPortModule.sendAsync port cmd in () } |> Async.Start)
    | _ -> model, Cmd.none
```

**Why it's better:** All async work is expressed as Cmd values. The update function never launches background work itself — the Elmish runtime owns all side effects.

---

## Finding 2: Dead `Toast` Type

**Severity:** Low  
**File:** Types.fs:22–28

The `Toast` record type is still defined but was removed from the Model during the single-screen layout redesign (per history). Dead code.

**Current:**
```fsharp
/// Toast notification data
type Toast =
    {
        Id: Guid
        Message: string
        IsError: bool
        CreatedAt: DateTime
    }
```

**Idiomatic:** Delete it entirely. No code references it.

---

## Finding 3: `sprintf` → String Interpolation

**Severity:** Low — style consistency  
**Files:** Helpers.fs, StatusBar.fs, TreeBrowser.fs, EndpointViewer.fs, BindingsPanel.fs, SerialPortPanel.fs, PortDetection.fs, Commands.fs

F# supports `$"..."` interpolated strings and even `$"...%A{value}"` for structured formatting. 15+ `sprintf` calls should migrate.

### 3a. `endpointKey` (Helpers.fs:34)

**Current:**
```fsharp
let endpointKey nodePath endpointName = sprintf "%s.%s" nodePath endpointName
```

**Idiomatic:**
```fsharp
let endpointKey nodePath endpointName = $"{nodePath}.{endpointName}"
```

### 3b. StatusBar (StatusBar.fs:28–29)

**Current:**
```fsharp
| ApiConnectionState.Connected info -> sprintf "Status: Connected to %s (Build %d)" info.Meta.GameName info.Meta.GameBuildNumber
| ApiConnectionState.Error msg -> sprintf "Status: Error - %s" msg
```

**Idiomatic:**
```fsharp
| ApiConnectionState.Connected info -> $"Status: Connected to {info.Meta.GameName} (Build {info.Meta.GameBuildNumber})"
| ApiConnectionState.Error msg -> $"Status: Error - {msg}"
```

### 3c. StatusBar (StatusBar.fs:43)

**Current:**
```fsharp
TextBlock.text (sprintf "Last response: %.0fms" time.TotalMilliseconds)
```

**Idiomatic:**
```fsharp
TextBlock.text $"Last response: {time.TotalMilliseconds:F0}ms"
```

### 3d. StatusBar (StatusBar.fs:51)

**Current:**
```fsharp
TextBlock.text (sprintf "Loco: %s" loco)
```

**Idiomatic:**
```fsharp
TextBlock.text $"Loco: {loco}"
```

### 3e. TreeBrowser (TreeBrowser.fs:21)

**Current:**
```fsharp
Button.content (sprintf "%s %s" arrow node.Name)
```

**Idiomatic:**
```fsharp
Button.content $"{arrow} {node.Name}"
```

### 3f. EndpointViewer (EndpointViewer.fs:94–99)

**Current:**
```fsharp
TextBlock.text (sprintf "Node: %s" (nullSafe node.Name))
...
TextBlock.text (sprintf "Path: %s" (nullSafe node.Path))
```

**Idiomatic:**
```fsharp
TextBlock.text $"Node: {nullSafe node.Name}"
...
TextBlock.text $"Path: {nullSafe node.Path}"
```

### 3g. BindingsPanel (BindingsPanel.fs:23, 59)

**Current:**
```fsharp
TextBlock.text (sprintf "%s = %s" b.Label value)
...
TextBlock.text (sprintf "Active Bindings (%d)" currentBindings.Length)
```

**Idiomatic:**
```fsharp
TextBlock.text $"{b.Label} = {value}"
...
TextBlock.text $"Active Bindings ({currentBindings.Length})"
```

### 3h. SerialPortPanel (SerialPortPanel.fs:38–40)

**Current:**
```fsharp
| ConnectionState.Error (PortInUse p) -> sprintf "%s in use" p
| ConnectionState.Error (PortNotFound p) -> sprintf "%s missing" p
```

**Idiomatic:**
```fsharp
| ConnectionState.Error (PortInUse p) -> $"{p} in use"
| ConnectionState.Error (PortNotFound p) -> $"{p} missing"
```

### 3i. PortDetection.fs:152

**Current:**
```fsharp
|> Option.map (fun usb -> sprintf "%s — %s" port.PortName usb.Description)
```

**Idiomatic:**
```fsharp
|> Option.map (fun usb -> $"{port.PortName} — {usb.Description}")
```

### 3j. Commands.fs error formatting (lines 34, 47–48)

**Current:**
```fsharp
return (result |> okOrFail (sprintf "API error: %A"), elapsed)
...
|> okOrFail (sprintf "CommKey discovery failed: %A")
|> okOrFail (sprintf "Invalid configuration: %A")
```

**Idiomatic (using F# extended interpolation):**
```fsharp
return (result |> okOrFail (fun e -> $"API error: %A{e}"), elapsed)
...
|> okOrFail (fun e -> $"CommKey discovery failed: %A{e}")
|> okOrFail (fun e -> $"Invalid configuration: %A{e}")
```

Note: `okOrFail` takes `'E -> string`, but `sprintf "API error: %A"` creates a *curried* function `'a -> string`. With `$"..."`, you need an explicit lambda. Whether this is cleaner is debatable — the `sprintf` partial application is arguably more idiomatic F# for format-as-function. **Keep sprintf here** — it's the right tool when the format string IS the function.

**Why interpolation is better (for the simple cases):** `$"..."` is more readable, avoids counting `%s` placeholders, catches type errors at the binding site, and is the standard .NET style. F# 6+ supports it fully.

---

## Finding 4: Repeated `SolidColorBrush(Color.Parse(...))` Pattern

**Severity:** Medium — DRY violation in views  
**Files:** StatusBar.fs, TreeBrowser.fs, EndpointViewer.fs, BindingsPanel.fs, SerialPortPanel.fs

`AppColors` defines hex strings, but every usage wraps them in `SolidColorBrush(Color.Parse(...))`. This 40-character incantation appears 16+ times across view files.

**Current (from multiple files):**
```fsharp
SolidColorBrush(Color.Parse(AppColors.connected))
SolidColorBrush(Color.Parse(AppColors.error))
SolidColorBrush(Color.Parse(AppColors.warning))
SolidColorBrush(Color.Parse(AppColors.panelBg))
SolidColorBrush(Color.Parse(AppColors.border))
SolidColorBrush(Color.Parse(AppColors.info))
```

**Idiomatic — change AppColors to return brushes directly (Helpers.fs):**
```fsharp
module AppColors =
    let private brush hex = SolidColorBrush(Color.Parse(hex)) :> IBrush
    let connected = brush "#00AA00"
    let error     = brush "#FF5555"
    let warning   = brush "#FFAA00"
    let panelBg   = brush "#2A2A2A"
    let border    = brush "#3A3A3A"
    let info      = brush "#55AAFF"
```

Then every callsite simplifies from:
```fsharp
TextBlock.foreground (SolidColorBrush(Color.Parse(AppColors.connected)))
```
to:
```fsharp
TextBlock.foreground AppColors.connected
```

**Why it's better:** Eliminates 16+ repetitions of the parse-and-wrap pattern, reduces view noise significantly, and creates one place to change brush creation (e.g., if switching to `ImmutableSolidColorBrush`).

**Note:** Requires adding `open Avalonia.Media` to Helpers.fs. Since Helpers.fs compiles before all view files, this is fine in the dependency chain.

---

## Finding 5: `BindingsPanel` Reads Mutable `currentSubscription` in View

**Severity:** Medium — known MVU anti-pattern  
**File:** BindingsPanel.fs:65–73

The view reads `currentSubscription.Value` (a mutable ref from Commands.fs) to show "● Live" / "○ Idle". This bypasses the Elmish model — the UI won't re-render when the subscription state changes because Elmish doesn't know about it.

**Current:**
```fsharp
TextBlock.text (
    if currentSubscription.Value |> Option.map (fun s -> s.IsActive) |> Option.defaultValue false
    then "● Live"
    else "○ Idle"
)
```

**Idiomatic — add to Model (ApplicationScreen.fs):**
```fsharp
type Model =
    { // ... existing fields ...
      IsSubscriptionActive: bool }
```

Update the field when subscription state changes (in Update.fs — `BindEndpoint`, `UnbindEndpoint`, `LocoDetected`, `Disconnect`):
```fsharp
// After creating subscription:
{ newModel with IsSubscriptionActive = true }, subCmd
// After disposing:
{ model with IsSubscriptionActive = false }, ...
```

Then the view simply reads the model:
```fsharp
TextBlock.text (if model.IsSubscriptionActive then "● Live" else "○ Idle")
```

**Why it's better:** The view becomes a pure function of the model. Re-rendering happens automatically when the field changes. No mutable ref access in view code. Already noted as a future cleanup item in project history.

---

## Finding 6: `ensureInitialized` Could Use `Lazy<unit>`

**Severity:** Low  
**File:** BindingPersistence.fs:82–94

The double-checked locking pattern with a mutable bool is correct but verbose and non-idiomatic.

**Current:**
```fsharp
let private ensureInitialized =
    let mutable initialized = false
    let lockObj = obj()
    fun () ->
        if not initialized then
            lock lockObj (fun () ->
                if not initialized then
                    Directory.CreateDirectory(configDir) |> ignore
                    let dbExisted = File.Exists(dbPath)
                    use conn = openConnection()
                    ensureSchema conn
                    if not dbExisted then migrateFromJson conn
                    initialized <- true)
```

**Idiomatic:**
```fsharp
let private ensureInitialized =
    lazy (
        Directory.CreateDirectory(configDir) |> ignore
        let dbExisted = File.Exists(dbPath)
        use conn = openConnection()
        ensureSchema conn
        if not dbExisted then migrateFromJson conn
    )
```

Then callsites change from `ensureInitialized()` to `ensureInitialized.Force()`.

Or, if you prefer the unit-function calling convention:
```fsharp
let private ensureInitialized =
    let init = lazy (
        Directory.CreateDirectory(configDir) |> ignore
        let dbExisted = File.Exists(dbPath)
        use conn = openConnection()
        ensureSchema conn
        if not dbExisted then migrateFromJson conn
    )
    fun () -> init.Force()
```

**Why it's better:** `Lazy<T>` is thread-safe by default in .NET (uses `LazyThreadSafetyMode.ExecutionAndPublication`). Eliminates manual `lock`, mutable bool, and double-checked locking boilerplate. Standard .NET pattern for one-time initialization.

---

## Finding 7: `readAllFromDb` Uses Imperative Collections

**Severity:** Low  
**File:** BindingPersistence.fs:96–119

Uses `Dictionary<string, ResizeArray<BoundEndpoint>>` and `ResizeArray<string>` — C# patterns in F# code.

**Current:**
```fsharp
let private readAllFromDb (conn: SqliteConnection) : BindingsConfig =
    use cmd = conn.CreateCommand()
    cmd.CommandText <- "..."
    use reader = cmd.ExecuteReader()
    let locoMap = System.Collections.Generic.Dictionary<string, ResizeArray<BoundEndpoint>>()
    let locoOrder = ResizeArray<string>()
    while reader.Read() do
        let locoName = reader.GetString(0)
        if not (locoMap.ContainsKey locoName) then
            locoOrder.Add(locoName)
            locoMap.[locoName] <- ResizeArray<BoundEndpoint>()
        if not (reader.IsDBNull(1)) then
            locoMap.[locoName].Add({
                NodePath = reader.GetString(1)
                EndpointName = reader.GetString(2)
                Label = reader.GetString(3)
            })
    { Version = 1
      Locos = locoOrder |> Seq.map (fun name -> { LocoName = name; BoundEndpoints = locoMap.[name] |> Seq.toList }) |> Seq.toList }
```

**Idiomatic — use seq + groupBy:**
```fsharp
let private readAllFromDb (conn: SqliteConnection) : BindingsConfig =
    use cmd = conn.CreateCommand()
    cmd.CommandText <- """
        SELECT l.loco_name, b.node_path, b.endpoint_name, b.label
        FROM Locos l
        LEFT JOIN BoundEndpoints b ON b.loco_id = l.id
        ORDER BY l.id, b.id;
    """
    use reader = cmd.ExecuteReader()
    let rows = [
        while reader.Read() do
            let locoName = reader.GetString(0)
            let endpoint =
                if reader.IsDBNull(1) then None
                else Some {
                    NodePath = reader.GetString(1)
                    EndpointName = reader.GetString(2)
                    Label = reader.GetString(3)
                }
            locoName, endpoint
    ]
    { Version = 1
      Locos =
        rows
        |> List.groupBy fst
        |> List.map (fun (locoName, entries) ->
            { LocoName = locoName
              BoundEndpoints = entries |> List.choose snd }) }
```

**Why it's better:** Uses F# `[ while ... do yield ]` computation expression for the reader loop. `List.groupBy` replaces manual dictionary management. No mutable state. The `ORDER BY l.id` in SQL ensures deterministic loco ordering, and `List.groupBy` preserves first-occurrence order.

---

## Finding 8: `CounterApp` Namespace

**Severity:** Medium — technical debt  
**Files:** Every .fs file in AWSSunflower/ and AWSSunflower.Tests/

`CounterApp` is a leftover from the FuncUI project template. The app is called AWSSunflower.

**Impact Assessment:**
- 17 F# source files with `namespace CounterApp` or `open CounterApp.*`
- 5 test files with `open CounterApp` references
- fsproj `InternalsVisibleTo` attribute
- No public NuGet package, no external consumers

**Recommendation:** Rename to `AWSSunflower` namespace in a dedicated refactor PR. Use find-and-replace across all .fs files. Low risk since there are no external consumers, but it touches every file so should be its own PR with a clean test run.

---

## Finding 9: `ToggleExpand` Uses Recursive `update` Call

**Severity:** Low  
**File:** Update.fs:67–73

**Current:**
```fsharp
| ToggleExpand path ->
    match findNode path model.TreeRoot with
    | Some node when node.IsExpanded -> update (CollapseNode path) model
    | Some node when node.Children.IsNone -> update (ExpandNode path) model
    | Some _ ->
        { model with TreeRoot = updateTreeNode path (fun n -> { n with IsExpanded = true }) model.TreeRoot },
        Cmd.none
    | None -> model, Cmd.none
```

Recursive `update` calls are valid Elmish but obscure message flow for debugging/logging. The alternative is `Cmd.ofMsg`:

**Idiomatic (if you want message traceability):**
```fsharp
| ToggleExpand path ->
    match findNode path model.TreeRoot with
    | Some node when node.IsExpanded -> model, Cmd.ofMsg (CollapseNode path)
    | Some node when node.Children.IsNone -> model, Cmd.ofMsg (ExpandNode path)
    | Some _ ->
        { model with TreeRoot = updateTreeNode path (fun n -> { n with IsExpanded = true }) model.TreeRoot },
        Cmd.none
    | None -> model, Cmd.none
```

**Trade-off:** `Cmd.ofMsg` makes the message visible in Elmish devtools/logging. Direct recursive call is synchronous (no extra dispatch cycle). Both are valid — choose based on whether you want message traceability.

---

## Finding 10: `okOrFail` Pattern in Commands

**Severity:** Low — but worth noting  
**File:** Commands.fs:22–25

**Current:**
```fsharp
let okOrFail (formatMsg: 'E -> string) (r: Result<'T, 'E>) : 'T =
    match r with
    | Ok v -> v
    | Error e -> failwith (formatMsg e)
```

This converts `Result` errors into exceptions, which are then caught by `Cmd.OfAsync.either`'s error handler. It works but throws away the typed error.

**Alternative — use `Result.defaultWith` from FsToolkit.ErrorHandling (v5.2.0 is already a dependency of TSWApi):**
```fsharp
// In Commands.fs, if FsToolkit is available:
open FsToolkit.ErrorHandling

// Then in timedApiCall:
let! result = apiCall
let value = result |> Result.defaultWith (fun e -> failwith $"API error: %A{e}")
```

Actually, `Result.defaultWith` returns the error case's value — not quite right here. The existing pattern is fine. If we wanted to avoid exceptions entirely, we'd need to change the Cmd pipeline to use `Result` all the way through. That's a larger refactor.

**Verdict:** Keep `okOrFail` as-is. It's a pragmatic bridge between Result-based API code and exception-based Elmish commands.

---

## Finding 11: `isSerialConnected` Could Be a Property-Style Extension

**Severity:** Very low — style only  
**File:** Helpers.fs:42–43

**Current:**
```fsharp
let isSerialConnected (model: ApplicationScreen.Model) =
    match model.SerialConnectionState with ConnectionState.Connected _ -> true | _ -> false
```

**Alternative — active pattern:**
```fsharp
let (|SerialConnected|SerialDisconnected|) (model: ApplicationScreen.Model) =
    match model.SerialConnectionState with
    | ConnectionState.Connected _ -> SerialConnected
    | _ -> SerialDisconnected
```

**Verdict:** The current helper function is fine and clear. An active pattern would be overkill for a simple bool check. No change recommended.

---

## Finding 12: `ConnectionPanel` Duplicates `isEnabled` Logic

**Severity:** Low  
**File:** ConnectionPanel.fs:27, 35

**Current:**
```fsharp
TextBox.isEnabled (match model.ConnectionState with ApiConnectionState.Disconnected -> true | _ -> false)
// ... repeated for both TextBoxes
```

**Idiomatic — extract to a let binding:**
```fsharp
let connectionPanel (model: Model) (dispatch: Dispatch<Msg>) =
    let isDisconnected = model.ConnectionState = ApiConnectionState.Disconnected
    StackPanel.create [
        ...
        TextBox.isEnabled isDisconnected
        ...
        TextBox.isEnabled isDisconnected
```

**Why it's better:** Evaluates the match once, names the concept, reduces noise in the DSL block.

---

## Finding 13: `SerialPort.fs` — `sendAsync` Context Capture Risk

**Severity:** Medium  
**File:** SerialPort.fs:34

**Current:**
```fsharp
let sendAsync (port: IO.Ports.SerialPort) (data: string) : Async<Result<unit, SerialError>> =
    async {
        let uiContext = Threading.SynchronizationContext.Current    // ← captured here
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
```

If `sendAsync` is called from the Elmish `Cmd.OfAsync.either` pipeline (which runs on a thread pool thread), `SynchronizationContext.Current` will be `null`. The `Async.SwitchToContext(null)` call would then either no-op or throw.

In practice this is fine because:
1. The only caller in Update.fs uses `Async.Start` (which starts on the current context), AND
2. The function returns a `Result`, not updating any UI state

But it's fragile. Since `sendAsync` returns `Result` (no UI state mutation needed), the context switch back to UI thread is unnecessary.

**Idiomatic:**
```fsharp
let sendAsync (port: IO.Ports.SerialPort) (data: string) : Async<Result<unit, SerialError>> =
    async {
        try
            if not port.IsOpen then
                return Error Disconnected
            else
                do! Async.SwitchToThreadPool()
                port.WriteLine data
                return Ok ()
        with
        | ex ->
            return Error (SendFailed ex.Message)
    }
```

**Why it's better:** Removes unnecessary context switch. The Result is consumed by the caller who is responsible for any UI thread marshalling.

---

## Finding 14: `ComboBox.onSelectedItemChanged` Fragile Cast

**Severity:** Medium  
**File:** SerialPortPanel.fs:81–87

**Current:**
```fsharp
ComboBox.onSelectedItemChanged (fun item ->
    let displayName = string item    // ← item is obj, could be null
    if String.IsNullOrEmpty displayName then dispatch (SetSerialPort None)
    else
        let port = model.DetectedPorts |> List.tryFind (fun p -> portDisplayName p = displayName)
        dispatch (SetSerialPort (port |> Option.map (fun p -> p.PortName)))
)
```

`string null` returns `""` in F#, so it works, but `item` being `obj` from the Avalonia event is fragile.

**Idiomatic:**
```fsharp
ComboBox.onSelectedItemChanged (fun item ->
    match item with
    | null -> dispatch (SetSerialPort None)
    | :? string as displayName when not (String.IsNullOrEmpty displayName) ->
        let port = model.DetectedPorts |> List.tryFind (fun p -> portDisplayName p = displayName)
        dispatch (SetSerialPort (port |> Option.map _.PortName))
    | _ -> dispatch (SetSerialPort None)
)
```

**Why it's better:** Type-safe pattern match on the obj. Handles null explicitly. Uses F# 8 `_.PortName` shorthand lambda.

---

## Finding 15: `LocoDetected` — Long Handler Could Use Local Helpers

**Severity:** Low  
**File:** Update.fs:129–153

This is the longest handler in update at 25 lines with a `Cmd.batch` containing a match expression. It's readable but could be tightened.

**Current:**
```fsharp
| LocoDetected locoName ->
    if model.CurrentLoco = Some locoName then
        model, Cmd.none
    else
        disposeSubscription ()
        let newConfig = BindingPersistence.load ()
        ...
        Cmd.batch [
            match model.ApiConfig with
            | Some config -> loadRootNodesCmd config
            | None -> Cmd.none
            resetSerialCmd model
            subCmd
        ]
```

**Idiomatic — using an `Option.toCmd` helper and moving side effects to Cmd:**
```fsharp
| LocoDetected locoName when model.CurrentLoco = Some locoName ->
    model, Cmd.none

| LocoDetected locoName ->
    let newConfig = BindingPersistence.load ()
    let locoBindings = getLocoBindings newConfig locoName
    let loadTreeCmd =
        model.ApiConfig
        |> Option.map loadRootNodesCmd
        |> Option.defaultValue Cmd.none
    let subCmd =
        match model.ApiConfig with
        | Some config when not locoBindings.IsEmpty -> createSubscriptionCmd config locoBindings
        | _ -> Cmd.none
    { model with
        CurrentLoco = Some locoName
        BindingsConfig = newConfig
        PollingValues = Map.empty
        TreeRoot = []; SelectedNode = None
        EndpointValues = Map.empty },
    Cmd.batch [
        Cmd.ofEffect (fun _ -> disposeSubscription ())
        Cmd.ofEffect (fun _ -> ())  // BindingPersistence.load is already called above (pure read)
        loadTreeCmd
        resetSerialCmd model
        subCmd
    ]
```

The `when` guard on the first case eliminates one nesting level.

---

## Summary of Priorities

| # | Finding | Severity | Effort | Recommendation |
|---|---------|----------|--------|----------------|
| 1 | Side effects in update | High | Medium | Wrap all I/O in `Cmd.ofEffect` |
| 5 | Mutable ref in view | Medium | Low | Add `IsSubscriptionActive` to Model |
| 4 | Repeated brush creation | Medium | Low | Change AppColors to return `IBrush` |
| 13 | `sendAsync` context risk | Medium | Low | Remove unnecessary context switch |
| 14 | ComboBox fragile cast | Medium | Low | Pattern match on obj |
| 8 | CounterApp namespace | Medium | Medium | Rename to AWSSunflower |
| 3 | `sprintf` → `$""` | Low | Low | Batch find-replace |
| 2 | Dead Toast type | Low | Trivial | Delete |
| 6 | `ensureInitialized` Lazy | Low | Low | Use `Lazy<unit>` |
| 7 | Imperative readAllFromDb | Low | Low | Use groupBy pattern |
| 9 | Recursive update call | Low | Trivial | Consider Cmd.ofMsg |
| 12 | Duplicated isEnabled | Low | Trivial | Extract let binding |
| 15 | Long LocoDetected | Low | Low | When guard + helpers |
| 10 | okOrFail pattern | Low | N/A | Keep as-is |
| 11 | isSerialConnected | Very Low | N/A | Keep as-is |

---

## Recommended Implementation Order

1. **Finding 1** (side effects in update) — highest impact, makes update testable
2. **Finding 5** (IsSubscriptionActive in model) — eliminates the known MVU anti-pattern
3. **Finding 4** (AppColors as brushes) — reduces view noise across 6 files
4. **Finding 3** (sprintf → interpolation) — easy batch change, do alongside other edits
5. **Finding 2** (delete Toast) — trivial cleanup
6. **Finding 8** (namespace rename) — do as separate PR, touches every file
