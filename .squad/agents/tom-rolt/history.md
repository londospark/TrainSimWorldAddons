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
