# Edward Thomas — History

## Project Context
- **Project:** TrainSimWorldAddons — F# library wrapping the Train Sim World 6 API
- **Stack:** F#, .NET 10, HTTP
- **User:** LondoSpark
- **Existing project:** AWSSunflower (Avalonia FuncUI desktop app targeting .NET 10)
- **Goal:** New F# class library implementing TSW API PRD (Phase 1: GET requests)
- **PRD location:** TSW_API_PRD.md

## Learnings
<!-- Append learnings below -->

### 2025-01-15: TDD Red Phase for Typestate Pattern (Issue #22)

Wrote comprehensive test suite for the typestate refactor on branch `feature/typestate-refactor`. The tests are **intentionally non-compiling** (red phase) as they reference types that don't exist yet: `BaseUrl`, `CommKey`, and `ConfigError`.

**Tests added to TypesTests.fs (10 new tests):**
- `BaseUrl.create` validation: empty string, non-http URLs, trailing slash normalization, valid http/https URLs
- `BaseUrl.defaultUrl` constant verification
- `CommKey.create` validation: empty string, whitespace-only, trimming, valid keys
- Updated existing `ApiConfig defaults to localhost` test to use smart constructors

**Tests updated in HttpTests.fs (12 tests modified + 3 new):**
- All 5 `discoverCommKey` tests now assert `CommKey` values via `.value` accessor
- Updated `createConfig` and `createConfigWithUrl` tests to unwrap `Result` and use `.value` accessors
- All 5 `sendRequest` tests now construct config using smart constructors
- **New tests:** `createConfig rejects empty commKey`, `createConfigWithUrl rejects invalid URL`, `createConfigWithUrl rejects empty commKey`

**Tests updated in ApiClientTests.fs:**
- Changed `testConfig` helper from record literal to smart constructor pattern — single change fixes all 6 tests

**Typestate pattern being tested:**
- **Single-case DUs with private constructors** for `BaseUrl` and `CommKey`
- **Smart constructors** returning `Result<T, ApiError>` enforce validation at construction time
- **Public `.value` accessors** allow safe observation without breaking encapsulation
- **New `ConfigError` case** on `ApiError` for configuration validation failures
- **ApiConfig remains public record** with validated types as fields — valid by construction

This follows standard F# DDD practice: "make illegal states unrepresentable." Once a consumer has a valid `ApiConfig`, all subsequent operations are type-safe with no runtime validation needed.

**Commit:** d0e2ea9 on `feature/typestate-refactor`
**Next:** Talyllyn will implement the types (green phase), then I'll verify all tests pass.

### 2025-01-18: MVU Update Function Tests for Recursive Expansion and Search Filter

Waited for Tom Rolt's implementation on `feature/recursive-search` branch and then wrote 7 pure-function tests for the ApiExplorer MVU `update` function covering two new features:

**Recursive expansion tests (4 tests):**
- `NodeExpanded sets parent endpoints` — verifies that `NodeExpanded` with endpoints parameter correctly updates the parent node's Endpoints field
- `NodeExpanded with nested path updates correct node` — tests expansion of a nested child node (Player/TransformComponent0), verifying the correct node in the tree is updated with both children and endpoints
- `ToggleExpand on expanded child collapses it` — verifies that toggling an already-expanded child node collapses it
- `ToggleExpand on unexpanded child with no children triggers expand` — verifies that toggling an unexpanded node with no children dispatches an ExpandNode command

**Search filter tests (3 tests):**
- `SetSearchQuery updates model` — verifies SearchQuery field is set correctly
- `SetSearchQuery with empty string clears search` — verifies empty search string resets to default
- `Initial model has empty SearchQuery` — verifies init() function includes SearchQuery = ""

**Key implementation changes tested:**
- `NodeExpanded` message now includes `endpoints: Endpoint list option` parameter (4th parameter)
- Model includes new `SearchQuery: string` field (initialized to "")
- New `SetSearchQuery of string` message updates the SearchQuery field
- The update function sets the node's Endpoints field when expanding (line 255 in ApiExplorer.fs)

**Pattern followed:** All tests use the existing test pattern from ApiExplorerUpdateTests.fs — create model, call `update msg model`, assert on returned model. Used `makeNode` helper and `connectedModel()` for consistent test setup.

**Results:** All 87 tests passed (14 original + 7 new + others).

**Commit:** 839a8f0 on `feature/recursive-search`

