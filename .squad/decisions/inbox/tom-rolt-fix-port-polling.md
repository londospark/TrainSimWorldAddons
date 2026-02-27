# Fix Port Polling Initialization Bug

**Date:** 2025-05-XX  
**Author:** Tom Rolt (UI Dev)  
**Status:** Implemented

## Problem

After the Elmish migration, COM ports were no longer being detected in the UI. The `SerialPorts` field in the model remained empty `[]` even when ports were available.

## Root Cause

In `AWSSunflower/SerialPort.fs`, the `startPortPolling` function had two issues:

1. **Synchronous dispatch during initialization:** It called `onUpdate lastPorts` immediately during the `useEffect` handler. With Elmish, `onUpdate` became `dispatch (PortsUpdated ports)`, but dispatching during `useEffect` initialization is unreliable — the Elmish loop may not be ready to process the message.

2. **No initial change detection:** `lastPorts` was initialized to `getAvailablePorts()`, so if ports didn't change, the timer callback would never fire (it only calls `onUpdate` when `currentPorts <> lastPorts`).

## Solution

Changed initialization in `startPortPolling`:
- Initialize `lastPorts` to empty list `[]` instead of `getAvailablePorts()`
- Removed the synchronous `onUpdate lastPorts` call

Now the first timer tick (~1 second after initialization) detects the change from `[]` to actual ports and dispatches through the Elmish loop correctly.

## Testing

- ✅ Build succeeded: `dotnet build AWSSunflower/AWSSunflower.fsproj`
- ✅ All 118 tests passed: `dotnet test TSWApi.Tests/TSWApi.Tests.fsproj`

## Impact

Minimal change (2 lines) that restores port detection functionality with the Elmish architecture.
