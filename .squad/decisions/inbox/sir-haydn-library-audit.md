# Deep F# Idiomaticity Audit — TSWApi Library

**Author:** Sir Haydn (Library Dev)  
**Date:** 2025-07-24  
**Requested by:** LondoSpark  
**Scope:** TSWApi/Types.fs, Http.fs, ApiClient.fs, TreeNavigation.fs, Subscription.fs + Tests  
**Baseline:** 275 tests passing (92 AWSSunflower + 183 TSWApi)

---

## 1. `asyncResult {}` CE — FsToolkit.ErrorHandling

**Severity:** Medium — Reduces nesting, improves readability  
**Status:** FsToolkit.ErrorHandling v5.2.0 is already a dependency but only used in `createConfigWithUrl`

### Finding 1a: `sendRequestWithMethod` in Http.fs

The core HTTP function uses manual `async { try ... return Ok/Error }` with nested try/catch. The `asyncResult {}` CE would flatten this significantly.

**Current code (Http.fs lines 82–110):**
```fsharp
let sendRequestWithMethod<'T> (client: HttpClient) (config: ApiConfig) (httpMethod: HttpMethod) (path: string) (body: string option) : Async<ApiResult<'T>> =
    async {
        try
            let url = $"{BaseUrl.value config.BaseUrl}{path}"
            use request = new HttpRequestMessage(httpMethod, url)
            request.Version <- Version(1, 1)
            request.Headers.Add("DTGCommKey", CommKey.value config.CommKey)

            body |> Option.iter (fun content ->
                request.Content <- new StringContent(content, Text.Encoding.UTF8, "application/json"))

            let! response =
                client.SendAsync(request) |> Async.AwaitTask

            if not response.IsSuccessStatusCode then
                let! body = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                return Error(HttpError(int response.StatusCode, body))
            else
                let! body = response.Content.ReadAsStringAsync() |> Async.AwaitTask

                try
                    let result = JsonSerializer.Deserialize<'T>(body, jsonOptions)
                    return Ok result
                with ex ->
                    return Error(ParseError $"Failed to parse response: {ex.Message}")
        with ex ->
            return Error(NetworkError ex)
    }
```

**Idiomatic version:**
```fsharp
let sendRequestWithMethod<'T> (client: HttpClient) (config: ApiConfig) (httpMethod: HttpMethod) (path: string) (body: string option) : Async<ApiResult<'T>> =
    asyncResult {
        let url = $"{BaseUrl.value config.BaseUrl}{path}"
        use request = new HttpRequestMessage(httpMethod, url)
        request.Version <- Version(1, 1)
        request.Headers.Add("DTGCommKey", CommKey.value config.CommKey)

        body |> Option.iter (fun content ->
            request.Content <- new StringContent(content, Text.Encoding.UTF8, "application/json"))

        let! response =
            client.SendAsync(request) |> Async.AwaitTask
            |> Async.Catch
            |> Async.map (function
                | Choice1Of2 r -> Ok r
                | Choice2Of2 ex -> Error(NetworkError ex))

        if not response.IsSuccessStatusCode then
            let! errorBody =
                response.Content.ReadAsStringAsync() |> Async.AwaitTask
                |> Async.Catch
                |> Async.map (function Choice1Of2 s -> Ok s | Choice2Of2 ex -> Error(NetworkError ex))
            return! Error(HttpError(int response.StatusCode, errorBody))

        let! responseBody =
            response.Content.ReadAsStringAsync() |> Async.AwaitTask
            |> Async.Catch
            |> Async.map (function Choice1Of2 s -> Ok s | Choice2Of2 ex -> Error(NetworkError ex))

        return
            try JsonSerializer.Deserialize<'T>(responseBody, jsonOptions)
            with ex -> return! Error(ParseError $"Failed to parse response: {ex.Message}")
    }
```

