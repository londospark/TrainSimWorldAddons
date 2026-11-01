# F# Idiomaticity Audit — Full Codebase

**Date:** 2026-03-01  
**Author:** Talyllyn (Lead)  
**Scope:** TSWApi (library, 5 files), AWSSunflower (application, 14 files), TSWApi.Tests (8 files), AWSSunflower.Tests (5 files) — 32 .fs files total  
**Status:** Report only — no changes made

---

## Executive Summary

The codebase is **generally well-written F#** — pipeline style, discriminated unions, immutable records, and `[<RequireQualifiedAccess>]` are used thoughtfully. The TSWApi library in particular shows strong idiomatic patterns. The main findings are:

1. **`sprintf` proliferation** — ~25 instances that should use `$"..."` string interpolation (trivial effort, high consistency win)
2. **`CounterApp` namespace** — the AWSSunflower project uses a leftover template namespace (medium effort, significant clarity win)
3. **Missing `[<RequireQualifiedAccess>]`** on `SerialError` and `DetectionResult` DUs
4. **`HttpMethod.Patch`** exists as a static property since .NET 5 — no need for `HttpMethod("PATCH")`
5. **Test duplication** — Boolean interpreter tests repeat across two test files, and `[<Theory>]`/`[<InlineData>]` would reduce boilerplate

---

## 🔴 Should Fix

