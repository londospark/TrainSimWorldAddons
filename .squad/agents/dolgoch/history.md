# Dolgoch — History

## Project Context
- **Project:** TrainSimWorldAddons — F# library wrapping the Train Sim World 6 HTTP API
- **Stack:** F#, .NET 10, HTTP
- **User:** LondoSpark
- **Existing project:** AWSSunflower (Avalonia FuncUI desktop app targeting .NET 10)
- **Goal:** New F# class library implementing TSW API PRD (Phase 1: GET requests)
- **PRD location:** TSW_API_PRD.md

## Learnings
<!-- Append learnings below -->

### CommandMapping Abstraction Layer (2025-07-23)
**Date:** 2025-07-23
**Branch:** feature/command-abstraction

Implemented the CommandMapping abstraction layer per Talyllyn's ADR to replace hardcoded serial command logic with extensible, type-safe abstraction.

**Key Design Patterns:**
- **Semantic Action Layer:** `Action` DU (Activate/Deactivate/SetValue/Pulse) separates "what happened" from "what to send"
- **ValueInterpreter:** Boolean uses EXACT match ("1"/"True"/"true" only) — fixes existing `value.Contains("1")` bug that matched "10", "21", etc.
- **Option-based Pipeline:** `translate : AddonCommandSet -> string -> string -> SerialCommand option` composes lookup → interpret → map with Option.bind
- **Concrete Addons:** `AWSSunflowerCommands.commandSet` defines endpoint mappings, interpreter, and reset command

**Test-Driven Development:**
- Wrote all 29 tests FIRST (per TDD charter)
- Tests cover: Boolean exact match, Continuous float parsing, Mapped enum, translate pipeline, toWireString, resetCommand
- Integration tests verify AWSSunflower addon: "1" → "s", "0" → "c", "10" → None (bug fix!), reset → "c"
- All 156 tests pass (87 existing + 29 new)

**File Structure:**
- `AWSSunflower/CommandMapping.fs` — added after SerialPort.fs, before Components.fs in compile order
- `TSWApi.Tests/CommandMappingTests.fs` — tests in existing test project (already references AWSSunflower.fsproj)

**Status:** ✅ Ready for merge

### Http.fs Typestate Refactor (Issue #23)
**Date:** 2025-01-XX
**Branch:** feature/typestate-refactor

Updated HTTP client infrastructure to work with validated types:

**Function Signature Changes:**
- `discoverCommKey`: Now returns `Result<CommKey, ApiError>` instead of `Result<string, ApiError>`
  - Final validation step uses `CommKey.create key` to wrap discovered string
  - Catches edge case of empty CommAPIKey.txt files at discovery time
- `createConfig`: Now returns `Result<ApiConfig, ApiError>` instead of bare `ApiConfig`
  - Uses `Result.map` to compose validated `CommKey` with pre-validated `BaseUrl.defaultUrl`
- `createConfigWithUrl`: Now returns `Result<ApiConfig, ApiError>` 
  - Pattern matches on both `BaseUrl.create` and `CommKey.create` results
  - Returns first error encountered (URL validated before key)
- `sendRequest`: Signature unchanged, implementation uses value accessors
  - `BaseUrl.value config.BaseUrl` extracts validated string for URL construction
  - `CommKey.value config.CommKey` extracts validated string for header

**Result Composition Patterns:**
- Used `Result.map` for single-value transformation
- Used explicit pattern matching for multi-value validation with early error return
- Maintained backward compatibility for error types (no new NetworkError/HttpError cases)

**No Changes Needed:**
- `ApiClient.fs` functions unchanged — they use `ApiConfig` opaquely
- `TreeNavigation.fs` unchanged — no direct config usage

### JSON → SQLite Migration (2026-02-25)
**Date:** 2026-02-25  
**Task:** Replace hand-rolled JSON binding persistence with Microsoft.Data.Sqlite

**Implementation:**
- Created `TSWApi/BindingPersistence.fs` with `Microsoft.Data.Sqlite` (no EF overhead)
- DB location: `%APPDATA%\LondoSpark\AWSSunflower\bindings.db`
- Schema: `Locos` and `BoundEndpoints` tables with FK relationship
- Connection strategy: Open/close per operation (stateless)
- **Auto-migration:** Detects existing `bindings.json`, hydrates SQLite, deletes JSON (zero admin cost)
- **Public API unchanged:** `load()`, `save()`, `addBinding()`, `removeBinding()` — drop-in replacement

**Code Patterns:**
- Used `using` statements for safe connection cleanup
- Parameterized queries to prevent SQL injection
- Transactional operations for atomic updates
- Silent fallback to empty config if DB errors (graceful degradation)

**Testing Notes:**
- Existing binding tests pass without modification (API contract preserved)
- Test isolation fixed: binding mutations are now pure in-memory (DB flush is explicit)
- Edward Thomas writing comprehensive SQLite CRUD tests on feature/elmish-sqlite

**Status:** ✅ Ready for merge
