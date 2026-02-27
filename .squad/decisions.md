# Decisions

> Canonical decision ledger. Append-only. Scribe merges from `.squad/decisions/inbox/`.

## 2026-02-24: Recursive Search Implementation

**Date:** 2025-01-XX  
**Author:** Tom Rolt (UI Dev)  
**Status:** Implemented  
**Branch:** feature/recursive-search  

### Decision: API Explorer Recursive Expansion & Search Filter

The API Explorer tree browser was not capturing parent node endpoints during recursive expansion. When expanding "Player", the API returned 108 endpoints for Player plus 5 child nodes, but only child nodes were stored.

**Solution:**
1. Modified `expandNodeCmd` to capture both child nodes AND parent endpoints
2. Updated `NodeExpanded` message to include optional `endpoints: Endpoint list option`
3. Added `SearchQuery: string` to Model with `SetSearchQuery` handler
4. Implemented recursive `filterTree` function for case-insensitive client-side filtering

**Files Modified:**
- `AWSSunflower/ApiExplorer.fs` — All MVU changes
- `TSWApi.Tests/ApiExplorerUpdateTests.fs` — Updated test fixture

**Outcome:** 87 tests passing (80 baseline + 7 new). Build succeeds. Feature branches merged to develop and main.

---

## 2025-07-22: ADR - Typestate / DDD Pattern for ApiConfig

**Status:** Proposed  
**Author:** Talyllyn (Lead)  
**Issue:** #21  

Adopt single-case DU with private constructors pattern for `BaseUrl` and `CommKey` value types. Keep `ApiConfig` as public record with validated fields. This makes illegal states unrepresentable at compile time (e.g., empty URLs, malformed keys).

**Key Points:**
- `BaseUrl` and `CommKey` have smart constructors returning `Result<T, ApiError>`
- `BaseUrl` validates non-empty, http/https protocol, trims trailing slashes
- `CommKey` validates non-empty after trimming whitespace
- New `ConfigError` case on `ApiError` enum
- Breaking changes to `createConfig`, `createConfigWithUrl`, `discoverCommKey` — now return `Result`
- HttpClient stays separate from config (lifecycle/SRP concerns)
- 53 total tests; 14 need updating; 39 unaffected

**Migration Path:** TDD approach — write validation tests first (red), implement types (green), update existing tests.

---

## 2025-07-18: UI Thread Dispatch in API Explorer Async Blocks

**Date:** 2025-07-18  
**Author:** Tom Rolt (UI Dev)  
**Status:** Applied  

**Decision:** All async functions in ApiExplorer.fs that call TSWApi HTTP methods must:
1. Capture `System.Threading.SynchronizationContext.Current` before async block
2. Call `Async.SwitchToContext uiContext` after each HTTP `let!` before state updates
3. Also switch in exception handlers

**Root Cause:** After `Async.AwaitTask` on HttpClient, continuation resumes on thread pool thread. FuncUI's `IWritable.Set` called from non-UI thread silently fails to re-render.

**Rule:** Any FuncUI async block calling TSWApi must switch back to UI thread before calling `.Set` on state.

---

## 2025-07-22: TSWApi Phase 1 — Lead Review APPROVED

**Author:** Talyllyn (Lead)  
**Issue:** #13  

Phase 1 of TSWApi library **APPROVED** for release. Correctly implements all three GET endpoints (/info, /list, /get) with authentication, type-safe deserialization, tree navigation, and error handling.

**Observations for Phase 2:**
1. `CollapsedChildren` field missing from `Node` type — add `CollapsedChildren: int option`
2. `sendRequest` hardcodes GET — add method parameter for POST/PATCH/DELETE
3. `GetResponse.Values` uses `Dictionary<string, obj>` — consider `Dictionary<string, JsonElement>` to preserve type info
4. URL-encoded node names (e.g., `Electric%28PushButton%29`) — add test coverage

---

## User Directive: Test-Driven Development (TDD)

**Date:** 2026-02-23T22:10Z  
**By:** LondoSpark (via Copilot)  

All work must be done in test-driven manner. Tests first, then implementation.

---

## User Directive: Git Flow

**Date:** 2026-02-23T22:10:01Z  
**By:** LondoSpark (via Copilot)  

All team members must follow git flow: feature branches → develop branch → main branch.

---

## User Directive: GitHub Issues, Actions, and Project Board

**Date:** 2026-02-23T22:12Z  
**By:** LondoSpark (via Copilot)  

Use GitHub Issues, Actions, and Project Board to track and build the project. Run all builds and tests locally before committing — CI should never go red.

---

## User Directive: Documentation Pipeline

**Date:** 2026-02-23T22:14Z  
**By:** LondoSpark (via Copilot)  

Library must have documentation generated as part of the CI pipeline. Publish docs as zip artifact for now. Hosting comes later.

---

## 2026-02-27: Global Exception Handling Strategy

**Date:** 2026-02-27  
**Author:** Tom Rolt (UI Dev)  
**Status:** Implemented  
**Branch:** feature/global-error-handling  

### Context

The app was crashing on NullReferenceException in the Elmish dispatch chain (ApiExplorer.fs line 815). In release mode, users see an unhandled crash with no useful feedback.

### Decision

Three-layer exception handling with `#if DEBUG` / `#else` compile-time split:

1. **Debug mode:** No wrapping — full stack traces propagate to console for developer diagnosis.
2. **Release mode:**
   - `AppDomain.CurrentDomain.UnhandledException` — last-chance handler, logs + shows dialog
   - `TaskScheduler.UnobservedTaskException` — catches async exceptions, calls `SetObserved()`, logs + shows dialog
   - `safeDispatch` wrapper — wraps all Elmish dispatch calls in try/catch, shows user-friendly dialog, logs full exception to stderr

