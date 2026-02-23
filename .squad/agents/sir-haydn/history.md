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
