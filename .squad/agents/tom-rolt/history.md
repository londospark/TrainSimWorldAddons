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

### UI Cleanup Refactoring (2026-02-27)
**Date:** 2026-02-27  
**Task:** Apply R-T1 through R-T9 refactoring items to AWSSunflower

**Key Changes:**
- **R-T9:** Deleted dead `Components.fs` (confirmed zero references via grep) and removed Compile entry from fsproj
- **R-T1:** Removed 3 unused serial message cases (`ConnectSerial`, `SerialConnected`, `SerialError`) and their handlers plus `connectSerialCmd`. Kept `DisconnectSerial` because TSWApi.Tests has a test for it
- **R-T2:** Extracted `resetSerialCmd` helper — eliminates duplicated 4-line pipeline in `UnbindEndpoint` and `LocoDetected`
- **R-T3:** Extracted `getLocoBindings` helper — eliminates triplicated 3-line pipeline in `BindEndpoint`, `LocoDetected`, `bindingsPanel`
- **R-T4:** Extracted `endpointKey` helper — replaces 5 inline `sprintf "%s.%s"` calls
- **R-T5:** Extracted `timedApiCall` helper — simplifies `connectCmd`, `loadRootNodesCmd`, `expandNodeCmd`, `getValueCmd` by removing duplicated start/elapsed/match boilerplate
- **R-T6:** Added `AppColors` module with 6 named constants (`connected`, `error`, `warning`, `panelBg`, `border`, `info`) — replaces 16 inline hex strings
- **R-T7:** Flattened `BindEndpoint` handler from 4 nesting levels to 2 using combined match on `model.CurrentLoco, model.ApiConfig`
- **R-T8:** Extracted `isSerialConnected` helper — replaces 3 inline pattern matches in view code

**Learnings:**
- `ApiResult<'T>` is a type alias for `Result<'T, ApiError>` in TSWApi.Types — useful for generic API call helpers
- When flattening nested matches, verify that early-exit cases preserve original behavior (e.g., BindEndpoint's save-even-when-API-disconnected was safely removable since the UI can't trigger it in that state)
- `DisconnectSerial` can't be removed without updating TSWApi.Tests — note for future dead-code passes

**Status:** ✅ Completed & tested — 200 tests pass (17 + 183)

### ApiExplorer.fs Decomposition (2026-02-28)
**Date:** 2026-02-28  
**Task:** Split 1046-line ApiExplorer.fs into 5 focused modules per Talyllyn's ADR