### SF-1: `CounterApp` namespace across entire AWSSunflower project
**Files:** All 14 AWSSunflower/*.fs files  
**Current:** `namespace CounterApp` / `module CounterApp.X`  
**Idiomatic:** `namespace AWSSunflower` / `module AWSSunflower.X`  
**Impact:** HIGH — Confusing for any new reader. The project is called AWSSunflower, not CounterApp.  
**Effort:** Medium — Rename namespace in all 14 app files + 5 test files + update .fsproj InternalsVisibleTo if needed. Mechanical but touches every file.

### SF-2: `sprintf` used where `$"..."` interpolation is clearer
**Files and lines:**
| File | Line(s) | Current | Replacement |
|------|---------|---------|-------------|
| ApiClient.fs | 16 | `sprintf "/list/%s"` | `$"/list/{v}"` |
| Helpers.fs | 34 | `sprintf "%s.%s" nodePath endpointName` | `$"{nodePath}.{endpointName}"` |
| Commands.fs | 34 | `sprintf "API error: %A"` | `$"API error: %A{err}"` |
| Commands.fs | 47 | `sprintf "CommKey discovery failed: %A"` | `$"CommKey discovery failed: %A{err}"` |
| Commands.fs | 51 | `sprintf "Invalid configuration: %A"` | `$"Invalid configuration: %A{err}"` |
| Commands.fs | 100 | `sprintf "%s: %O" kvp.Key kvp.Value` | `$"{kvp.Key}: %O{kvp.Value}"` |
| Commands.fs | 113 | `sprintf "Detect loco failed: %A"` | `$"Detect loco failed: %A{err}"` |
| TreeBrowser.fs | 21 | `sprintf "%s %s" arrow node.Name` | `$"{arrow} {node.Name}"` |
| StatusBar.fs | 28 | `sprintf "Status: Connected to %s (Build %d)"` | `$"Status: Connected to {info.Meta.GameName} (Build {info.Meta.GameBuildNumber})"` |
| StatusBar.fs | 29 | `sprintf "Status: Error - %s"` | `$"Status: Error - {msg}"` |
| StatusBar.fs | 43 | `sprintf "Last response: %.0fms"` | `$"Last response: %.0f{time.TotalMilliseconds}ms"` |
| StatusBar.fs | 51 | `sprintf "Loco: %s"` | `$"Loco: {loco}"` |
| SerialPortPanel.fs | 38 | `sprintf "%s in use"` | `$"{p} in use"` |
| SerialPortPanel.fs | 39 | `sprintf "%s missing"` | `$"{p} missing"` |
| EndpointViewer.fs | 93,98,114 | `sprintf "Node: %s"`, `sprintf "Path: %s"` | `$"Node: {name}"`, `$"Path: {path}"` |
| BindingsPanel.fs | 17 | `sprintf "%s = %s"` | `$"{b.Label} = {value}"` |
| BindingsPanel.fs | 59 | `sprintf "Active Bindings (%d)"` | `$"Active Bindings ({currentBindings.Length})"` |
| PortDetection.fs | 152 | `sprintf "%s — %s"` | `$"{port.PortName} — {usb.Description}"` |
| TestHelpers.fs | 48 | `sprintf """..."""` | `$"""..."""` |

**Impact:** HIGH — Inconsistent string formatting across the codebase. Some functions already use `$"..."` (Http.fs:52,53,107; Types.fs:53), creating a mixed style.  
**Effort:** Trivial — Mechanical find-and-replace. Each one is 5 seconds.

### SF-3: `HttpMethod("PATCH")` instead of `HttpMethod.Patch`
**Files:** TSWApi/Http.fs:129, TSWApi.Tests/HttpTests.fs:247,253  
**Current:** `HttpMethod("PATCH")`  
**Idiomatic:** `HttpMethod.Patch` (available since .NET 5.0, project targets .NET 10)  
**Impact:** HIGH — Suggests the code was written against an older .NET, which it wasn't.  
**Effort:** Trivial — 3 line changes.

### SF-4: Missing `[<RequireQualifiedAccess>]` on `SerialError`
**File:** AWSSunflower/Types.fs:7  
**Current:** `type SerialError = | PortInUse of ... | PortNotFound of ... | Disconnected`  
**Idiomatic:** Add `[<RequireQualifiedAccess>]`. The `Disconnected` case collides with `ConnectionState.Disconnected` in the same namespace.  
**Impact:** HIGH — Name collision risk. Already mitigated by convention but not enforced.  
**Effort:** Small — Add attribute + qualify ~8 call sites in Update.fs and SerialPortPanel.fs.

---

## 🟡 Worth Fixing

### WF-1: `List.length > 0` instead of `not List.isEmpty`
**Files and lines:**
- Helpers.fs:27 — `nodes.Length > 0` → `not nodes.IsEmpty`
- Helpers.fs:66 — `filteredChildren.Length > 0` → `not filteredChildren.IsEmpty`
- Helpers.fs:69 — `filteredChildren.Length > 0` → `not filteredChildren.IsEmpty`
- EndpointViewer.fs:90 — `endpoints.Length > 0` → `not endpoints.IsEmpty`

**Impact:** Medium — `.Length > 0` is O(n) on F# lists; `.IsEmpty` is O(1).  
**Effort:** Trivial — 4 line changes.

### WF-2: `currentSubscription` and `httpClient` should be `internal`
**File:** AWSSunflower/ApplicationScreen/Commands.fs:17-18  
**Current:** `let httpClient = new HttpClient()` and `let currentSubscription : ISubscription option ref = ref None` — both public.  
**Idiomatic:** Mark as `internal` since they're implementation details accessed only within the assembly.  
**Impact:** Medium — Exposes mutable infrastructure to consumers of the module.  
**Effort:** Trivial — Add `internal` keyword.

### WF-3: `BindingsPanel` reads `currentSubscription.Value` directly (MVU violation)
**File:** AWSSunflower/ApplicationScreen/BindingsPanel.fs:65-66,71-72  
**Current:** View reads mutable ref cell `currentSubscription.Value` to determine "Live" vs "Idle" status.  
**Idiomatic:** Add `IsSubscriptionActive: bool` to Model, set it in update function.  
**Impact:** Medium — Violates MVU unidirectional data flow. View should only read from Model.  
**Effort:** Small — Add field to Model, set in relevant message handlers, read in view.  
**Note:** Already identified in decomposition analysis. Flagging again for tracking.

### WF-4: Test duplication — Boolean interpreter tests exist in two test files
**Files:** TSWApi.Tests/CommandMappingTests.fs and AWSSunflower.Tests/CommandMappingTests.fs  
**Current:** Both files contain near-identical tests for `interpret ValueInterpreter.Boolean`.  
**Idiomatic:** One canonical test suite per function. The AWSSunflower.Tests version is the more complete one.  
**Impact:** Medium — Duplicate maintenance burden.  
**Effort:** Small — Remove duplicates from one file.

### WF-5: Boolean interpreter tests should use `[<Theory>]` / `[<InlineData>]`
**File:** Both CommandMappingTests.fs files  
**Current:** 8+ individual `[<Fact>]` tests for Boolean interpreter (`"1"` → Activate, `"0"` → Deactivate, `"True"` → Activate, etc.)  
**Idiomatic:**
```fsharp
[<Theory>]
[<InlineData("1", true)>]
[<InlineData("True", true)>]
[<InlineData("0", false)>]
[<InlineData("False", false)>]
let ``interpret Boolean maps value correctly`` (input: string, isActivate: bool) = ...
```
**Impact:** Medium — Reduces 8 tests to 1 parameterized test.  
**Effort:** Small.

### WF-6: `discoverCommKey` could use `result {}` CE more
**File:** TSWApi/Http.fs:24-54  
**Current:** Nested if/else with manual `Error(...)` returns inside try/with.  
**Idiomatic:** Could partially use `result {}` CE for the inner validation chain. The outer try/with must stay since it catches I/O exceptions.  
**Impact:** Medium — Readability improvement for a 30-line function.  
**Effort:** Small.

### WF-7: Missing `[<RequireQualifiedAccess>]` on `DetectionResult`
**File:** AWSSunflower/PortDetection.fs:43  
**Current:** `type DetectionResult = | SingleArduino of ... | NoPorts`  
**Idiomatic:** Add `[<RequireQualifiedAccess>]`. Cases like `NoPorts` are generic enough to collide.  
**Impact:** Medium — Currently safe because all call sites use `PortDetection.NoPorts` via module qualification, but not enforced.  
**Effort:** Trivial — Add attribute. Call sites already use qualified names.

### WF-8: `SendSerialCommand` handler fires async without error handling
**File:** AWSSunflower/ApplicationScreen/Update.fs:216-222  
**Current:**
```fsharp
async {
    let! _ = SerialPortModule.sendAsync port cmd
    ()
} |> Async.Start
```
**Idiomatic:** Use `Async.StartImmediate` (if on UI thread) or handle the Result. Currently discards the `Result<unit, SerialError>` return value silently.  
**Impact:** Medium — Serial send errors are swallowed. At minimum, use `do! ... |> Async.Ignore` or log the error.  
**Effort:** Small.

### WF-9: `okOrFail` pattern converts Results to exceptions
**File:** AWSSunflower/ApplicationScreen/Commands.fs:22-25  
**Current:** `okOrFail` wraps `Result.Error` as `failwith`, relying on `Cmd.OfAsync.either`'s exception handler.  
**Idiomatic:** This is a known Elmish limitation — `Cmd.OfAsync.either` doesn't natively support `Result`. Consider `Cmd.OfAsync.perform` with explicit `Result` handling in the success path, or a custom `Cmd.ofAsyncResult` helper.  
**Impact:** Medium — Stack traces from `failwith` are misleading (they look like bugs, not expected errors).  
**Effort:** Medium — Requires a small helper function and updating all command functions.

---

## 🟢 Nice to Have

### NH-1: Old-style array indexing `.Groups.[1]` vs `.Groups[1]`
**Files:** TSWApi/Http.fs:41,43; PortDetection.fs:59; plus ~15 instances in tests using `.[0]`, `.[1]`  
**Current:** `result.Nodes.Value.[0]`, `m.Groups.[1].Value`  
**Idiomatic:** Modern F# supports `result.Nodes.Value[0]`, `m.Groups[1].Value` (no dot before bracket).  
**Impact:** Low — Both syntaxes work; the new one is preferred in modern F#.  
**Effort:** Trivial — Mechanical replacement.

### NH-2: `isSerialConnected` could use `function` keyword
**File:** AWSSunflower/ApplicationScreen/Helpers.fs:42-43  
**Current:** `let isSerialConnected (model: ...) = match model.SerialConnectionState with ConnectionState.Connected _ -> true | _ -> false`  
**Idiomatic:** Could extract as active pattern `(|IsSerialConnected|_|)` if used in pattern matches, or leave as-is since it's only used as a boolean predicate.  
**Impact:** Low — Single use site, current form is readable.  
**Effort:** Trivial.

### NH-3: `AppColors` module could use a DU or typed colors
**File:** AWSSunflower/ApplicationScreen/Helpers.fs:77-82  
**Current:** String constants like `let connected = "#00AA00"`.  
**Idiomatic:** These are fine as string constants for Avalonia color parsing. A more type-safe approach would use `Avalonia.Media.Color` values directly, but the string approach integrates cleanly with the FuncUI DSL.  
**Impact:** Low — Works fine, minor type-safety improvement possible.  
**Effort:** Small.

### NH-4: No property-based testing with FsCheck
**Files:** All test files  
**Current:** Only example-based tests.  
**Idiomatic:** `interpret` (CommandMapping) and tree navigation functions are good candidates for property-based tests. E.g., "for any valid float string, `interpret Continuous` returns `Some (SetValue _)`".  
**Impact:** Low — Current test coverage is good with 200+ tests.  
**Effort:** Medium — Add FsCheck dependency, write property tests.

### NH-5: No cancellation token support in TSWApi library
**File:** TSWApi/Http.fs  
**Current:** `sendRequestWithMethod` doesn't accept or propagate `CancellationToken`.  
**Idiomatic:** Modern .NET async APIs should accept optional `CancellationToken` for cooperative cancellation.  
**Impact:** Low — The app doesn't need cancellation yet. Important for Phase 2 long-running operations.  
**Effort:** Small — Add optional parameter, pass to `SendAsync`.

### NH-6: `GetResponse.Values` uses `Dictionary<string, obj>`
**File:** TSWApi/Types.fs:156  
**Current:** `Values: Dictionary<string, obj>` — forces consumers to cast/unbox.  
**Idiomatic:** `Dictionary<string, System.Text.Json.JsonElement>` preserves type info without forcing CLR types.  
**Impact:** Low — Already noted in Phase 1 review. Breaking change, defer to Phase 2.  
**Effort:** Medium — Breaking API change.

### NH-7: `BindingPersistence.readAllFromDb` uses mutable collections locally
**File:** AWSSunflower/BindingPersistence.fs:105-119  
**Current:** `Dictionary<string, ResizeArray<BoundEndpoint>>` and `ResizeArray<string>` for building result.  
**Idiomatic:** Could use a fold over reader results. However, the current code reads from a `DbDataReader` imperatively, and mutable locals are pragmatic here.  
**Impact:** Low — Localized to one function, not exposed.  
**Effort:** Medium — Rewriting reader iteration as functional fold adds complexity.

### NH-8: `eprintfn` format strings vs interpolation
**Files:** BindingPersistence.fs:80,128,158; Program.fs:55,60,73  
**Current:** `eprintfn "[Persistence] JSON migration failed: %s" ex.Message`  
**Idiomatic:** `eprintfn` with `%s`/`%A` format specifiers IS idiomatic F# — it provides type-safe printf-style formatting. No change needed. Both `eprintfn $"..."` and `eprintfn "%s" x` are valid; the printf-style is traditional.  
**Impact:** Low — Consistency choice, not a defect.  
**Effort:** N/A.

### NH-9: `Endpoint` record has bare `Name: string` which can be CLR null
**File:** TSWApi/Types.fs:117  
**Current:** `Name: string` — JSON deserialization can produce null.  
**Idiomatic:** Could use `Name: string option` with `[<JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)>]`, but this would be a breaking API change and System.Text.Json handles null strings by default.  
**Impact:** Low — Defensive `nullSafe` calls in app code already handle this.  
**Effort:** Medium — Breaking change to library API.

---

## Already Idiomatic ✅

These areas were reviewed and found to be **well-done**:

1. **`[<AutoOpen>]` on `TSWApi.Types`** — Deliberate ergonomic choice for a small, focused library. DU cases like `NetworkError`, `AuthError` available without qualification.
2. **`[<RequireQualifiedAccess>]` on `ConnectionState`, `ApiConnectionState`, `Action`, `ValueInterpreter`, `SerialCommand`** — All appropriate.
3. **`BaseUrl` and `CommKey` single-case DUs with private constructors** — Excellent DDD/typestate pattern.
4. **`result {}` CE usage in `createConfigWithUrl`** — Clean FsToolkit integration.
5. **Pipeline style** — Consistent `|>` usage throughout. `|> Option.map ... |> Option.bind ... |> Option.defaultValue` chains are clean.
6. **Record `with` copy-and-update** — Used correctly in all Model updates.
7. **`function` keyword** — Used well in `toWireString`, `ToCommand` lambdas.
8. **Module organization** — Clean dependency chains, no cycles. TSWApi layers (Types → Http → ApiClient → TreeNavigation → Subscription). AWSSunflower layers (Types → Helpers → Commands → Update → Views).
9. **Test names** — Double-backtick style (`let ``test name here`` () =`) is idiomatic xUnit F#.
10. **`ApiResult<'T>` type abbreviation** — Good use of type alias for `Result<'T, ApiError>`.
11. **XML doc comments** — Thorough on all TSWApi public API. App code is appropriately less documented.
12. **Async patterns** — `async {}` is correct for Elmish/FuncUI integration. `task {}` in test mock handler is correct for .NET overrides.
13. **Error handling** — `ApiError` DU covers all error categories. `result {}` CE used where beneficial.

---

## Priority Order for Implementation

1. **SF-2** (sprintf → interpolation) — Trivial effort, immediate consistency win
2. **SF-3** (HttpMethod.Patch) — 3 line changes, removes false impression of old .NET
3. **SF-4** (RequireQualifiedAccess on SerialError) — Small effort, prevents future collisions
4. **WF-1** (List.IsEmpty) — Trivial, O(1) vs O(n) correctness
5. **WF-2** (internal on shared state) — Trivial, better encapsulation
6. **WF-7** (RequireQualifiedAccess on DetectionResult) — Trivial
7. **SF-1** (CounterApp → AWSSunflower namespace) — Medium effort, do as dedicated PR
8. **WF-3** (MVU subscription fix) — Small effort, architectural correctness
9. **WF-4/WF-5** (Test cleanup) — Small effort, maintenance win
10. **WF-8/WF-9** (Error handling patterns) — Medium effort, correctness win
