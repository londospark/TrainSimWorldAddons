# Decision: Single-Screen Layout Redesign

**Date:** 2026-02-25  
**Author:** Tom Rolt (UI Dev)  
**Status:** Implemented  
**Branch:** develop

## Decision

Merged the two-tab layout (Serial Port + API Explorer) into a single unified screen. The serial port controls are now a compact 200px panel docked on the right side of the window. The toast notification system has been removed entirely.

## Rationale

- Two tabs forced users to switch back and forth — serial port and API explorer are used together during operation
- Single-screen gives immediate visibility of serial connection status while browsing the API tree
- Toast notifications added visual noise without clear value — status is now shown inline via a colored dot indicator

## Key Design Choices

- **DockPanel layout order:** Right (serial) → Bottom (status) → Bottom (bindings) → Top (connection) → Left (tree) → Center (endpoint viewer)
- **Serial panel width:** 200px fixed — enough for controls without wasting space
- **Status indicator:** Colored dot (●) with short text instead of 36px bold display
- **SendSerialCommand:** Simplified to fire-and-forget (`Async.Start`) since toast feedback was removed
- **Components.fs:** `errorToast` and `mainLayout` functions are now unused but left in place (no breaking change)

## Impact

- No API/update logic changes — purely view reorganization
- All 127 tests pass unchanged
- `Components.fs` individual functions (`portSelector`, `connectionButton`, `actionButtons`, `portDisplay`) still available for reuse if needed
