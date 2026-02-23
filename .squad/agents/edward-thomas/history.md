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
