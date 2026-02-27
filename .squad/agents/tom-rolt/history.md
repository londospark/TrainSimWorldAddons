# Tom Rolt — History

## Project Context
- **Project:** TrainSimWorldAddons
- **Stack:** F#, .NET 10, Avalonia 11.3 Desktop, FuncUI 1.5.2
- **User:** LondoSpark
- **Role:** UI Dev — building Avalonia FuncUI interfaces for the AWSSunflower app

## Key Files
- AWSSunflower/Program.fs — Main window, app entry point
- AWSSunflower/Components.fs — FuncUI component functions (portSelector, connectionButton, etc.)
- AWSSunflower/Types.fs — App types (ConnectionState DU, SerialError DU, Toast record)
- AWSSunflower/AWSSunflower.fsproj — Project file, references TSWApi
- TSWApi/ — API library for Train Sim World 6 (Types, Http, ApiClient, TreeNavigation modules)

## Learnings
- AWSSunflower/ApiExplorer.fs — API Explorer component with tree browser, connects to TSW6 API via TSWApi library
- **Async threading rule:** After any `Async.AwaitTask` call (e.g. HttpClient), the continuation may resume on a thread pool thread, NOT the UI thread. Always capture `SynchronizationContext.Current` before the async block and call `do! Async.SwitchToContext uiContext` after each `let!` that wraps a Task, before touching any FuncUI state (`IWritable.Set`). This matches the pattern already established in SerialPort.fs.
- The TSWApi `sendRequest` function uses `Async.AwaitTask` on `HttpClient.SendAsync`, which means its async continuations can land on any thread.
- FuncUI `IWritable.Set` called from a non-UI thread silently fails to trigger a re-render — no exception, no crash, just a frozen UI. Very hard to diagnose.
- Three async functions in ApiExplorer.fs need this pattern: `connect()`, `expandNode()`, `getEndpointValue()`.
- **Null-guard API responses:** `GetResponse.Values` (Dictionary<string, obj>) can be null when deserialized — always null-check before iterating with `Seq.map`.
- **ScrollViewer in StackPanel won't scroll:** StackPanel gives children infinite height. Use DockPanel with the fixed-size element docked to Top and ScrollViewer filling remaining space.
- **Unified MVU architecture:** All state (serial tab + API explorer) lives in one `ApiExplorer.Model` with one `Msg` union and one `update` function. Program.fs hosts the single `Component` with `ctx.useState`, dispatch loop, and all effects (port polling, toast dismiss, polling/loco timers). This prevents state loss on tab switch — the top-level Component is never destroyed.
- **Shared serial port:** The `SerialPort` field on Model is shared between serial tab and API explorer polling. API `Disconnect` does NOT disconnect serial. Serial tab uses `ToggleSerialConnection`/`SerialConnectResult` messages; API explorer's internal `ConnectSerial`/`DisconnectSerial` remain for backward compatibility.
- **Serial value mapping:** `PollValueReceived` maps values containing "1" → send "s" (set sunflower), "0" → send "c" (clear sunflower). No longer sends `key=value` format.
- **Immediate bind poll:** `BindEndpoint` issues `pollEndpointsCmd` for the newly bound endpoint immediately, rather than waiting for the next `PollingTick`.
- **Elmish migration:** Replaced hand-rolled `Dispatch<'msg>`, `Sub<'msg>`, `Cmd<'msg>` types and `Cmd` module with `Avalonia.FuncUI.Elmish` (v1.5.2) which brings `Elmish` (v4.3.0). In ApiExplorer.fs, just `open global.Elmish` — types and `Cmd.OfAsync.either` are identical. In Program.fs, the manual dispatch loop is replaced by `ctx.useElmish(writableModel, ApiExplorer.update)` from `Avalonia.FuncUI.Elmish.ElmishHook`.
- **Namespace gotcha:** `open Elmish` inside the `CounterApp` namespace triggers FS0893 because F# partially matches it to `Avalonia.FuncUI.Elmish`. Fix: use `open global.Elmish`.
- **Polling timers:** Endpoint value polling at 200ms, loco detection at 1s. Timers remain as `DispatcherTimer` in `ctx.useEffect` (AfterInit) since `useElmish` doesn't expose Elmish subscriptions directly.
- **Loco change handling:** `LocoDetected` now detects when loco changes (vs same loco repeating), reloads bindings from DB, clears stale `PollingValues`, and auto-starts polling if the new loco has bindings.
- **Tree refresh on loco change:** When loco changes, `LocoDetected` now clears all tree state (`TreeRoot = []`, `SelectedNode = None`, `EndpointValues = Map.empty`) and issues `loadRootNodesCmd config` to reload the tree from the API. This ensures the UI reflects the current loco's API data, not stale data from the previous loco.

