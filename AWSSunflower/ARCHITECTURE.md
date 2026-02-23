# AWS Sunflower - Refactored Architecture

## Overview
The application has been refactored from a monolithic single-file structure to a modular, component-based architecture using Avalonia FuncUI with the **lifted state pattern (Option A)**.

## Architecture

### File Structure

```
AWSSunflower/
├── Types.fs           (Domain types - 29 lines)
├── SerialPort.fs      (Serial port logic - 89 lines)
├── Components.fs      (UI components - 196 lines)
├── Program.fs         (Main view & app - 180 lines)
└── AWSSunflower.fsproj (Compilation order)
```

### Compilation Order
Files are compiled in dependency order:
1. `Types.fs` - Defines all domain types
2. `SerialPort.fs` - Uses types, provides serial logic
3. `Components.fs` - Uses types and imports for UI
4. `Program.fs` - Uses all modules, wires everything together

---

## Module Details

### 1. Types.fs
Defines core domain types:
- **SerialError** (discriminated union): Port errors, not found, open failed, send failed, disconnected
- **ConnectionState**: Disconnected | Connecting | Connected | Error
- **Toast**: Notification data with ID, message, error flag, creation time

**Why separate?** Types are the contract between all modules. Keeping them isolated prevents circular dependencies.

---

### 2. SerialPort.fs
Pure functions for serial port operations with **async workflows** for non-blocking UI:

#### Key Functions:
- `getAvailablePorts(): string list` - Sync helper
- `connectAsync(portName, baudRate): Async<Result<SerialPort, SerialError>>` 
  - Switches to thread pool (non-blocking)
  - Handles UnauthorizedAccessException (port in use), FileNotFoundException (not found)
  - Switches back to UI thread before returning
  
- `sendAsync(port, data): Async<Result<unit, SerialError>>`
  - Async version of WriteLine for non-blocking sends
  
- `disconnect(port option): unit` - Cleanup helper
  
- `startPortPolling(callback): IDisposable`
  - Timer that runs every 1000ms
  - Calls callback when port list changes
  - Returns disposable for cleanup

**Why async?** Serial operations can block. Async workflows keep UI responsive.

**Why Result type?** Explicit error handling: `Ok port | Error (PortInUse portName)`

---

### 3. Components.fs
Reusable UI components following **props-based pattern** (no local state):

#### Components:
1. **portSelector** - ComboBox for selecting COM ports
   - Receives: `ports list`, `selectedPort option`, `onSelectionChanged callback`
   
2. **connectionButton** - Dynamic button reflecting connection state
   - Color changes: Disconnected (white) → Connecting (orange) → Connected (green) → Error (red)
   - Text: "Connect", "Connecting...", "Disconnect"
   
3. **actionButtons** - "Set sunflower" / "Clear sunflower" buttons
   - Disabled when not connected
   
4. **portDisplay** - Large TextBlock showing current state
   - Color-coded status feedback
   
5. **errorToast** - Toast notifications (top-right corner)
   - Red background with error message and dismiss button
   - Auto-dismisses after 5 seconds
   
6. **mainLayout** - Orchestrates all components together

**Why component functions?** Composability. Each component is a pure function returning a UI element.

---

### 4. Program.fs
Main application logic orchestrating everything with **lifted state**:

#### State (all in Main.view):
```fsharp
let serialPorts = ctx.useState []              // Available COM ports
let selectedPort = ctx.useState None            // Currently selected port
let connectionState = ctx.useState Disconnected // Connection state (see Types)
let isConnecting = ctx.useState false           // Loading indicator during connect
let serialPortRef = ctx.useState(None, renderOnChange = false) // SerialPort ref (no re-render)
let toasts = ctx.useState []                   // Active toast notifications
```

#### Effects:
1. **Port Polling Effect** - Runs at init, manages timer cleanup
2. **Toast Auto-dismiss Effect** - Dismisses oldest toast after 5 seconds

#### Event Handlers:
1. **addToast** - Creates new toast with GUID
2. **dismissToast** - Removes toast by ID
3. **toggleConnection** - Async connection with error handling
4. **sendCommand** - Async send with error handling