**Verdict: NOT RECOMMENDED.** The current `async { try...with }` structure is actually *cleaner* here because:
- The outer try/catch elegantly maps all network exceptions to `NetworkError`
- `asyncResult {}` doesn't auto-catch exceptions — you'd need manual `Async.Catch` wrappers everywhere
- The control flow with `if not response.IsSuccessStatusCode` doesn't naturally short-circuit in `asyncResult`
- The rewrite is *longer* and *harder to read*

The current pattern is the right choice for HTTP code where exception-to-Result mapping is the primary concern.

### Finding 1b: `createConfigWithUrl` already uses `result {}`

```fsharp
let createConfigWithUrl (baseUrl: string) (commKey: string) : Result<ApiConfig, ApiError> =
    result {
        let! url = BaseUrl.create baseUrl
        let! key = CommKey.create commKey
        return { BaseUrl = url; CommKey = key }
    }
```

✅ **Already idiomatic.** This is the textbook use case for `result {}`. Good.

### Finding 1c: `discoverCommKey` in Http.fs

**Current code (Http.fs lines 24–54):**
```fsharp
let discoverCommKey (myGamesPath: string) : Result<CommKey, ApiError> =
    try
        let dirs = ...
        if dirs.Length = 0 then
            Error(AuthError "No TrainSimWorld directory found")
        else
            let sorted = ...
            let keyPath = Path.Combine(sorted.[0], "Saved", "Config", "CommAPIKey.txt")
            if File.Exists(keyPath) then
                let key = File.ReadAllText(keyPath).Trim()
                CommKey.create key
            else
                Error(AuthError $"CommAPIKey.txt not found at {keyPath}")
    with ex ->
        Error(AuthError $"Failed to discover CommKey: {ex.Message}")
```

**Could use `result {}`:**
```fsharp
let discoverCommKey (myGamesPath: string) : Result<CommKey, ApiError> =
    try
        result {
            let dirs =
                if Directory.Exists(myGamesPath) then
                    Directory.GetDirectories(myGamesPath, "TrainSimWorld*")
                else [||]

            do! if dirs.Length = 0 then Error(AuthError "No TrainSimWorld directory found") else Ok()

            let sorted =
                dirs |> Array.sortByDescending (fun d ->
                    let name = Path.GetFileName(d)
                    let m = Regex.Match(name, @"TrainSimWorld(\d*)")
                    if m.Success && m.Groups.[1].Value <> "" then int m.Groups.[1].Value else 0)

            let keyPath = Path.Combine(sorted.[0], "Saved", "Config", "CommAPIKey.txt")

            do! if not (File.Exists(keyPath)) then Error(AuthError $"CommAPIKey.txt not found at {keyPath}") else Ok()

            let key = File.ReadAllText(keyPath).Trim()
            return! CommKey.create key
        }
    with ex ->
        Error(AuthError $"Failed to discover CommKey: {ex.Message}")
```

**Verdict: MARGINAL.** The `do!` with inline conditionals is no cleaner than `if/else`. The outer try/catch still must remain. Current code is fine.

---

## 2. API Surface Ergonomics

**Severity:** Low-Medium

### Finding 2a: `client` and `config` threading through every call

Every API function takes `(client: HttpClient) (config: ApiConfig)` as the first two parameters. This forces consumers to thread these through every call site.

**Current usage pattern:**
```fsharp
let! info = ApiClient.getInfo client config
let! nodes = ApiClient.listNodes client config (Some "Root/Player")
let! value = ApiClient.getValue client config "Root/Player.Property.Speed"
```

**Option A — Session record (recommended):**
```fsharp
/// A connected API session bundling HttpClient + ApiConfig.
type ApiSession = { Client: HttpClient; Config: ApiConfig }

module ApiSession =
    let create client config = { Client = client; Config = config }

module ApiClient =
    let getInfo (session: ApiSession) =
        sendRequest<InfoResponse> session.Client session.Config "/info"

    let listNodes (session: ApiSession) (path: string option) =
        let endpoint = path |> Option.map (sprintf "/list/%s") |> Option.defaultValue "/list"
        sendRequest<ListResponse> session.Client session.Config endpoint

    let getValue (session: ApiSession) (path: string) =
        sendRequest<GetResponse> session.Client session.Config $"/get/{path}"
```

