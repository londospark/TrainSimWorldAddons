# Decision: Global Exception Handling Strategy

**Date:** 2026-02-25  
**Author:** Tom Rolt (UI Dev)  
**Status:** Implemented  
**Branch:** feature/global-error-handling  

## Context

The app was crashing on NullReferenceException in the Elmish dispatch chain (ApiExplorer.fs line 815). In release mode, users see an unhandled crash with no useful feedback.

## Decision

Three-layer exception handling with `#if DEBUG` / `#else` compile-time split:

1. **Debug mode:** No wrapping — full stack traces propagate to console for developer diagnosis.
2. **Release mode:**
   - `AppDomain.CurrentDomain.UnhandledException` — last-chance handler, logs + shows dialog
   - `TaskScheduler.UnobservedTaskException` — catches async exceptions, calls `SetObserved()`, logs + shows dialog
   - `safeDispatch` wrapper — wraps all Elmish dispatch calls in try/catch, shows user-friendly dialog, logs full exception to stderr

## Key Design Choices

- **safeDispatch wraps ALL dispatch calls** (timers, port polling, and view), not just timers. This catches any exception originating in `update` regardless of trigger source.
- **showErrorDialog uses `Dispatcher.UIThread.Post`** — safe to call from any thread, defensively wrapped in try/catch in case dispatcher isn't ready.
- **App continues running** after dispatch errors and task exceptions. Only `AppDomain.UnhandledException` is terminal (CLR enforces this).
- **ErrorHandling module** lives in Program.fs — no new files, single point of truth.

## Files Modified

- `AWSSunflower/Program.fs` — Added `ErrorHandling` module, wrapped dispatch chain