#### Async Flow Example (toggleConnection):
```
User clicks → isConnecting = true
  ↓
Call connectAsync (runs on thread pool)
  ↓
port.Open() (blocks, but on thread pool, not UI thread)
  ↓
Switch back to UI thread
  ↓
Update state: serialPortRef, connectionState, isConnecting
  ↓
Show toast (success or error)
```

---

## State Management: Option A (Lifted State + Props)

### Why This Pattern?

| Aspect | Before | After |
|--------|--------|-------|
| State Location | Scattered across components | Centralized in Main |
| Data Flow | Unclear (multiple sources of truth) | Unidirectional (top → down) |
| Debugging | Hard to track state changes | Easy (all changes in Main) |
| Component Reuse | Difficult (coupled to state) | Easy (pure functions) |
| Testability | Hard (side effects everywhere) | Better (business logic separated) |

### Data Flow
```
Main.view (manages all state)
  ↓
  ├→ portSelector (receives ports[], callback)
  ├→ connectionButton (receives state, callback)
  ├→ actionButtons (receives state, callbacks)
  ├→ portDisplay (receives state)
  └→ errorToast (receives toasts[], callback)
```

Each component receives:
- **Data** (what to display)
- **Callbacks** (how to notify parent of changes)

### Advantages
✅ Single source of truth  
✅ Clear data flow (top-down only)  
✅ Easy to add features (new state + effect in Main)  
✅ Easy to debug (trace state changes in one place)  
✅ Scales well up to ~300 lines of state logic

### When to Upgrade to MVU (Option B)
- App grows beyond 400 lines in Main.view
- Complex state interactions become hard to follow
- Want formal message passing and time-travel debugging
- Multiple features competing for state updates

---

## Async/Await Patterns Used

### Pattern 1: Async Result
```fsharp
let! result = connectAsync portName 9600
match result with
| Ok port -> ...
| Error err -> ...
```
**What happens:**
- `connectAsync` returns `Async<Result<SerialPort, SerialError>>`
- `let!` unwraps the async
- `match` on the Result for success/error

### Pattern 2: Thread Pool Switching
```fsharp
do! Async.SwitchToThreadPool()
port.Open()  // Blocking operation on thread pool
do! Async.SwitchToContext(Avalonia.Threading.Dispatcher.UIThread)
// Safe to update UI state here
```
**Why?** Blocking serial operations don't freeze UI.

### Pattern 3: Async.Start
```fsharp
async { ... } |> Async.Start
```
**What?** Fire and forget. Useful for UI events that shouldn't block.

---

## Error Handling

All errors flow through:
1. **SerialError DU** (Types.fs) - Type-safe error representation
2. **Result type** in async functions - Explicit success/failure
3. **Pattern matching in Main** - Convert to user-friendly messages
4. **Toast notifications** - Display to user

Example:
```
PortInUse "COM3" → "Port COM3 is already in use" (toast)
Disconnected → "Port is disconnected" (toast)
```

---

## Future Enhancements

### Option B (Message Pattern) - When to use
If you want to consolidate further:
```fsharp
type Message =
  | SelectPort of string
  | Connect
  | Disconnect
  | SendCommand of string
  | ReceiveError of SerialError

let update message state =
  match message with
  | SelectPort p -> { state with selectedPort = Some p }
  | Connect -> // async logic here
  | ...
```

**Upgrade trigger:** Once Main.view exceeds 300 lines

### HTTP API Integration (Step 4)
Future `Api.fs` module:
```fsharp
namespace CounterApp
module ApiModule =
  let getEndpointsAsync(): Async<Result<Endpoint list, ApiError>>
  let pollEndpointAsync(url, interval): IDisposable
```
Would integrate with new UI components in Components.fs

---

## Quick Reference

| Need | File | Function |
|------|------|----------|
| Add new error type | Types.fs | SerialError DU |
| Add new serial operation | SerialPort.fs | New async function |
| Add new UI component | Components.fs | New function in Components module |
| Add new state field | Program.fs | New `ctx.useState` in Main.view |
| Add new event handler | Program.fs | New let-binding in Main.view |
| Add new effect | Program.fs | New `ctx.useEffect` in Main.view |