**Consumer simplification:**
```fsharp
let session = ApiSession.create client config
let! info = ApiClient.getInfo session
let! nodes = ApiClient.listNodes session (Some "Root/Player")
let! value = ApiClient.getValue session "Root/Player.Property.Speed"
```

**Why better:** Reduces parameter count from 3 to 2 (or 1 for getInfo). Prevents accidentally mixing different configs with different clients. Natural place to hang future state (retry policy, logging, etc).

**Verdict: RECOMMEND for Phase 2.** Breaking change — should be batched with other API changes.

### Finding 2b: Pipe-friendly parameter ordering

`findEndpoint` takes `(node: Node) (endpointName: string)` — the data parameter is first. Idiomatic F# puts the "data" parameter last for pipe-friendliness.

**Current:**
```fsharp
let findEndpoint (node: Node) (endpointName: string) : Endpoint option =
    node.Endpoints |> Option.bind (List.tryFind (fun e -> e.Name = endpointName))
```

**Idiomatic:**
```fsharp
let findEndpoint (endpointName: string) (node: Node) : Endpoint option =
    node.Endpoints |> Option.bind (List.tryFind (fun e -> e.Name = endpointName))
```

**Enables:**
```fsharp
node |> TreeNavigation.findEndpoint "Property.Speed"
// vs
TreeNavigation.findEndpoint node "Property.Speed"
```

