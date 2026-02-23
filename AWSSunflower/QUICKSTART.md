# Quick Start Guide

## What Was Refactored

Your monolithic `Program.fs` has been split into a clean, modular architecture:

### Before
```
Program.fs (157 lines)
├── Serial port management (mixed with UI)
├── State management (scattered)
└── UI (all components together)
```

### After
```
Types.fs (29 lines) ────────────── Domain types (SerialError, ConnectionState, Toast)
    ↓
SerialPort.fs (89 lines) ──────── Business logic (async serial operations)
    ↓
Components.fs (196 lines) ──────── UI components (portSelector, connectionButton, etc.)
    ↓
Program.fs (180 lines) ────────── Main orchestration (lifted state + effects)
```

---

## Key Improvements

| Aspect | Before | After |
|--------|--------|-------|
| **Testability** | Hard (everything intertwined) | Easy (pure functions, separated logic) |
| **Reusability** | Components tied to state | Components are pure functions |
| **Debugging** | State scattered everywhere | Single source of truth in Main |
| **Async** | Blocking `port.Open()` | Non-blocking async workflows |
| **Error Handling** | Try/catch, printfn | Type-safe Result + Toasts |
| **UI Feedback** | Console output only | Toast notifications |

---

## State Management Pattern: Lifted State (Option A)

```fsharp
Main.view()
  │
  ├─ State: serialPorts, selectedPort, connectionState, isConnecting, toasts
  │
  ├─ Effects: Port polling, Toast auto-dismiss
  │
  ├─ Handlers: toggleConnection, sendCommand, addToast, dismissToast
  │
  └─ Render: mainLayout
      ├─ portSelector       (receives: ports, selected, callback)
      ├─ connectionButton   (receives: state, isConnecting, callback)
      ├─ actionButtons      (receives: isConnected, callbacks)
      ├─ portDisplay        (receives: connectionState)
      └─ errorToast         (receives: toasts[], dismissCallback)
```

**Why this is good:**
- Single source of truth (Main manages all state)
- Unidirectional data flow (parent → children)
- Easy to add features (new state + effect in Main)
- Ready to scale to MVU when needed

---

## Building & Running

```bash
# Build the project
cd X:\Code\TrainSimWorldAddons\AWSSunflower
dotnet build

# Run it
dotnet run
```

---

## File Purposes

### `Types.fs`
Defines the domain types that all other modules depend on:
- `SerialError` - All possible serial port errors
- `ConnectionState` - Current connection status
- `Toast` - Notification data

**When to edit:** Add new error types, new states

### `SerialPort.fs`
Pure, testable functions for serial port operations:
- `connectAsync` - Non-blocking connection
- `sendAsync` - Non-blocking send
- `startPortPolling` - Automatically updates available ports

**When to edit:** Add new serial operations, change polling interval

### `Components.fs`
Reusable UI components as pure functions:
- `portSelector` - Choose port
- `connectionButton` - Connect/disconnect with dynamic styling
- `actionButtons` - Set/clear sunflower
- `portDisplay` - Status display
- `errorToast` - Error notifications
- `mainLayout` - Orchestrates all above

**When to edit:** Change UI appearance, add new components, reorder elements

### `Program.fs`
Main application logic:
- `Main.view()` - Component with all state and effects
- `toggleConnection()` - Async connection handler
- `sendCommand()` - Async send handler
- App setup (MainWindow, App class, Program entry point)

**When to edit:** Add state, add effects, add event handlers, change app setup

---

## Common Tasks

### Add a New UI Component

1. Create function in `Components.fs`:
   ```fsharp
   let myNewComponent (data: string) (onAction: unit -> unit) =
       Button.create [
           Button.content data
           Button.onClick (fun _ -> onAction ())
       ]
   ```

2. Add to `mainLayout` function signature and rendering

3. Add state in `Program.fs` Main.view if needed:
   ```fsharp
   let myState = ctx.useState "initial"
   ```

4. Wire up in `mainLayout` call:
   ```fsharp
   myNewComponent myState.Current (fun () -> myState.Set "new")
   ```

### Add a New State Variable

