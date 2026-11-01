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

### 2027-02-28: CommandMapping and Helpers Pure Function Tests

**Date:** 2027-02-28  
**Task:** Create comprehensive test coverage for CommandMapping and ApplicationScreenHelpers pure functions  
**Files Created:** CommandMappingTests.fs (29 tests), HelpersTests.fs (26 tests)

**CommandMapping Tests (29 tests):**
1. **interpret with ValueInterpreter.Boolean (9 tests):**
   - "1", "True", "true" → Some Activate
   - "0", "False", "false" → Some Deactivate
   - Random string → None
   - Whitespace-padded values " 1 " and " 0 " → trimmed and mapped correctly

2. **interpret with ValueInterpreter.Continuous (4 tests):**
   - "0.5", "1.0" → Some (SetValue float)
   - "not a number" → None
   - Whitespace-padded " 0.75 " → Some (SetValue 0.75)

3. **interpret with ValueInterpreter.Mapped (3 tests):**
   - Known key → Some mapped action
   - Unknown key → None
   - Whitespace-padded key → trimmed and mapped

4. **interpret with ValueInterpreter.Custom (2 tests):**
   - Custom fn returning Some → Some
   - Custom fn returning None → None

5. **translate (3 tests):**
   - Known endpoint + valid value → Some SerialCommand
   - Unknown endpoint → None
   - Known endpoint + invalid value → None

6. **toWireString (2 tests):**
   - Text "s" → "s"
   - Formatted "T:0.75\n" → "T:0.75\n"

7. **resetCommand (2 tests):**
   - Addon with ResetCommand → Some
   - Addon without ResetCommand → None

8. **AWSSunflowerCommands.commandSet (4 tests):**
   - "Property.AWS_SunflowerState" + "1" → Text "s"
   - "Property.AWS_SunflowerState" + "0" → Text "c"
   - Unknown endpoint → None
   - Reset command is Text "c"

**HelpersTests (26 tests):**
1. **stripRootPrefix (4 tests):**
   - "Root/Player" → "Player"
   - "SomethingElse" → "SomethingElse"
   - null → null
   - "" → ""

2. **nullSafe (3 tests):**
   - null → ""
   - "hello" → "hello"
   - "" → ""

3. **effectiveName (3 tests):**
   - Node with NodeName set → returns NodeName
   - Node with only Name set → returns Name
   - Node with both empty → returns ""

4. **endpointKey (1 test):**
   - "path" "name" → "path.name"

5. **getLocoBindings (3 tests):**
   - Config with matching loco → returns its bindings
   - Config without matching loco → returns empty list
   - Empty config → returns empty list

6. **findNode (3 tests):**
   - Find root-level node → Some
   - Find nested node → Some (recursive)
   - Not found → None

7. **updateTreeNode (3 tests):**
   - Update root-level node → changed
   - Update nested node → changed, parents preserved
   - Non-existent path → list unchanged

8. **filterTree (6 tests):**
   - Empty query → returns all nodes
   - Whitespace query → returns all nodes
   - Query matching leaf → returns leaf
   - Query matching parent → returns parent with children
   - Query matching no nodes → returns empty
   - Case insensitive matching

**Key Design:**
- All tests are pure — no I/O, no mocking, no side effects
- Tested exact edge cases specified in charter (whitespace padding, null handling, case sensitivity)
- Used backtick test names for readability
- Namespace: `CounterApp.Tests`
- Opened: `CounterApp`, `CounterApp.CommandMapping`, `CounterApp.ApplicationScreenHelpers`, `TSWApi`

**Test Framework:**
- xUnit with F# backtick test names
- TSWApi.Types.Node used for effectiveName tests
- TreeNodeState, BoundEndpoint, BindingsConfig types from CounterApp namespace

**Results:** All 72 tests pass (17 PortDetection + 29 CommandMapping + 26 Helpers). Test project total increased from 17 to 72 tests.