### 2025-07-25: Elmish Migration, SQLite Persistence, and Polling Tests

Wrote 12 new tests on branch `feature/elmish-sqlite` covering three migration areas: Elmish update function changes, SQLite persistence pure functions, and polling/serial behavior.

**Loco change behavior (3 tests):**
- `LocoDetected with different loco clears polling values` — verifies PollingValues becomes Map.empty when loco changes
- `LocoDetected with different loco reloads bindings config` — verifies BindingsConfig is reloaded and new loco has no bindings
- `LocoDetected with same loco does not clear polling values` — verifies PollingValues remain intact when loco is unchanged

**Serial value mapping (3 tests):**
- `PollValueReceived with value containing 1 updates PollingValues` — verifies model state for "1" values
- `PollValueReceived with value containing 0 updates PollingValues` — verifies model state for "0" values
- `PollValueReceived with unchanged value does not trigger change` — verifies no-op when value is same

**BindEndpoint immediate poll (2 tests):**
- `BindEndpoint returns poll command when api config present` — verifies non-empty Cmd returned
- `BindEndpoint returns no command when api config absent` — verifies Cmd.none when ApiConfig is None

**Pure in-memory persistence functions (4 tests):**
- `addBinding adds to empty config` — verifies new loco and binding created
- `addBinding does not duplicate` — verifies idempotent behavior
- `removeBinding removes specific endpoint` — verifies targeted removal
- `removeBinding is no-op for missing endpoint` — verifies no change for non-existent binding

**Key observations:**
- The `update` function follows Elmish convention: `update (msg: Msg) (model: Model) -> Model * Cmd<Msg>`
- `LocoDetected` with different loco calls `BindingPersistence.load()` which hits the real SQLite DB — tests still work because empty DB returns empty config
- `PollValueReceived` serial side-effects only fire when `model.SerialPort` is `Some` with open port — tests are safe with default `None`
- `Cmd.none` in Elmish is an empty list `[]`, so `List.isEmpty` check works correctly

**Results:** All 118 tests passed (106 original + 12 new).

**Commit:** 4e70b9a on `feature/elmish-sqlite`

### Elmish + SQLite Comprehensive Test Suite (2026-02-25)
**Date:** 2026-02-25  
**Task:** Write full test coverage for Elmish migration, SQLite persistence, polling behavior, and test isolation fixes

**Test Categories:**

**Pure in-memory binding mutations (6 tests):**
- `addBinding adds to empty BindingsConfig` — loco created, binding added
- `addBinding idempotent` — duplicate binding not added twice
- `removeBinding removes specific endpoint` — targeted removal
- `removeBinding no-op for missing` — no crash on non-existent binding
- `BindingsConfig load/save roundtrip` — serialization fidelity
- `SQLite auto-migration from JSON` — detects JSON, hydrates DB, deletes JSON

**Polling integration (4 tests):**
- `PollingTick queries all active bindings` — correct Cmd dispatches
- `PollingValueReceived sends on change` — serial output only on value change
- `PollingValueReceived value="1" maps to send "s"` — serial protocol mapping
- `PollingValueReceived value="0" maps to send "c"` — serial protocol mapping

**Loco lifecycle (3 tests):**
- `LocoDetected different loco resets polling state` — PollingValues cleared
- `LocoDetected different loco reloads bindings` — fresh config loaded from DB
- `LocoDetected same loco preserves polling values` — idempotent behavior

**Test Isolation Fixes (implemented by Coordinator):**
- Discovered: `addBinding`/`removeBinding` were calling `BindingPersistence.load()` during tests → broke isolation
- **Solution:** Made binding mutations pure in-memory, DB flush is explicit (`flushBindingsToDb`)
- **Result:** 1 failing test (`UnbindEndpoint removes binding`) recovered
- **Impact:** Test suite now stable, no cross-test contamination

**Status:** ✅ In progress, targeting completion this batch (106+ tests passing)

### 2027-02-27: Port Detection Module Implementation (TDD)

**Date:** 2027-02-27  
**Branch:** feature/port-detection  
**Task:** Implement Arduino/USB COM port detection module

**TDD Approach:**
- Wrote 17 tests FIRST covering all pure functions and classification logic
- Tests initially failed (red phase) as expected
- Implemented PortDetection.fs to make all tests pass (green phase)

