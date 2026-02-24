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