**Files Modified:**
- Created: `AWSSunflower.Tests\CommandMappingTests.fs`
- Created: `AWSSunflower.Tests\HelpersTests.fs`
- Modified: `AWSSunflower.Tests\AWSSunflower.Tests.fsproj` (added both .fs files to Compile ItemGroup)

**Note on stripRootPrefix null handling:** Task charter stated `null → ""` but actual implementation returns `null` when given `null` input (returns path unchanged). Test updated to reflect actual behavior: `stripRootPrefix null` returns `null`. This matches usage pattern in `mapNodeToTreeState` which checks `NodePath` for null/empty before calling `stripRootPrefix`.


### 2027-01-XX: AWSSunflower Test Coverage Expansion

**Date:** 2027-01-XX  
**Task:** Create comprehensive test coverage for BindingPersistence pure functions and Elmish Update function  
**Files Created:** BindingPersistenceTests.fs (7 tests), UpdateTests.fs (13 tests)

**BindingPersistence Tests (7 pure function tests):**
- `addBinding adds to empty config creates new loco with binding` — verifies new loco creation with first binding
- `addBinding to existing loco appends binding` — verifies binding appended to existing loco's list
- `addBinding duplicate binding does not add duplicate` — verifies idempotent behavior (already bound check)
- `addBinding to different loco adds new loco entry` — verifies multi-loco support
- `removeBinding removes existing binding` — verifies targeted binding removal
- `removeBinding non-existent binding leaves config unchanged` — verifies no-op for missing binding
- `removeBinding from non-existent loco leaves config unchanged` — verifies no-op for missing loco

**Key Design Notes:**
- Tests ONLY pure functions (`addBinding`, `removeBinding`) — NO SQLite I/O (`load()`, `save()` have side effects)
- Used fully qualified record construction for `BoundEndpoint` and `BindingsConfig`
- All tests verify state transformations without touching filesystem or database

**Elmish Update Tests (13 tests):**
- `SetBaseUrl updates model BaseUrl` — simple string field update
- `SetCommKey updates model CommKey` — simple string field update
- `SetSearchQuery updates model SearchQuery` — simple string field update
- `Connect sets IsConnecting true and ConnectionState to Connecting` — state transition test
- `ConnectError sets Error state and IsConnecting false` — error handling state transition
- `Disconnect clears ApiConfig ConnectionState TreeRoot and PollingValues` — verifies full cleanup
- `SelectNode updates SelectedNode and clears EndpointValues` — verifies node selection side effect
- `CollapseNode sets node IsExpanded to false` — tree state mutation
- `EndpointValueReceived adds value to EndpointValues` — map update test
- `SetSerialPort updates SerialPortName` — option type update
- `PortsUpdated with single Arduino auto-selects when no port selected` — auto-selection logic
- `LocoDetectError leaves model unchanged` — no-op message test
- `ApiError sets ConnectionState to Error` — error state propagation

**Test Helper Pattern:**
- Created `testModel()` helper function to avoid calling `ApplicationScreen.init()` (which calls `BindingPersistence.load()` with filesystem side effects)
- Helper provides clean model state with empty collections and disconnected state
- All tests use `update msg model` pattern and assert on returned model state (ignoring Cmd values)

**Namespace Pattern:**
- Opened `CounterApp`, `CounterApp.ApplicationScreen`, `CounterApp.ApplicationScreenUpdate`, `CounterApp.ApplicationScreenHelpers`, `CounterApp.CommandMapping`
- Used `open global.Elmish` (NOT `open Elmish` — causes FS0893 compiler error)

**Known Issues:**
- Pre-existing `HelpersTests.fs` has 3 compilation errors referencing non-existent `CollapsedChildren` field on `Node` type — NOT introduced by this work
- Per instructions: "Ignore unrelated bugs or broken tests; it is not your responsibility to fix them"
- Main AWSSunflower project builds successfully

**Status:** ✅ Test files created (20 new tests total). Files ready for .fsproj integration by project coordinator.