**Module Structure:**
- **File:** AWSSunflower/PortDetection.fs (after SerialPort.fs, before Components.fs)
- **Namespace:** CounterApp
- **Types:** KnownVids module, UsbDeviceInfo record, DetectedPort record, DetectionResult DU
- **Functions:** tryParseVidPid, isArduinoVid, detectPorts, classifyPorts, detectArduino, portDisplayName

**Key Design:**
- **Registry-based detection:** Reads HKLM\SYSTEM\CurrentControlSet\Enum\USB to match COM ports to VID/PID
- **Graceful fallback:** If registry access fails (SecurityException/UnauthorizedAccessException), returns bare port names
- **Pure functions:** tryParseVidPid, isArduinoVid, classifyPorts are fully testable without hardware
- **classifyPorts extracted:** Separates classification logic from I/O for testability
- **InternalsVisibleTo:** Added to AWSSunflower.fsproj to allow test project access

**Test Coverage (17 tests, all passing):**
1-5. tryParseVidPid: Valid VID/PID parsing (2 cases), invalid input, empty string, case insensitivity
6-12. isArduinoVid: Arduino LLC, CH340, FTDI, CP210x (true cases), unknown VID (false), case insensitivity
13-16. classifyPorts: Empty list → NoPorts, single Arduino → SingleArduino, multiple Arduinos → MultipleArduinos, no Arduino → NoArduinoFound
17-18. portDisplayName: With USB info displays "COM3 — Arduino Uno", without displays "COM3"

**Test Project Setup:**
- Created AWSSunflower.Tests/ with xUnit framework
- Added project reference to AWSSunflower (Exe project)
- Required InternalsVisibleTo attribute in AWSSunflower.fsproj for visibility
- All tests use qualified names (PortDetection.tryParseVidPid) due to module vs namespace distinction

**Challenges:**
- Initially used `vidMatch.Groups.[1]` for both VID and PID (copy-paste error) — caught before commit
- Test project visibility: Exe projects have internal-only visibility by default, needed InternalsVisibleTo
- Module qualification: F# requires PortDetection.X when opening namespace CounterApp (not `open CounterApp.PortDetection`)

**Outcome:** ✅ All 17 tests pass. Module ready for integration into AWSSunflower UI. Registry detection works on Windows, gracefully falls back if registry unavailable.

**Commit:** e3cfe9d on `feature/port-detection`

### 2026-02-27: Test Infrastructure Cleanup (R-E1 through R-E6)

**Date:** 2026-02-27  
**Branch:** refactor/test-helpers  
**Task:** Consolidate duplicated test infrastructure across 7 test files

**Changes Made:**

- **R-E1:** Created `TSWApi.Tests/TestHelpers.fs` as a shared module with `MockHandler`, `CallbackMockHandler`, mock client factories (`mockClient`, `capturingHandler`, `constantClient`, `sequentialClient`, `errorClient`), `makeResponse`, `valueJson`, shared `testConfig`, and a `TestJson` module with shared JSON fixtures
- **R-E2:** Removed CommKey boilerplate from 15 tests in `HttpTests.fs` — each test had a `match CommKey.create ... with | Ok key -> ... | Error e -> Assert.Fail(...)` wrapper adding one nesting level. Renamed local `MockHandler` to `CapturingMockHandler` (captures body, method, content-type) since it's richer than the shared one
- **R-E3:** Extracted `testMapping` and `testCommandSet` at module level in `CommandMappingTests.fs`, replacing 3 identical inline definitions in translate tests
- **R-E4:** Removed duplicate `testConfig` definitions from `ApiClientTests.fs`, `SubscriptionTests.fs`, and `ApiExplorerUpdateTests.fs`; all now use shared `testConfig` from `TestHelpers`
- **R-E6:** Moved shared JSON test data (`info`, `listWithEndpoints`, `getResponse`) into `TestHelpers.TestJson` module, replacing near-identical definitions in `TypesTests.fs` and `ApiClientTests.fs`

**Key Decision:** Kept `CapturingMockHandler` local to `HttpTests.fs` rather than adding it to `TestHelpers`. Only `HttpTests.fs` needs body/method/content-type capture; other files only need basic response mocking. This keeps `TestHelpers` focused on widely-shared utilities.

**Results:** All 200 tests pass (183 TSWApi + 17 AWSSunflower). Net reduction of ~147 lines across 7 test files.

**Commit:** 17320dd on `refactor/test-helpers`

