# Decision: Null-Safety & Unbind Cleanup in ApiExplorer

**Date:** 2026-02-25  
**Author:** Tom Rolt (UI Dev)  
**Status:** Applied  
**Branch:** develop  

## Context

Two bugs were reported:
1. NullReferenceException crash at line 815 when clicking "Get Value" — caused by CLR null strings from JSON deserialization propagating through the Elmish view re-render cycle.
2. Sunflower LED not clearing on unbind — `UnbindEndpoint` handler didn't remove stale `PollingValues` entries or send serial "c" command.

## Decisions

### 1. Null-guard all JSON-deserialized fields in view functions

TSWApi types (`Endpoint.Name`, `Node.NodePath`, etc.) can carry CLR null from System.Text.Json deserialization even though they're F# `string` type. Added `nullSafe` helper and `Option.bind` guard for `Endpoints: Endpoint list option` which can be `Some null`.

**Rule:** Any view function rendering TSWApi-sourced data must null-guard string fields and list fields before use.

### 2. Use `Cmd.ofMsg` for chaining serial commands from update handlers

`UnbindEndpoint` and `LocoDetected` (loco change) now issue `Cmd.ofMsg (SendSerialCommand "c")` to clear hardware state. This keeps all side-effects in the Elmish command pipeline, unlike the direct `Async.Start` pattern in `PollValueReceived`.

**Rule:** Prefer `Cmd.ofMsg` for message chaining in update handlers over direct `Async.Start`.

### 3. UnbindEndpoint must clean up PollingValues

When unbinding an endpoint, the key (`nodePath.endpointName`) must be removed from `PollingValues` to prevent stale state from affecting future polling or serial commands.

## Files Modified

- `AWSSunflower/ApiExplorer.fs` — Update handlers + view null-safety
- `TSWApi.Tests/ApiExplorerUpdateTests.fs` — 6 new tests
