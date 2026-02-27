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
