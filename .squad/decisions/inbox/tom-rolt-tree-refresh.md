# Decision: Tree Refresh on Loco Change

**Date:** 2026-02-25  
**Author:** Tom Rolt (UI Dev)  
**Status:** Implemented  

## Context

The `LocoDetected` message handler in ApiExplorer.fs was not refreshing the tree view when the player switched locomotives. The handler correctly detected loco changes, reloaded bindings from the database, and cleared polling values, but left the tree displaying stale data from the previous loco.

## Decision

Modified `LocoDetected` handler (lines 406-420) to match the pattern used by `ConnectSuccess` and `Disconnect`:

1. Clear tree state fields: `TreeRoot = []`, `SelectedNode = None`, `EndpointValues = Map.empty`
2. Issue `loadRootNodesCmd config` instead of `Cmd.none` (only if `model.ApiConfig` is `Some config`)

This ensures the tree view always reflects the current loco's API data.

## Outcome

- Build succeeds
- All 118 tests pass
- Tree now refreshes correctly when loco changes, preventing UI confusion
