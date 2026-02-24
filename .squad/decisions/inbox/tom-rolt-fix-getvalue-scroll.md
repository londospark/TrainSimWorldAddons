# Decision: Null-guard API responses & DockPanel for scrollable trees

**Date:** 2025-07-23  
**Author:** Tom Rolt (UI Dev)  
**Status:** Implemented  
**Branch:** feature/fix-getvalue-scroll  

## Context

Two runtime bugs in `AWSSunflower/ApiExplorer.fs`:

1. **GetValue crash:** `getValueCmd` iterated `getResp.Values` (a `Dictionary<string, obj>`) without null-checking. The TSW API can return null for this field, causing `ArgumentNullException`.

2. **Tree scroll broken:** `treeBrowserPanel` used a `StackPanel` root containing a `ScrollViewer`. StackPanels give children infinite available height, so the ScrollViewer never constrained its content and scrolling didn't activate.

## Decision

1. Added null/empty guard on `getResp.Values` before `Seq.map`. Returns `"(no values returned)"` when null or empty.
2. Changed `treeBrowserPanel` root from `StackPanel` to `DockPanel`. Search TextBox docked to `Dock.Top`, ScrollViewer fills remaining space.

## Outcome

Build succeeds (0 warnings, 0 errors). All 87 tests pass. Commit on `feature/fix-getvalue-scroll`.