**Key Changes:**
- **ApiExplorer.fs** (~97 lines): Model type, Msg union, init function — the MVU contract
- **ApiExplorerHelpers.fs** (~80 lines): Pure functions — `stripRootPrefix`, `nullSafe`, `effectiveName`, `mapNodeToTreeState`, `endpointKey`, `getLocoBindings`, `isSerialConnected`, `updateTreeNode`, `findNode`, `filterTree`
- **ApiExplorerCommands.fs** (~140 lines): Shared mutable state (`httpClient`, `currentSubscription`) + all async Elmish commands
- **ApiExplorerUpdate.fs** (~210 lines): The `update` function with all Msg pattern matches
- **ApiExplorerViews.fs** (~530 lines): `AppColors` module + all 7 view functions + `mainView` entry point
- **Program.fs:** Updated refs to `ApiExplorerUpdate.update` and `ApiExplorerViews.mainView`
- **Tests:** Added `open CounterApp.ApiExplorerUpdate` to test file
- **Visibility:** Removed `private` from all cross-module functions (now assembly-internal via default F# access)

**Learnings:**
- F# compile order enforces dependency direction — ApiExplorer → Helpers → Commands → Update → Views → Program
- `open TSWApi.Subscription` needed in both ApiExplorer.fs (for `ValueChange` type in Msg) and ApiExplorerUpdate.fs (for `Subscription.endpointPath`)
- `bindingsPanel` reads `currentSubscription.Value` directly from Commands module — noted as MVU anti-pattern for future cleanup
- `InternalsVisibleTo` attribute already covers test assembly access to internal functions

**Status:** ✅ Completed & tested — 200 tests pass (17 + 183)

### ApplicationScreen Refactor (2026-02-28)
**Date:** 2026-02-28  
**Task:** Rename ApiExplorer → ApplicationScreen, move files into ApplicationScreen/ folder, split views into component files

**Key Changes:**
- **Renamed modules:** ApiExplorer → ApplicationScreen, ApiExplorerHelpers → ApplicationScreenHelpers, ApiExplorerCommands → ApplicationScreenCommands, ApiExplorerUpdate → ApplicationScreenUpdate
- **New folder:** All ApplicationScreen files now in `AWSSunflower/ApplicationScreen/`
- **View decomposition:** Split ApiExplorerViews.fs (530 lines) into 7 component files: ConnectionPanel, StatusBar, TreeBrowser, EndpointViewer, BindingsPanel, SerialPortPanel, MainView
- **Flattened nesting:** Extracted `renderEndpoint`, `renderBinding`, `serialStatus` helper functions from deeply nested view code
- **AppColors:** Moved from `module private AppColors` in views to non-private `module AppColors` in Helpers.fs
- **Test file:** Renamed ApiExplorerUpdateTests.fs → ApplicationScreenUpdateTests.fs with updated module/opens

**Learnings:**
- When splitting view files into components, each component needs its own set of Avalonia opens — don't forget `Avalonia.FuncUI.Types` for files using `:> IView` casts
- `open TSWApi` is needed in component files that directly reference `Endpoint`, `TreeNodeState`, or `ApiConnectionState` types
- MainView composition file is very small (~25 lines) — just opens all component modules and calls their functions in DockPanel order
- BindingsPanel is the only component that needs `open CounterApp.ApplicationScreenCommands` (for `currentSubscription` read)

**Status:** ✅ Completed & tested — 200 tests pass (17 + 183)

### F# Idiomaticity Audit (2026-02-28)
**Date:** 2026-02-28  
**Task:** Deep idiomaticity audit of all AWSSunflower source files — Elmish patterns, FuncUI DSL, F# style

**Key Findings (15 total, report in `.squad/decisions/inbox/tom-rolt-app-audit.md`):**

1. **Side effects in update (CRITICAL):** Six update handlers perform I/O directly (disposeSubscription, BindingPersistence.save, SerialPortModule.disconnect, Async.Start). All should be wrapped in `Cmd.ofEffect`. This is the #1 Elmish anti-pattern in the codebase.
2. **BindingsPanel reads mutable ref in view:** `currentSubscription.Value` bypasses MVU — add `IsSubscriptionActive: bool` to Model.
3. **AppColors returns strings, not brushes:** 16+ callsites wrap in `SolidColorBrush(Color.Parse(...))`. Change AppColors to return `IBrush` directly.
4. **15+ sprintf calls should use `$"..."` interpolation:** Simple string formatting throughout views and helpers.
5. **Dead `Toast` type in Types.fs:** Unused since single-screen layout redesign.
6. **`ensureInitialized` in BindingPersistence.fs:** Double-checked locking — should use `Lazy<unit>`.
7. **`readAllFromDb` uses Dictionary/ResizeArray:** Should use `List.groupBy` + F# computation expression reader loop.
8. **CounterApp namespace:** Legacy name, should be AWSSunflower (separate PR).
9. **`sendAsync` captures SynchronizationContext unnecessarily:** Returns Result, no UI mutation — remove context switch.
10. **ComboBox.onSelectedItemChanged fragile obj cast:** Should pattern match on obj type.

**Learnings:**
- `Cmd.ofEffect` is the correct way to express synchronous side effects in Elmish — not inline calls in the update function
- F# `Lazy<unit>` replaces double-checked locking for one-time initialization
- `$"...%A{value}"` works in F# for structured formatting in interpolated strings (F# 6+)
- AppColors as `IBrush` values eliminates repetitive `SolidColorBrush(Color.Parse(...))` wrapping in views
- `when` guards on match cases can eliminate one level of `if/else` nesting in update handlers

**Status:** ✅ Audit complete — report written to decisions inbox
