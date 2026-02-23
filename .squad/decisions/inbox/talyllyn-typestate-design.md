# ADR: Typestate / DDD Pattern for ApiConfig

**Status:** Proposed  
**Author:** Talyllyn (Lead)  
**Date:** 2025-07-15  
**Issue:** #21  

## Context

`ApiConfig` is currently a plain record with bare `string` fields:

```fsharp
type ApiConfig = { BaseUrl: string; CommKey: string }
```

This permits illegal states: empty strings, whitespace-only keys, malformed URLs, and nulls all compile without complaint. Consumers discover these errors at runtime — typically as cryptic HTTP failures — rather than at the type-system boundary where they belong.

The goal is **make illegal states unrepresentable at compile time**. Once a consumer holds a valid `ApiConfig`, every subsequent API call is type-safe with no further validation needed.

## Decision

Adopt the **single-case DU with private constructors** pattern (standard F# DDD). Introduce two opaque value types — `BaseUrl` and `CommKey` — each with smart constructors returning `Result`. Keep `ApiConfig` as a public record whose fields are these validated types.

### Why This Approach

| Alternative | Verdict |
|---|---|
| **Private record + smart constructor on ApiConfig itself** | Less granular. Can't validate URL and key independently. `discoverCommKey` would return `string`, not `CommKey`. |
| **Opaque ApiConfig (private record)** | Over-constrains. If both fields are already validated types, the record is valid by construction. Making it private adds ceremony without safety. |
| **Phantom type / GADT typestate** | F# doesn't support GADTs natively. Encoding via interfaces is complex and un-idiomatic. |
| **Single-case DUs with private constructors** ✅ | Idiomatic F# DDD. Each value validates independently. Composes naturally with `Result`. Battle-tested pattern. |

### Why HttpClient Stays Separate

`HttpClient` is a .NET infrastructure concern orthogonal to configuration. Including it in `ApiConfig` would:
- Complicate lifetime management (`HttpClient` should be long-lived and reused)
- Violate SRP — config represents *what to connect to*, not *how to connect*
- Make testing harder (config becomes entangled with HTTP mocking)

## New Type Hierarchy

All types defined in `Types.fs` within the existing `[<AutoOpen>] module Types`:

```fsharp
// ── Error types ──

type ApiError =
    | NetworkError of exn
    | HttpError of status: int * message: string
    | AuthError of string
    | ParseError of string
    | ConfigError of string   // NEW: invalid configuration values (e.g., malformed URL)

// ── Validated value types ──

/// A validated base URL for the TSW API.
type BaseUrl = private BaseUrl of string

/// Companion module for BaseUrl.
[<RequireQualifiedAccess>]
module BaseUrl =
    /// Pre-validated default URL. No Result wrapper needed.
    let defaultUrl : BaseUrl = BaseUrl "http://localhost:31270"

    /// Validate and create a BaseUrl.
    /// Must be non-empty and start with http:// or https://.
    /// Trailing slashes are normalized away.
    let create (url: string) : Result<BaseUrl, ApiError> =
        if System.String.IsNullOrWhiteSpace(url) then
            Error(ConfigError "Base URL cannot be empty")
        elif not (url.StartsWith("http://") || url.StartsWith("https://")) then
            Error(ConfigError $"Base URL must start with http:// or https://: '{url}'")
        else
            Ok(BaseUrl(url.TrimEnd('/')))

    /// Extract the validated string value.
    let value (BaseUrl url) : string = url


/// A validated DTGCommKey authentication token.
type CommKey = private CommKey of string

/// Companion module for CommKey.
[<RequireQualifiedAccess>]
module CommKey =
    /// Validate and create a CommKey.
    /// Must be non-empty after trimming whitespace.
    let create (key: string) : Result<CommKey, ApiError> =
        if System.String.IsNullOrWhiteSpace(key) then
            Error(AuthError "CommKey cannot be empty")
        else
            Ok(CommKey(key.Trim()))

    /// Extract the validated string value.
    let value (CommKey key) : string = key


// ── Configuration ──

/// API configuration. All fields are validated — illegal states are unrepresentable.
/// Construct via Http.createConfig / Http.createConfigWithUrl, or directly
/// using validated BaseUrl and CommKey values.
type ApiConfig = { BaseUrl: BaseUrl; CommKey: CommKey }
```

### Privacy Model

| Scope | Can construct `BaseUrl`/`CommKey` directly? | Can read via `.value`? |
|---|---|---|
| Inside `Types.fs` (same module) | ✅ Yes — private is scoped to enclosing module | ✅ Yes |
| Inside `Http.fs` / `ApiClient.fs` (same assembly) | ❌ No — must use smart constructors | ✅ Yes — `BaseUrl.value`, `CommKey.value` are public |
| External consumers | ❌ No | ✅ Yes |

This means: **the only way to get a `BaseUrl` or `CommKey` is through validation**. Once you have them, you're in the "validated" state permanently.

## Updated API Function Signatures

### Http module (`Http.fs`)

```fsharp
/// Discover CommKey from filesystem. Now returns validated CommKey directly.
val discoverCommKey : myGamesPath:string -> Result<CommKey, ApiError>

/// Create config with default URL. Now validates the CommKey.
val createConfig : commKey:string -> Result<ApiConfig, ApiError>

/// Create config with custom URL. Now validates both inputs.
val createConfigWithUrl : baseUrl:string -> commKey:string -> Result<ApiConfig, ApiError>

/// Send request. Signature unchanged — ApiConfig fields are now validated types.
val sendRequest<'T> : HttpClient -> ApiConfig -> string -> Async<ApiResult<'T>>
```

#### Implementation Changes in `Http.fs`

```fsharp
let discoverCommKey (myGamesPath: string) : Result<CommKey, ApiError> =
    // ... existing filesystem discovery logic ...
    // Final line changes from:
    //   Ok key
    // To:
    //   CommKey.create key
    // This validates the discovered key (catches empty CommAPIKey.txt files)

let createConfig (commKey: string) : Result<ApiConfig, ApiError> =
    CommKey.create commKey
    |> Result.map (fun key -> { BaseUrl = BaseUrl.defaultUrl; CommKey = key })

let createConfigWithUrl (baseUrl: string) (commKey: string) : Result<ApiConfig, ApiError> =
    match BaseUrl.create baseUrl, CommKey.create commKey with
    | Ok url, Ok key -> Ok { BaseUrl = url; CommKey = key }
    | Error e, _     -> Error e
    | _, Error e     -> Error e

let sendRequest<'T> (client: HttpClient) (config: ApiConfig) (path: string) : Async<ApiResult<'T>> =
    async {
        // Changed lines:
        let url = $"{BaseUrl.value config.BaseUrl}{path}"
        // ...
        request.Headers.Add("DTGCommKey", CommKey.value config.CommKey)
        // ... rest unchanged
    }
```

### ApiClient module (`ApiClient.fs`)

**No signature changes.** All three functions take `ApiConfig` as before — the type is the same name, just with validated fields now. The functions themselves don't touch `BaseUrl` or `CommKey` directly; they delegate to `sendRequest`.

```fsharp
val getInfo   : HttpClient -> ApiConfig -> Async<ApiResult<InfoResponse>>
val listNodes : HttpClient -> ApiConfig -> string option -> Async<ApiResult<ListResponse>>
val getValue  : HttpClient -> ApiConfig -> string -> Async<ApiResult<GetResponse>>
```

## Consumer Migration Guide

### Before (current)

```fsharp
// Anything compiles — errors surface at runtime
let config = Http.createConfig ""           // compiles, fails at runtime
let config2 = { BaseUrl = ""; CommKey = "" } // compiles, fails at runtime
let! info = ApiClient.getInfo client config  // runtime HTTP error
```

### After (proposed)

```fsharp
// Validation at the boundary — Result forces you to handle errors
let configResult = Http.createConfig "my-comm-key"

match configResult with
| Ok config ->
    // From here on, everything is compile-time safe
    let! info = ApiClient.getInfo client config
    let! nodes = ApiClient.listNodes client config None
    // ...
| Error e ->
    printfn "Config error: %A" e

// Or with auto-discovery:
let configResult =
    Http.discoverCommKey myGamesPath
    |> Result.map (fun commKey -> { BaseUrl = BaseUrl.defaultUrl; CommKey = commKey })

// Or compose validated values directly:
match CommKey.create "my-key", BaseUrl.create "http://192.168.1.50:31270" with
| Ok key, Ok url ->
    let config = { BaseUrl = url; CommKey = key }
    // ...
| _ -> // handle errors
```

### Breaking Changes Summary

| Change | Impact |
|---|---|
| `ApiConfig` fields change from `string` to `BaseUrl`/`CommKey` | All direct record construction breaks — use smart constructors |
| `createConfig` returns `Result<ApiConfig, ApiError>` (was `ApiConfig`) | Callers must handle `Result` |
| `createConfigWithUrl` returns `Result<ApiConfig, ApiError>` (was `ApiConfig`) | Callers must handle `Result` |
| `discoverCommKey` returns `Result<CommKey, ApiError>` (was `Result<string, ApiError>`) | `Ok` branch now holds `CommKey`, not `string` |
| New `ConfigError` case on `ApiError` | Exhaustive pattern matches need a new branch |

## Test Impact Analysis

**53 total tests** across 4 test files. **14 tests need updating**, **39 are unaffected**.

### TreeNavigationTests.fs — 0 of 16 affected ✅
No `ApiConfig` usage. Entirely unaffected.

### TypesTests.fs — 1 of 19 affected

| Test | Change Required |
|---|---|
| `ApiConfig defaults to localhost` | Construct via `Http.createConfig` or validated types instead of record literal |

Additionally: **add new tests** for `BaseUrl.create`, `CommKey.create`, and the `ConfigError` DU case.

### HttpTests.fs — 7 of 12 affected

| Test | Change Required |
|---|---|
| `createConfig uses default base URL` | Unwrap `Result`; assert `BaseUrl`/`CommKey` via `.value` accessors |
| `createConfigWithUrl sets custom base URL` | Unwrap `Result`; assert `BaseUrl.value` |
| `sendRequest adds DTGCommKey header` | Construct config via smart constructors |
| `sendRequest constructs correct URL` | Construct config via smart constructors |
| `sendRequest returns Ok on successful response` | Construct config via smart constructors |
| `sendRequest returns HttpError on non-success status` | Construct config via smart constructors |
| `sendRequest returns ParseError on invalid JSON` | Construct config via smart constructors |

The 5 `discoverCommKey` tests remain structurally the same but now assert `CommKey` values instead of raw strings.

### ApiClientTests.fs — 6 of 6 affected

| Test | Change Required |
|---|---|
| All 6 tests | Change `testConfig` from record literal to smart-constructor-based creation (single-line fix, all tests benefit) |

### New Tests to Add

| Test | Purpose |
|---|---|
| `BaseUrl.create rejects empty string` | Validates smart constructor |
| `BaseUrl.create rejects non-http URL` | Validates protocol check |
| `BaseUrl.create trims trailing slash` | Validates normalization |
| `BaseUrl.create accepts valid http URL` | Happy path |
| `BaseUrl.create accepts valid https URL` | Happy path |
| `BaseUrl.defaultUrl is localhost:31270` | Pre-validated constant |
| `CommKey.create rejects empty string` | Validates smart constructor |
| `CommKey.create rejects whitespace-only` | Validates smart constructor |
| `CommKey.create trims whitespace` | Validates trimming |
| `CommKey.create accepts valid key` | Happy path |
| `createConfig rejects empty commKey` | Integration: factory + validation |
| `createConfigWithUrl rejects invalid URL` | Integration: factory + validation |
| `discoverCommKey returns CommKey not string` | Return type change |

## File Change Summary

| File | Changes |
|---|---|
| `TSWApi/Types.fs` | Add `BaseUrl` type + module, `CommKey` type + module, `ConfigError` case |
| `TSWApi/Http.fs` | Update `discoverCommKey`, `createConfig`, `createConfigWithUrl`, `sendRequest` |
| `TSWApi/ApiClient.fs` | **No changes** (signatures use `ApiConfig` which is the same name) |
| `TSWApi/TreeNavigation.fs` | **No changes** |
| `TSWApi.Tests/TypesTests.fs` | Update 1 test, add ~10 new validation tests |
| `TSWApi.Tests/HttpTests.fs` | Update 7 tests, update 5 discoverCommKey assertions |
| `TSWApi.Tests/ApiClientTests.fs` | Update `testConfig` definition (1 line, fixes all 6 tests) |
| `TSWApi.Tests/TreeNavigationTests.fs` | **No changes** |

## Risks and Mitigations

| Risk | Mitigation |
|---|---|
| `ConfigError` breaks exhaustive matches downstream | AWSSunflower is the only known consumer; update during integration |
| `Result` return types add ceremony | Provide convenience `createConfig` functions; F# `result {}` CE available |
| `BaseUrl.value` / `CommKey.value` leak raw strings | Acceptable — reads are safe; the invariant is on *construction*, not *observation* |
| Compilation order sensitivity in `Types.fs` | Type + companion module pattern is well-supported in modern F# (.NET 10) |

## Recommendation

Implement in a single PR against `develop`. TDD approach: write the new validation tests first (red), then implement the types (green), then update existing tests. The change is mechanical but touches many test files, so careful review is needed.
