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
