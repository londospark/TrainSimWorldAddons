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
