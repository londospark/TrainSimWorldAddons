# Sir Haydn — History

## Project Context
- **Project:** TrainSimWorldAddons — F# library wrapping the Train Sim World 6 HTTP API
- **Stack:** F#, .NET 10, HTTP
- **User:** LondoSpark
- **Existing project:** AWSSunflower (Avalonia FuncUI desktop app targeting .NET 10)
- **Goal:** New F# class library implementing TSW API PRD (Phase 1: GET requests)
- **PRD location:** TSW_API_PRD.md

## Learnings
<!-- Append learnings below -->

### API Surface Cleanup — R-S1, R-S2 (refactor/api-surface)
**Date:** 2025-07-23
**Branch:** refactor/api-surface

Applied two refactoring items to reduce nesting and eliminate duplication:

**R-S1: Flatten getUsbPortMappings (PortDetection.fs)**
- Extracted `tryGetInstancePort` — handles a single registry instance key, returns `(portName, UsbDeviceInfo) option`
- Extracted `tryGetPortMappings` — iterates instances for one device class, returns seq of mappings
- `getUsbPortMappings` now simply opens the USB registry key and collects all mappings via `Seq.collect`
- Nesting reduced from 6 levels to 2-3 levels
- Changed from `Array` computation expression + `Map.ofArray` to `Seq.collect` + `Map.ofSeq` for cleaner composition

**R-S2: Extract nameMatches helper (TreeNavigation.fs)**
- Extracted `nameMatches : string -> Node -> bool` private helper
- Replaced duplicated inline lambda in both `[ name ]` and `name :: rest` branches of `getNodeAtPath`
- Pattern: `nodes |> List.tryFind (nameMatches name)`

**Outcome:** 200 tests pass (183 TSWApi + 17 AWSSunflower). Build clean, zero warnings.

### Subscription Module Implementation (feature/subscribe-api)
**Date:** 2025-02-27
**Branch:** feature/subscribe-api (in progress)

Implemented TSWApi Subscription module following Talyllyn's ADR for Phase 2. This module moves polling logic from AWSSunflower into the library layer for reusability.

**Key Architecture:**
- Uses `System.Threading.Timer` (not Avalonia DispatcherTimer) for framework-agnostic polling
- Thread-safe with `lock` on mutable `Dictionary<string, EndpointState>`
- Sequential polling within each tick (TSW6 game is single-threaded)
- Change detection: first poll always fires OnChange (OldValue = None), subsequent polls only fire on delta
- Callbacks fire on timer thread — consumer must marshal to UI thread if needed
- Default 200ms interval matches existing AWSSunflower behavior

**Types:**
- `EndpointAddress` — minimal {NodePath, EndpointName}, library-specific (not reusing UI's BoundEndpoint)
- `ValueChange` — {Address, OldValue: string option, NewValue: string}
- `SubscriptionConfig` — {Interval, OnChange callback, OnError callback}
- `ISubscription` interface — Add, Remove, Endpoints, IsActive, IDisposable

**Implementation Details:**
- `Add`/`Remove` are idempotent
- Poll errors call `OnError`, don't stop subscription (allows retry on next tick)
- `Dispose` stops timer, sets IsActive to false, idempotent
- Internal `EndpointState` tracks LastValue for delta detection
- Uses `ApiClient.getValue` to poll, formats multiple Values as comma-separated string

**Testing Challenges:**
- Encountered file creation issues with CLI tools during TDD red phase
- Test suite drafted but compilation incomplete
- Need to verify: first poll behavior, change detection, idempotency, disposal, error handling, multiple endpoints

**Next Steps:**
- Complete test suite and verify all 10 test scenarios pass
- Run full test suite (existing 127 tests + 10 new subscription tests)
- Update AWSSunflower Program.fs to use Subscription.create instead of DispatcherTimer
- Remove PollingTick message and pollEndpointsCmd from ApiExplorer.fs after migration

### Typestate Pattern Implementation (Issue #23)
**Date:** 2025-01-XX
**Branch:** feature/typestate-refactor

Implemented typestate pattern using single-case DUs with private constructors to make illegal states unrepresentable:

**API Design:**
- Created `BaseUrl` type with private constructor, preventing direct instantiation outside the module
- Created `CommKey` type with private constructor for validated authentication tokens
- Both types expose `[<RequireQualifiedAccess>]` modules with smart constructors returning `Result<T, ApiError>`
- Added `ConfigError` case to `ApiError` discriminated union for configuration validation errors
- Updated `ApiConfig` record to use validated types instead of raw strings

**Key Patterns:**
- Smart constructors validate at the boundary: `BaseUrl.create` checks URL format, `CommKey.create` checks non-empty
- Pre-validated constants skip Result wrapper: `BaseUrl.defaultUrl` is directly constructed since it's known-valid
- Value accessors extract raw strings: `BaseUrl.value` and `CommKey.value` use pattern matching on private constructor
- Privacy model: constructors private to module, accessors public, enforcing validation path

**Testing Strategy:**
- Edward Thomas wrote red-phase TDD tests covering all validation scenarios
- Tests verify rejection of empty strings, invalid protocols, whitespace-only inputs
- Tests verify acceptance of valid inputs and correct normalization (trimming, trailing slash removal)
- All 66 tests pass (53 existing + 13 new typestate validation tests)

**Impact:**
- Breaking change: `ApiConfig` fields changed from `string` to validated types
- Factory functions now return `Result<ApiConfig, ApiError>` instead of bare `ApiConfig`
- Compile-time safety: impossible to construct invalid config that compiles
- Downstream consumers must handle `Result` at config creation boundary
