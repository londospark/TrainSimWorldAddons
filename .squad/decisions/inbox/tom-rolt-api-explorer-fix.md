# Decision: Explicit UI thread dispatch after HTTP calls in FuncUI async blocks

**Date:** 2025-07-18
**Author:** Tom Rolt (🎨 UI Dev)
**Status:** Applied

## Context
The API Explorer `connect()` function hung silently after `listNodes` returned successfully. The `treeRoot.Set rootNodes` call appeared to do nothing — no re-render, no exception, no error.

## Root Cause
TSWApi's `sendRequest` uses `Async.AwaitTask` on `HttpClient.SendAsync`. After the HTTP response arrives, the F# async continuation resumes on a thread pool thread, not the Avalonia UI thread. FuncUI's `IWritable.Set` called from a non-UI thread silently fails to trigger a component re-render.

## Decision
All async functions in ApiExplorer.fs that call TSWApi HTTP methods must:
1. Capture `System.Threading.SynchronizationContext.Current` before the `async {}` block
2. Call `do! Async.SwitchToContext uiContext` after every `let!` that involves an HTTP call, before any state updates
3. Also switch in the `with ex ->` handler since exceptions during HTTP processing may fire on the thread pool

This follows the same pattern already established in SerialPort.fs (`Async.SwitchToContext`).

## Scope
Applied to `connect()`, `expandNode()`, and `getEndpointValue()` in AWSSunflower/ApiExplorer.fs.

## Rule for future code
**Any FuncUI async block that calls TSWApi must switch back to the UI thread before calling `.Set` on any state.**
