# Talyllyn — History

## Project Context
- **Project:** TrainSimWorldAddons — F# library wrapping the Train Sim World 6 HTTP API
- **Stack:** F#, .NET 10, HTTP
- **User:** LondoSpark
- **Existing project:** AWSSunflower (Avalonia FuncUI desktop app targeting .NET 10)
- **Goal:** New F# class library implementing TSW API PRD (Phase 1: GET requests — /info, /list, /get)
- **PRD location:** TSW_API_PRD.md

## Learnings
<!-- Append learnings below -->

### Lead Review — Issue #13 (Phase 1 Complete)

**Review Date:** 2025-07-22

**Architecture Observations:**
- Module organization (Types → Http → ApiClient → TreeNavigation) follows a clean dependency chain with no cycles. Compile order in .fsproj matches logical layering.
- `[<AutoOpen>]` on `Types` module is a deliberate choice — makes DU cases like `NetworkError`, `AuthError` available without qualification. Good ergonomics for a small, focused library.
- `sendRequest<'T>` is generic over response type and curried — composable and testable. However, it hardcodes `HttpMethod.Get`. Phase 2 (POST/PATCH/DELETE for subscriptions and /set) will require either a new function or a method parameter.
- `HttpClient` is passed explicitly (not owned), which is correct — avoids socket exhaustion and lets consumers manage lifetime via `IHttpClientFactory` or similar.
- `GetResponse.Values` uses `Dictionary<string, obj>` — the `obj` type means consumers must cast/unbox values. This is pragmatic since the TSW API returns heterogeneous value types, but `JsonElement` would preserve more type information without forcing a specific CLR type.

