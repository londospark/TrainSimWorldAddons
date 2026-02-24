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
