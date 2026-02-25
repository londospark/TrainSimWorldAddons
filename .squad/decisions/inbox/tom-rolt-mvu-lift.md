# Decision: Unified MVU Architecture

**Date:** 2025-07-23
**Author:** Tom Rolt (UI Dev)
**Status:** Implemented
**Branch:** feature/mvu-lift

## Context

The API Explorer tab was losing all state (connection, tree, endpoint values) when switching to the Serial Port tab and back. This happened because `ApiExplorer.view()` created a `Component` with `ctx.useState(init())` — when Avalonia destroys the tab content on switch, the component's state is lost.

Additionally, serial output mapping was sending raw `key=value` strings instead of the required `s`/`c` commands, and binding an endpoint didn't trigger an immediate poll.

## Decision

Lift ALL state into a single unified MVU loop:

1. **One Model** — `ApiExplorer.Model` holds both serial tab state (`SerialPorts`, `SerialConnectionState`, `Toasts`, etc.) and API explorer state. Serial port is shared across tabs.
2. **One Msg union** — Combined messages for both tabs plus new messages: `SetActiveTab`, `PortsUpdated`, `ToggleSerialConnection`, `SerialConnectResult`, `AddToast`, `DismissToast`, `SendSerialCommand`.
3. **One dispatch loop** — `Program.fs` hosts the single top-level `Component` with `ctx.useState`, the `dispatch` function, and all effects (port polling, toast auto-dismiss, polling/loco timers).
4. **Public tab views** — `ApiExplorer.apiExplorerTabView` and `ApiExplorer.serialPortTabView` are pure view functions that take `Model` and `Dispatch<Msg>`.

## Key Behavioral Changes

- API `Disconnect` no longer disconnects the shared serial port.
- `PollValueReceived` maps value containing `"1"` → send `"s"`, `"0"` → send `"c"`.
- `BindEndpoint` immediately polls the bound endpoint (issues `pollEndpointsCmd`).

## Files Modified

- `AWSSunflower/ApiExplorer.fs` — Unified Model/Msg, new handlers, public view functions, removed Component host
- `AWSSunflower/Program.fs` — Single MVU host with dispatch loop and all effects

## Outcome

Build succeeds, all 104 tests pass. No changes to Components.fs, Types.fs, or TSWApi.