### Key Design Choices

- **safeDispatch wraps ALL dispatch calls** (timers, port polling, and view), not just timers. This catches any exception originating in `update` regardless of trigger source.
- **showErrorDialog uses `Dispatcher.UIThread.Post`** — safe to call from any thread, defensively wrapped in try/catch in case dispatcher isn't ready.
- **App continues running** after dispatch errors and task exceptions. Only `AppDomain.UnhandledException` is terminal (CLR enforces this).
- **ErrorHandling module** lives in Program.fs — no new files, single point of truth.

### Files Modified

- `AWSSunflower/Program.fs` — Added `ErrorHandling` module, wrapped dispatch chain

### Outcome

127 tests pass. Build succeeds. Feature branches merged to develop and main. v1.0.0 released.

---

## 2026-02-28: ApiExplorer.fs Decomposition

**Date:** 2026-02-28  
**Author:** Talyllyn (Lead) → Tom Rolt (UI Dev) [Execution]  
**Status:** Implemented  
**Branch:** feature/apiexplorer-decomposition  
**PR:** #28 (merged to main)

### Problem

`AWSSunflower/ApiExplorer.fs` is 1046 lines containing the entire MVU stack: Model, Msg, init, helpers, async commands, update, and 7 view functions. Hard to navigate, reason about, and maintain.

### Decision

Split into 5 focused modules:
- **ApiExplorer.fs** — Model, Msg, init (~537 lines after trim)
- **ApiExplorerHelpers.fs** — Pure functions (stripRootPrefix, nullSafe, effectiveName, mapNodeToTreeState, endpointKey, getLocoBindings, isSerialConnected, updateTreeNode, findNode, filterTree) (~97 lines)
- **ApiExplorerCommands.fs** — Shared state (httpClient, currentSubscription) + async commands (timedApiCall, connectCmd, loadRootNodesCmd, expandNodeCmd, getValueCmd, detectLocoCmd, createSubscriptionCmd, disposeSubscription, resetSerialCmd) (~77 lines)
- **ApiExplorerUpdate.fs** — update function (~145 lines)
- **ApiExplorerViews.fs** — All UI panels + mainView (~222 lines)

### Key Design Choices

- **Preserve module name** `ApiExplorer` for backward compatibility (Program.fs, test call sites)
- **Visibility:** private → internal (assembly-scoped) via existing `InternalsVisibleTo`
- **Pragmatic:** Kept currentSubscription mutable read from views (MVU anti-pattern; separate PR to add IsSubscriptionActive to Model)
- **Dependencies:** No cycles; clean compile order

### External Call Sites

- `Program.fs`: `ApiExplorer.update` → `ApiExplorerUpdate.update`, `ApiExplorer.mainView` → `ApiExplorerViews.mainView`
- `ApiExplorerUpdateTests.fs`: Added opens for ApiExplorerUpdate, ApiExplorerHelpers

### Files Modified

- AWSSunflower/ApiExplorer.fs (refactored)
- AWSSunflower/ApiExplorerHelpers.fs (new)
- AWSSunflower/ApiExplorerCommands.fs (new)
- AWSSunflower/ApiExplorerUpdate.fs (new)
- AWSSunflower/ApiExplorerViews.fs (new)
- AWSSunflower.fsproj (compile order)
- AWSSunflower/Program.fs (2 line changes)
- TSWApi.Tests/ApiExplorerUpdateTests.fs (opens)

### Outcome

✅ All 200 tests passing. Build succeeds. PR #28 merged to main. 1046 lines split into 5 files (97+77+145+222+537 net = 1078 lines with formatting).

---

## 2026-02-27: DisconnectSerial Message Kept (Pragmatic Test Compatibility)

**Date:** 2026-02-27  
**Author:** Tom Rolt (UI Dev)  
**Status:** Applied  
**Branch:** refactor/ui-cleanup

**Decision:** Removed dead messages `ConnectSerial`, `SerialConnected`, `SerialError` (never dispatched from UI/commands). Kept `DisconnectSerial` because test `TSWApi.Tests/ApiExplorerUpdateTests.fs:366` directly dispatches it. Rather than rewrite the test now, pragmatically retained the message.

**Action Needed:** Edward Thomas should update that test to use `ToggleSerialConnection` (live code path), then `DisconnectSerial` can be removed in a follow-up.

---

## 2026-02-27: CapturingMockHandler Kept Local to HttpTests.fs

**Date:** 2026-02-27  
**Author:** Edward Thomas (Tester)  
**Status:** Applied  
**Branch:** refactor/test-helpers

**Decision:** During test infrastructure consolidation, two different mock handler types existed. Kept the rich `CapturingMockHandler` (captures body, method, content-type) local to `HttpTests.fs` rather than promoting to `TestHelpers.fs`.

**Rationale:** Only HTTP verb tests need body/method/content-type capture. Adding it to TestHelpers would add unused complexity for other test files. If future tests need request capture, it can be imported from HttpTests or promoted then.

---

## 2026-02-27: Remove `getAvailablePorts` and `startPortPolling` from SerialPortModule

**Date:** 2026-02-27  
**Author:** Dolgoch (Core Dev)  
**Status:** Applied  
**Branch:** refactor/backend-cleanup

**Decision:** Both `SerialPortModule.startPortPolling` and `SerialPortModule.getAvailablePorts` are dead code. Port polling was replaced by `PortDetection.detectPorts()` (returns richer `DetectedPort` records). Grep confirmed zero external callers.

**Action Taken:** Removed both functions. `SerialPortModule` now exposes only: `connectAsync`, `sendAsync`, `disconnect`.

**Note:** Documentation files (ARCHITECTURE.md, QUICKSTART.md, IMPLEMENTATION_SUMMARY.md) still reference the removed functions — may need updating.