### Elmish + SQLite Integration (2026-02-25)
**Date:** 2026-02-25  
**Task:** Migrate MVU from hand-rolled to Elmish, integrate SQLite persistence, fix polling timers and loco detection

**Key Changes:**
- **Elmish adoption:** Removed ~50 lines of hand-rolled dispatch infrastructure, integrated `Avalonia.FuncUI.Elmish.ElmishHook`
- **Polling intervals:** Reduced from 500ms → 200ms (values), 5s → 1s (loco) for better responsiveness
- **Loco change detection:** Now properly distinguishes "loco changed" from "same loco repeated" — clears stale polling values and reloads bindings on change
- **Config reload:** When loco changes, `LocoDetected` calls `BindingPersistence.load()` to get fresh bindings for that loco
- **Auto-start polling:** If new loco has bindings, polling starts immediately; if no bindings, polling stops

**Test Integration:**
- 106 tests passing (103 original + 3 new loco-change tests)
- Edward Thomas writing 12 additional tests for Elmish/SQLite/polling edge cases
- Coordinator fixed test isolation (binding mutations now pure in-memory)

**Status:** ✅ Completed & tested

### NRE & Unbind Bug Fixes (2026-02-25)
**Date:** 2026-02-25  
**Task:** Fix NullReferenceException crash in endpoint viewer + sunflower not clearing on unbind

**Key Changes:**
- **Null-safety in view:** Added `nullSafe` helper and CLR null guard for `Endpoints` list (`Some null` from JSON deserialization). `endpointViewerPanel` now guards `ep.Name`, `node.Path`, `node.Name`, and the endpoints list itself.
- **UnbindEndpoint fix:** Now removes the unbound key from `PollingValues` map AND sends `SendSerialCommand "c"` via `Cmd.ofMsg` to reset sunflower hardware.
- **LocoDetected fix:** On loco change, now batches `loadRootNodesCmd` with `Cmd.ofMsg (SendSerialCommand "c")` to clear stale hardware state.
- **Pattern:** Use `Cmd.ofMsg` to chain serial commands from update handlers, keeping side-effects in the Elmish command pipeline rather than using `Async.Start` directly.

**Test Integration:**
- 127 tests passing (121 baseline + 6 new bug-fix tests)

**Status:** ✅ Completed & tested

### Single-Screen Layout Redesign (2026-02-25)
**Date:** 2026-02-25  
**Task:** Merge two-tab layout into single screen with serial port docked right; remove toast notifications

**Key Changes:**
- **Removed TabControl:** Replaced two-tab layout (Serial Port + API Explorer) with a single unified `mainView` using DockPanel
- **Serial port side panel:** New compact 200px-wide panel docked right with port dropdown, connect button, status indicator (colored dot), and sunflower buttons
- **Removed toast system:** Stripped `Toasts` field from Model, `AddToast`/`DismissToast` messages, toast handlers, toast auto-dismiss timer from Program.fs, and `errorToast`/`mainLayout` from Components.fs (now unused)
- **Removed ActiveTab:** No longer needed without tabs
- **Simplified SendSerialCommand:** Now fires-and-forgets via `Async.Start` instead of routing through toast messages
- **Layout order:** DockPanel children order: serial panel (Right) → status bar (Bottom) → bindings panel (Bottom) → connection panel (Top) → tree browser (Left) → endpoint viewer (fills center)