1. Add to Main.view in Program.fs:
   ```fsharp
   let myNewState = ctx.useState defaultValue
   ```

2. Create handler (if needed):
   ```fsharp
   let myHandler =
       // ...update state or call async
   ```

3. Pass to components or use in effects

### Add a New Effect

1. In Main.view, after other effects:
   ```fsharp
   ctx.useEffect(
       handler = (fun _ ->
           // Setup code
           { new IDisposable with
               member _.Dispose() =
                   // Cleanup code
           }
       ),
       triggers = [ EffectTrigger.AfterInit ]
   )
   ```

Triggers:
- `EffectTrigger.AfterInit` - Once, at component init
- `EffectTrigger.AfterRender [ box someState ]` - When someState changes
- `EffectTrigger.Never` - Never (useful for setup)

### Add Error Handling to a New Operation

1. Create result-returning async function in `SerialPort.fs`:
   ```fsharp
   let myOperationAsync () : Async<Result<Success, SerialError>> =
       async {
           try
               // ... do work ...
               return Ok result
           with
           | specificException -> return Error (SomeError "message")
       }
   ```

2. Call and handle in Program.fs:
   ```fsharp
   async {
       let! result = myOperationAsync ()
       match result with
       | Ok value -> 
           // Success - update state
           addToast "Success!" false
       | Error error ->
           // Error - show user
           let msg = match error with
                     | ErrorType1 -> "Error message 1"
                     | ErrorType2 -> "Error message 2"
           addToast msg true
   } |> Async.Start
   ```

3. Toast automatically displays and auto-dismisses after 5 seconds

---

## Next Steps

### Immediate (Ready Now)
- ✅ Build and run the app
- ✅ Test port selection, connect/disconnect, send commands
- ✅ Test error handling (try invalid port)
- ✅ Verify toasts appear and auto-dismiss

### Near-Term (Following This Pattern)
- Add baud rate selection (see EXTENSION_GUIDE.md Example 1)
- Add command history (see EXTENSION_GUIDE.md Example 2)
- Add connection timeout (see EXTENSION_GUIDE.md Example 3)

### When Ready for HTTP API
- Create `Api.fs` module with HTTP client functions
- Add API components to `Components.fs`
- Add API state to Main.view in Program.fs
- Call API in async workflows with error handling
- Display API data in components

---

## Documentation Files

- **ARCHITECTURE.md** - Deep dive into design decisions and patterns
- **EXTENSION_GUIDE.md** - Concrete examples of adding features
- **This file** - Quick reference and common tasks

---

## Code Statistics

| File | Lines | Purpose |
|------|-------|---------|
| Types.fs | 29 | Domain types |
| SerialPort.fs | 89 | Business logic |
| Components.fs | 196 | UI components |
| Program.fs | 180 | Main logic + app setup |
| **Total** | **494** | Clean, modular, testable |

---

## Key Concepts

### Lifted State
State lives in parent (Main), children are pure functions receiving data + callbacks.

### Result Type
`Result<'Success, 'Error>` makes errors explicit: `Ok value | Error error`

### Async Workflows
Non-blocking operations: `async { ... } |> Async.Start` runs on thread pool without blocking UI

### Effect Cleanup
Every effect returns `IDisposable` for cleanup (timers, subscriptions, etc.)

### Component as Functions
Components are pure functions: `(data) -> (callbacks) -> UI element`

---

## Troubleshooting

### Build fails: "Type 'XYZ' not found"
- Check file compilation order in .fsproj (Types → SerialPort → Components → Program)
- Verify `open` statements include needed namespaces

### UI freezes when connecting
- Already handled! `connectAsync` uses `Async.SwitchToThreadPool()`
- If happens anyway, check that you're using async functions in SerialPort.fs

### Toast doesn't appear
- Verify `addToast` is called with message
- Check it's not called inside a try/catch that swallows errors

### Changes don't take effect
- Run `dotnet clean && dotnet build`
- Restart the app

---

## Questions?

Refer to:
- **How does X work?** → ARCHITECTURE.md
- **How do I add Y?** → EXTENSION_GUIDE.md
- **What's the syntax for Z?** → Look at existing code in Components.fs or Program.fs