**Type Fidelity Gap:**
- The PRD JSON shows a `CollapsedChildren: int` field on some `/list` nodes (e.g., `"CollapsedChildren": 188`). This field is not modeled in the `Node` type. System.Text.Json will silently discard it during deserialization. Not blocking for Phase 1 (it's metadata about collapsed subtrees) but should be added before Phase 2 tree expansion features.

**Test Quality:**
- 53 tests, all passing. Good edge case coverage for CommKey discovery (whitespace trimming, missing files, multiple directories). Mock HTTP handler pattern is clean and reusable.
- No NetworkError test (would require a throwing HttpMessageHandler mock) — acceptable gap for Phase 1.
- No test for URL-encoded node names (e.g., `Electric%28PushButton%29`) — the PRD shows these exist in the wild.

**Documentation:**
- XML doc comments on all public functions with param/returns tags. docs/index.md and quickstart.md cover all modules with working code examples. README.md includes quick start and build commands.

**Decision:** APPROVED for Phase 1 release.
### Typestate / DDD Design for ApiConfig (Issue #21)
- **Pattern chosen:** Single-case DUs with private constructors (`type BaseUrl = private BaseUrl of string`) + smart constructors returning `Result<T, ApiError>`. This is the idiomatic F# DDD approach.
- **Key insight:** Compile-time safety comes from opaque value types (`BaseUrl`, `CommKey`), not from making `ApiConfig` itself private. If both fields are validated types, the record is valid by construction.
- **HttpClient stays separate** from config — it's an infrastructure concern with different lifetime semantics (long-lived, reused). Bundling it would violate SRP.
- **`[<RequireQualifiedAccess>]`** on companion modules prevents name collisions (`BaseUrl.create` vs `CommKey.create`).
- **Privacy model:** `private` on DU cases in F# is scoped to the enclosing module. Sub-modules (companion modules) within the same parent can access private constructors. Code in other files (Http.fs, ApiClient.fs) cannot.
- **New `ConfigError` case** added to `ApiError` for URL validation errors. `AuthError` kept for CommKey validation (semantically correct).
- **Test impact:** 14 of 53 tests need updating. TreeNavigation (16 tests) and most TypesTests are unaffected. Main changes are mechanical: replace record literals with smart constructor calls.
- **`discoverCommKey`** return type changes from `Result<string, ApiError>` to `Result<CommKey, ApiError>`, making it compose directly into config creation.
- **Design decision written to:** `.squad/decisions/inbox/talyllyn-typestate-design.md`

### Endpoint Binding, Polling, and Serial Output Architecture

**Review Date:** 2025-02-24

**Feature Request:** Dynamic endpoint monitoring system — bind endpoints to locomotives, poll at 500ms intervals, send value changes over serial port, persist bindings across app restarts.

**Architecture Decisions:**
- **Data Model:** `LocoId` is composite key (Provider + Product + BlueprintId) for uniqueness across DLCs. `BoundEndpoint` stores persistent binding, `PollingState` tracks transient runtime state (last value, error count).
- **Config Format:** JSON at `%APPDATA%\LondoSpark\AWSSunflower\bindings.json` using System.Text.Json. Versioned schema for future migration.
- **Polling Strategy:** Timer-based (DispatcherTimer at 500ms) matches existing SerialPort.startPortPolling pattern. Simpler than recursive Cmd loops, easier to test and manage lifecycle (IDisposable).
- **Loco Detection:** Query Player tree every 3 seconds to extract LocoId. Location TBD (requires manual exploration with API Explorer). Returns `LocoId option` to handle "not driving" state.
- **Serial Integration:** Reuse existing `SerialPort.sendAsync`, no changes needed. Data format: `"nodePath/endpointName=value\n"` (newline-delimited, parseable).
- **MVU Changes:** New messages (`BindEndpoint`, `PollingTick`, `PollingValueReceived`, `LocoDetected`), extended Model (CurrentLoco, BoundEndpoints, PollingStates, timers), new "Bind" button in endpoint UI.
- **TSWApi Impact:** NONE. Existing `getValue` sufficient, polling logic belongs in app layer.

**Key Files:**
- New: `AWSSunflower/BindingPersistence.fs` (JSON load/save), `AWSSunflower/LocoDetection.fs` (loco ID extraction)
- Modified: `AWSSunflower/Types.fs` (add types), `AWSSunflower/ApiExplorer.fs` (extend MVU)

**Work Decomposition:** 6 phases, 14-20 hours total (data model → loco detection → binding UI → polling engine → serial integration → integration testing). Critical path, no parallelization.

**Open Questions:** Loco metadata path in Player tree (action: manual exploration), serial protocol format (action: user confirmation), polling interval configurable vs hardcoded.

**Decision written to:** `.squad/decisions/inbox/talyllyn-binding-polling-serial-architecture.md`

### ApiExplorer.fs Decomposition Analysis

**Review Date:** 2026-02-28

**File Analysis:** `AWSSunflower/ApiExplorer.fs` (1046 lines) contains the entire MVU stack — Model, Msg, init, pure helpers, async commands, update function, and 7 view functions. Analyzed for decomposition into focused modules.

**Proposed Split:** 5 files replacing the monolith:
1. `ApiExplorer.fs` (~90 lines) — Model, Msg, init (keeps module name for minimal call-site changes)
2. `ApiExplorerHelpers.fs` (~100 lines) — pure functions (tree ops, string helpers, filterTree)
3. `ApiExplorerCommands.fs` (~150 lines) — shared HttpClient, subscription state, all async Cmd functions
4. `ApiExplorerUpdate.fs` (~215 lines) — update function only
5. `ApiExplorerViews.fs` (~530 lines) — AppColors + all 7 view functions + mainView

**Key Findings:**
- `endpointKey` and `getLocoBindings` are shared between update and views — must go in Helpers.
- `bindingsPanel` reads `currentSubscription.Value` directly (MVU violation) — creates view→commands dependency. Recommended fix: add `IsSubscriptionActive: bool` to Model (separate PR).
- All `private` helpers become `internal` (assembly-scoped) when split across files. `InternalsVisibleTo` already set for tests.
- Only 2 call-site changes in Program.fs: `ApiExplorerUpdate.update` and `ApiExplorerViews.mainView`.
- Test file needs additional `open` statements but no logic changes.
- Dependency chain is linear with no cycles.

**Decision written to:** `.squad/decisions/inbox/talyllyn-apiexplorer-decomposition.md`

### Idiomaticity Audit — Full Codebase

**Audit Date:** 2025-03-01

**Scope:** 34 .fs files across TSWApi, AWSSunflower, and both test projects.

**Key Findings:**
- `CounterApp` namespace is a leftover template name across all AWSSunflower files — high-impact naming issue.
- ~25 `sprintf` calls should be `$"..."` interpolation for consistency — some functions already use interpolation (Http.fs), creating mixed style.
- `HttpMethod("PATCH")` should be `HttpMethod.Patch` (available since .NET 5, project targets .NET 10).
- `SerialError` DU missing `[<RequireQualifiedAccess>]` — `Disconnected` case collides with `ConnectionState.Disconnected`.
- `.Length > 0` on F# lists is O(n); `.IsEmpty` is O(1) — found in 4 places.
- `httpClient` and `currentSubscription` in Commands.fs are public mutable module state that should be `internal`.
- `BindingsPanel` directly reads `currentSubscription` ref cell from view — confirmed MVU anti-pattern (already documented in decomposition analysis).
- `SendSerialCommand` handler silently discards `Result<unit, SerialError>` — errors swallowed.
- `okOrFail` pattern converts `Result.Error` to `failwith` for Elmish `Cmd.OfAsync.either` — a custom `Cmd.ofAsyncResult` helper would be cleaner.

**What's Already Idiomatic:**
- TSWApi types (BaseUrl, CommKey) use single-case DUs with private constructors — excellent DDD pattern.
- `result {}` CE from FsToolkit.ErrorHandling used in `createConfigWithUrl`.
- Pipeline style is consistent throughout both projects.
- Module dependency chains are clean with no cycles.
- `async {}` is correct for Elmish/FuncUI; `task {}` used correctly in test mocks.
- Test naming uses double-backtick convention consistently.
- `[<RequireQualifiedAccess>]` applied to 5 of 7 DUs that need it.

**Decision written to:** `.squad/decisions/inbox/talyllyn-idiomaticity-audit.md`