**Verdict: RECOMMEND.** Breaking change, but small. Same applies to `getChildNodes` (though it only has one parameter so it's already pipe-friendly).

---

## 3. Error Types

**Severity:** Low

### Finding 3a: `ApiError` is well-designed

The current DU has good coverage:
- `NetworkError of exn` — connection failures
- `HttpError of status: int * message: string` — HTTP-level errors
- `AuthError of string` — authentication issues
- `ParseError of string` — deserialization failures
- `ConfigError of string` — validation errors

### Finding 3b: Consider `TimeoutError` case

Currently, HTTP timeouts surface as `NetworkError(TaskCanceledException)`. A dedicated case would enable better consumer handling:

```fsharp
type ApiError =
    | NetworkError of exn
    | TimeoutError                              // <-- NEW
    | HttpError of status: int * message: string
    | AuthError of string
    | ParseError of string
    | ConfigError of string
```

With mapping in `sendRequestWithMethod`:
```fsharp
with
| :? TaskCanceledException -> return Error TimeoutError
| ex -> return Error(NetworkError ex)
```

**Verdict: NICE TO HAVE.** Current code works. Add when consumers need to distinguish timeouts from other network errors.

### Finding 3c: Missing `toString` / `describe` helper

Consumers currently need to pattern-match every case to log errors. A utility function would help:

```fsharp
module ApiError =
    let describe = function
        | NetworkError ex -> $"Network error: {ex.Message}"
        | HttpError(status, msg) -> $"HTTP {status}: {msg}"
        | AuthError msg -> $"Auth error: {msg}"
        | ParseError msg -> $"Parse error: {msg}"
        | ConfigError msg -> $"Config error: {msg}"
```

**Verdict: RECOMMEND.** Low effort, high utility for consumers. Non-breaking.

---

## 4. Type Safety

**Severity:** Low

### Finding 4a: `BaseUrl` and `CommKey` are already wrapped ✅

Excellent typestate pattern with private constructors and smart constructors.

### Finding 4b: `NodePath` could be a wrapped type

`path: string` appears in `listNodes`, `getValue`, `getNodeAtPath`, `parseNodePath`, `buildNodePath`, etc. A `NodePath` wrapper would prevent mixing paths with arbitrary strings:

```fsharp
type NodePath = private NodePath of string

module NodePath =
    let create (path: string) = NodePath path
    let value (NodePath p) = p
    let parse (NodePath p) = if System.String.IsNullOrEmpty(p) then [] else p.Split('/') |> Array.toList
    let build (segments: string list) = System.String.Join("/", segments) |> NodePath
    let append (NodePath parent) (child: string) = NodePath $"{parent}/{child}"
```

**Verdict: DEFER.** The benefit is moderate — node paths are validated by the API itself, not locally. Would be a large breaking change across both TSWApi and AWSSunflower for marginal safety gain. Revisit if path-related bugs emerge.

### Finding 4c: `GetResponse.Values` uses `Dictionary<string, obj>`

This was already flagged in the Phase 1 review (decisions.md). `Dictionary<string, obj>` loses type information — `obj` forces consumers to cast.

**Better:**
```fsharp
open System.Text.Json

type GetResponse =
    { Result: string
      Values: Dictionary<string, JsonElement> }
```

`JsonElement` preserves the JSON type info and has `.GetInt32()`, `.GetString()`, `.GetDouble()` etc.

**Verdict: RECOMMEND.** Already identified in decisions.md. Should be done in Phase 2. Breaking change.

---

## 5. Async Cancellation

**Severity:** Medium

### Finding 5a: No CancellationToken support

All async functions ignore cancellation. `sendRequestWithMethod` uses `client.SendAsync(request)` without passing a token. Long-running tree traversals or subscriptions can't be cancelled.

**Current:**
```fsharp
let! response = client.SendAsync(request) |> Async.AwaitTask
```

**Idiomatic:**
```fsharp
let sendRequestWithMethod<'T> (client: HttpClient) (config: ApiConfig) (httpMethod: HttpMethod) (path: string) (body: string option) (ct: CancellationToken) : Async<ApiResult<'T>> =
    async {
        try
            ...
            let! response = client.SendAsync(request, ct) |> Async.AwaitTask
            ...
    }
```

**Or, using F# async's built-in cancellation:**
```fsharp
let sendRequestWithMethod<'T> ... : Async<ApiResult<'T>> =
    async {
        try
            ...
            let! ct = Async.CancellationToken
            let! response = client.SendAsync(request, ct) |> Async.AwaitTask
            ...
    }
```

The second approach (`Async.CancellationToken`) is more idiomatic F# — it lets callers use `Async.RunSynchronously(computation, cancellationToken=ct)` or `Async.Start(computation, ct)` without changing the function signature.

**Verdict: RECOMMEND using `Async.CancellationToken` approach.** Non-breaking — just thread the implicit token through to `SendAsync`. The `Subscription` module's `Async.RunSynchronously` call would also benefit.

---

## 6. HTTP Patterns

**Severity:** Low

### Finding 6a: HttpClient usage is correct

- Not creating/disposing HttpClient per request ✅ (injected)
- Setting HTTP/1.1 explicitly ✅ (TSW6 requirement)
- Using DTGCommKey header ✅
- `use request = new HttpRequestMessage(...)` — correctly disposed ✅

### Finding 6b: `HttpMethod("PATCH")` vs `HttpMethod.Patch`

**Current (Http.fs line 129):**
```fsharp
let sendPatch<'T> ... =
    sendRequestWithMethod<'T> client config (HttpMethod("PATCH")) path (Some body)
```

**Idiomatic (.NET 10):**
```fsharp
let sendPatch<'T> ... =
    sendRequestWithMethod<'T> client config HttpMethod.Patch path (Some body)
```

`HttpMethod.Patch` has been available since .NET 5. The `HttpMethod("PATCH")` constructor allocates a new object each call.

**Verdict: RECOMMEND.** Trivial fix. Non-breaking.

### Finding 6c: JSON options missing `JsonFSharpConverter`

**Current (Http.fs line 14–15):**
```fsharp
let private jsonOptions =
    JsonSerializerOptions(PropertyNameCaseInsensitive = true)
```

The project doesn't use `JsonFSharpConverter` from `FSharp.SystemTextJson`. Yet F# types like `option`, DUs, and `list` are used throughout. The current deserialization works because:
- `list` maps from JSON arrays naturally with `PropertyNameCaseInsensitive`
- `option` fields use `[<JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)>]`
- System.Text.Json has had built-in F# support since .NET 9

This actually works fine on .NET 10 without `JsonFSharpConverter`. See Finding 7 for details.

---

## 7. JSON Serialization

**Severity:** Low

### Finding 7a: System.Text.Json on .NET 10 is the right choice

Since .NET 9, System.Text.Json has native support for F# discriminated unions, records, and option types. The project targets .NET 10, so `JsonFSharpConverter` is NOT needed.

**Current approach is correct:**
- `PropertyNameCaseInsensitive = true` — handles the PascalCase JSON from TSW6 ✅
- `[<JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)>]` on optional fields — correct for `option` ✅
- No `JsonFSharpConverter` dependency — correct for .NET 10 ✅

### Finding 7b: `jsonOptions` could be `JsonSerializerDefaults.Web`

**Current:**
```fsharp
let private jsonOptions =
    JsonSerializerOptions(PropertyNameCaseInsensitive = true)
```

**Alternative:**
```fsharp
let private jsonOptions =
    JsonSerializerOptions(JsonSerializerDefaults.Web)
```

`JsonSerializerDefaults.Web` sets `PropertyNameCaseInsensitive = true` plus `camelCase` property naming and number reading from strings. The TSW API uses PascalCase, so the camelCase naming policy could cause issues with *serialization* (writing JSON). Since this library only deserializes API responses (reads), the current explicit approach is safer and more intentional.

**Verdict: KEEP CURRENT.** Explicit is better than implicit here.

---

## 8. Module Structure

**Severity:** Low

### Finding 8a: Module structure is good

```
Types.fs      — All domain types (ApiError, ApiConfig, Node, etc.)
Http.fs       — HTTP infrastructure (sendRequest, config factories, CommKey discovery)
ApiClient.fs  — High-level API operations (getInfo, listNodes, getValue)
Subscription.fs — Polling subscription layer
TreeNavigation.fs — Tree traversal helpers
```

Clean layering: Types → Http → ApiClient → Subscription/TreeNavigation.

### Finding 8b: `discoverCommKey` belongs in a separate module

`discoverCommKey` does file system I/O (reading registry-style paths). It's in `Http` module alongside pure HTTP functions. This mixes concerns.

**Suggestion:**
```fsharp
/// Discovery functions for locating TSW6 game configuration on disk.
module Discovery =
    let discoverCommKey (myGamesPath: string) : Result<CommKey, ApiError> = ...
```

**Verdict: NICE TO HAVE.** Low priority. Current placement works, just slightly impure.

### Finding 8c: `[<AutoOpen>]` on Types module

```fsharp
[<AutoOpen>]
module Types =
```

This means `open TSWApi` gives access to all types without `open TSWApi.Types`. This is the right choice for a domain types module — consumers shouldn't need to remember an extra `open`.

✅ **Already idiomatic.**

---

## 9. Documentation

**Severity:** Medium

### Finding 9a: `<GenerateDocumentationFile>true</GenerateDocumentationFile>` ✅

The fsproj enables XML doc generation. Good.

### Finding 9b: XML docs are thorough on public API

- `ApiError` — has `<example>` with pattern matching ✅
- `BaseUrl`, `CommKey` — smart constructors documented with `<summary>`, `<param>`, `<returns>` ✅
- `sendRequestWithMethod` — full `<summary>` with all `<param>` tags ✅
- `ApiClient` functions — have `///` summaries ✅
- `TreeNavigation` functions — have `///` summaries ✅
- `Subscription` types — have `///` summaries ✅

### Finding 9c: Missing `<returns>` on some ApiClient functions

**Current (ApiClient.fs):**
```fsharp
/// Get API information and available routes.
let getInfo (client: HttpClient) (config: ApiConfig) : Async<ApiResult<InfoResponse>> =
```

**Better for fsdocs:**
```fsharp
/// <summary>Get API information and available routes.</summary>
/// <param name="client">The HttpClient instance to use.</param>
/// <param name="config">API configuration with base URL and auth key.</param>
/// <returns>Async ApiResult containing InfoResponse with game metadata and available routes.</returns>
let getInfo (client: HttpClient) (config: ApiConfig) : Async<ApiResult<InfoResponse>> =
```

**Verdict: RECOMMEND.** Low effort, improves fsdocs output. Non-breaking.

### Finding 9d: No module-level `<namespacedoc>` for fsdocs

fsdocs uses `<namespacedoc>` to generate namespace-level documentation. Adding this to Types.fs would improve the generated docs landing page:

```fsharp
/// <namespacedoc>
/// <summary>
/// F# client library for the Train Sim World 6 HTTP API. Provides type-safe access to
/// game data including node trees, endpoints, and real-time value polling.
/// </summary>
/// </namespacedoc>
```

**Verdict: NICE TO HAVE for when docs pipeline is built.

---

## 10. Collection Types

**Severity:** Low

### Finding 10a: `list` is the right default for F# return types ✅

`InfoResponse.HttpRoutes: HttpRoute list`, `Node.Nodes: Node list option`, `Endpoint list option` — all use immutable F# lists. Correct for:
- Small collections (API responses typically have <100 items)
- Pattern matching (list is the natural match target)
- Immutability (consumer can't accidentally mutate the response)

### Finding 10b: `string list` return from `parseNodePath` is correct

Path segments are small, ordered, and pattern-matched — `list` is ideal.

### Finding 10c: `GetResponse.Values: Dictionary<string, obj>` should be `Map`

**Current:**
```fsharp
type GetResponse =
    { Result: string
      Values: Dictionary<string, obj> }
```

`Dictionary<string, obj>` is mutable and from `System.Collections.Generic`. An F# `Map` would be more idiomatic:

```fsharp
type GetResponse =
    { Result: string
      Values: Map<string, JsonElement> }
```

However, `Map` doesn't deserialize as naturally from JSON as `Dictionary`. Since this type is exclusively deserialized from API responses, `Dictionary` with `JsonElement` values (see Finding 4c) is the pragmatic choice.

**Verdict: KEEP Dictionary, but change `obj` to `JsonElement`.** Combines with Finding 4c.

---

## Summary of Recommendations

### Do Now (non-breaking, low effort):
1. **HttpMethod.Patch** — replace `HttpMethod("PATCH")` with `HttpMethod.Patch` (Finding 6b)
2. **ApiError.describe** — add error formatting helper (Finding 3c)
3. **Async.CancellationToken** — thread implicit CT to `SendAsync` (Finding 5a)
4. **XML docs** — add `<param>` and `<returns>` to ApiClient functions (Finding 9c)

### Do in Phase 2 (breaking changes, batch together):
5. **ApiSession record** — bundle HttpClient + ApiConfig (Finding 2a)
6. **`Dictionary<string, obj>` → `Dictionary<string, JsonElement>`** (Finding 4c)
7. **Parameter order** — `findEndpoint endpointName node` (Finding 2b)

### Defer:
8. **`asyncResult {}`** — current try/catch pattern is actually cleaner for HTTP code (Finding 1a)
9. **`TimeoutError` case** — add when consumers need it (Finding 3b)
10. **`NodePath` wrapper** — large breaking change for marginal benefit (Finding 4b)
11. **Move `discoverCommKey`** — low priority module reorganization (Finding 8b)

---

## What's Already Good

The library is well-designed for an F# API client:
- **Typestate pattern** on BaseUrl/CommKey with private constructors — excellent
- **`[<AutoOpen>]` on Types** — right choice for domain types
- **`result {}` CE** in `createConfigWithUrl` — textbook usage
- **Immutable F# lists** for all collection return types
- **HttpClient injected, not owned** — correct lifecycle management
- **Comprehensive XML docs** with examples on the error type
- **Clean module layering** — Types → Http → ApiClient → higher modules
- **`ApiResult<'T>` type alias** — convenient without hiding the Result structure
