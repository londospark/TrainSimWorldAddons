# Decision: Migrate to Avalonia.FuncUI.Elmish

**Date:** 2025-07-XX  
**Author:** Tom Rolt (UI Dev)  
**Status:** Implemented  
**Branch:** feature/elmish-sqlite

## Context

The AWSSunflower app had a hand-rolled MVU dispatch loop in Program.fs with manual `Dispatch<'msg>`, `Sub<'msg>`, `Cmd<'msg>` types and a `Cmd` module in ApiExplorer.fs. This duplicated infrastructure that the `Avalonia.FuncUI.Elmish` package already provides.

## Decision

Replace the hand-rolled MVU infrastructure with the standard `Elmish` library (v4.3.0) brought in via `Avalonia.FuncUI.Elmish` (v1.5.2).

### Key Changes

1. **ApiExplorer.fs:** Removed ~20 lines of hand-rolled types/modules. Added `open global.Elmish`. The `update` function signature (`msg -> model -> model * Cmd<msg>`) and all `Cmd.OfAsync.either` calls are unchanged since Elmish uses identical types.

2. **Program.fs:** Replaced manual dispatch loop (`Dispatcher.UIThread.Post` + `model.Set` + cmd execution) with `ctx.useElmish(writableModel, ApiExplorer.update)` from `ElmishHook`. Used the `IWritable<'model>` overload so timer effects can still read `writableModel.Current`.

3. **Timer intervals:** Polling 500ms → 200ms, loco detection 5s → 1s.

4. **Loco change handling:** `LocoDetected` now reloads bindings from persistence when loco changes, clears stale polling values, and auto-starts polling if the new loco has bindings.

### Namespace Note

`open Elmish` inside the `CounterApp` namespace triggers FS0893 (partial path ambiguity with `Avalonia.FuncUI.Elmish`). Must use `open global.Elmish` instead.

## Tests

- 105 passing (103 existing + 2 new loco-change tests)
- 1 pre-existing failure (`UnbindEndpoint removes binding`) from SQLite test isolation — not related to this change