**Files Modified:**
- `AWSSunflower/ApiExplorer.fs` — New `serialPortPanel`, `mainView`; removed `serialPortTabView`, `apiExplorerTabView`, toast/tab state
- `AWSSunflower/Program.fs` — Removed TabControl + toast effect; calls `ApiExplorer.mainView`

**Test Integration:**
- 127 tests passing (no test changes needed — all removed fields were UI-only)

**Status:** ✅ Completed & tested

### Global Exception Handling (2026-02-25)
**Date:** 2026-02-25  
**Task:** Add debug/release exception handling split to AWSSunflower

**Key Changes:**
- **ErrorHandling module** in Program.fs with `showErrorDialog`, `setupGlobalExceptionHandlers`, `safeDispatch`
- **Debug mode:** `#if DEBUG` — exceptions propagate normally, full stack traces in console
- **Release mode:** Three layers of protection:
  1. `AppDomain.CurrentDomain.UnhandledException` — catches fatal unhandled exceptions, logs to stderr, shows dialog
  2. `TaskScheduler.UnobservedTaskException` — catches async task exceptions, calls `SetObserved()` to prevent crash
  3. `safeDispatch` wrapper — wraps ALL Elmish dispatch calls (timers, port polling, view) in try/catch
- **User-friendly dialog:** Simple Avalonia Window with message + OK button, posted via `Dispatcher.UIThread.Post`
- **safeDispatch is the key layer** — catches NRE-in-update-chain bugs at the dispatch boundary before they become unhandled
**Status:** ✅ Completed & tested

### UI Refinements — Subscription, PortDetection, CommandMapping (2026-02-25)
**Date:** 2026-02-25  
**Task:** Integrate three new library modules into AWSSunflower UI

**Key Changes:**
- **Subscription API (Change Set 1):** Replaced manual 200ms `DispatcherTimer` + `pollEndpointsCmd` with `TSWApi.Subscription.create`. Subscription is stored in a module-level mutable ref (`currentSubscription`). Created via `Cmd.ofEffect` which captures `dispatch` and marshals `OnChange` callbacks to UI thread via `Dispatcher.UIThread.Post`. Removed `IsPolling`, `StartPolling`, `StopPolling`, `PollingTick`, `PollValueReceived`, `PollError`. Added `EndpointChanged of ValueChange`.
- **Port Detection (Change Set 2):** Replaced `SerialPorts: string list` with `DetectedPorts: DetectedPort list`. Port polling now uses `PortDetection.detectPorts()` instead of `SerialPort.getAvailablePorts()`. ComboBox shows `portDisplayName` (e.g., "COM3 — Arduino Uno"). Auto-selects Arduino when `detectArduino()` returns `SingleArduino`.
- **Command Mapping (Change Set 3):** Added `ActiveAddon: AddonCommandSet option` to Model, initialized to `AWSSunflowerCommands.commandSet`. `EndpointChanged` uses `CommandMapping.translate` instead of hardcoded substring matching. `UnbindEndpoint` and `LocoDetected` use `CommandMapping.resetCommand` instead of literal `"c"`.
- **Pattern:** Mutable ref + `Cmd.ofEffect` is the practical pattern for IDisposable resources in Elmish. The subscription is a resource (like HttpClient), not UI state.

**Files Modified:**
- `AWSSunflower/ApiExplorer.fs` — All three change sets
- `AWSSunflower/Program.fs` — Removed 200ms timer, updated port polling to use `PortDetection`
- `TSWApi.Tests/ApiExplorerUpdateTests.fs` — Updated tests for new API

**Test Integration:**
- 190 tests passing (17 AWSSunflower.Tests + 173 TSWApi.Tests)

**Status:** ✅ Completed & tested
