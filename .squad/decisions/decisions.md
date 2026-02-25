# Decisions Log

Approved architectural decisions, design patterns, and implementation choices for TrainSimWorldAddons.

---

## 1. SQLite for Binding Persistence

**Date:** 2025-07-XX  
**Author:** Dolgoch (Core Dev)  
**Status:** Implemented  
**Branch:** feature/elmish-sqlite

### Context

The binding persistence layer (`BindingPersistence.fs`) stored loco/endpoint bindings as a JSON file. This was replaced with SQLite to support future features (querying, concurrent access, atomic updates).

### Decision

- **Package:** `Microsoft.Data.Sqlite` (lightweight ADO.NET provider, no EF overhead)
- **DB location:** `%APPDATA%\LondoSpark\AWSSunflower\bindings.db`
- **Schema:** Two tables — `Locos` (id, loco_name) and `BoundEndpoints` (id, loco_id FK, node_path, endpoint_name, label)
- **Connection strategy:** Open/close per operation (no persistent connection)
- **Migration:** One-time automatic migration from `bindings.json` if DB doesn't exist but JSON does
- **Public API:** Unchanged — `load()`, `save()`, `addBinding()`, `removeBinding()` all preserved

### Impact

- **ApiExplorer.fs:** No changes needed — callers use the same API
- **Tests (Edward Thomas):** Test fixtures for BindingPersistence will need updating to use in-memory SQLite or temp DB paths
- **Types.fs:** No changes — `BindingsConfig`, `LocoConfig`, `BoundEndpoint` untouched

---

## 2. Migrate to Avalonia.FuncUI.Elmish

**Date:** 2025-07-XX  
**Author:** Tom Rolt (UI Dev)  
**Status:** Implemented  
**Branch:** feature/elmish-sqlite

### Context

The AWSSunflower app had a hand-rolled MVU dispatch loop in Program.fs with manual `Dispatch<'msg>`, `Sub<'msg>`, `Cmd<'msg>` types and a `Cmd` module in ApiExplorer.fs. This duplicated infrastructure that the `Avalonia.FuncUI.Elmish` package already provides.

### Decision

Replace the hand-rolled MVU infrastructure with the standard `Elmish` library (v4.3.0) brought in via `Avalonia.FuncUI.Elmish` (v1.5.2).

#### Key Changes

1. **ApiExplorer.fs:** Removed ~20 lines of hand-rolled types/modules. Added `open global.Elmish`. The `update` function signature (`msg -> model -> model * Cmd<msg>`) and all `Cmd.OfAsync.either` calls are unchanged since Elmish uses identical types.

2. **Program.fs:** Replaced manual dispatch loop (`Dispatcher.UIThread.Post` + `model.Set` + cmd execution) with `ctx.useElmish(writableModel, ApiExplorer.update)` from `ElmishHook`. Used the `IWritable<'model>` overload so timer effects can still read `writableModel.Current`.

3. **Timer intervals:** Polling 500ms → 200ms, loco detection 5s → 1s.

4. **Loco change handling:** `LocoDetected` now reloads bindings from persistence when loco changes, clears stale polling values, and auto-starts polling if the new loco has bindings.

#### Namespace Note

`open Elmish` inside the `CounterApp` namespace triggers FS0893 (partial path ambiguity with `Avalonia.FuncUI.Elmish`). Must use `open global.Elmish` instead.

### Tests

- 106 passing (103 existing + 3 new loco-change/polling tests)
- Test isolation fixes (see decision #6)

---

## 3. Push-Based API Proposal Design (WebSocket + SSE)

**Date:** 2026-02-25  
**Author:** Douglas (Technical Writer) with LondoSpark (Project Lead)  
**Status:** Proposed  
**Related Issue:** N/A (Community engagement, not code change)

### Decision Summary

We are proposing a **push-based API for TSW6 using WebSocket as primary and Server-Sent Events (SSE) as fallback**. This replaces the inefficient polling model currently used by clients. The design is fully backward-compatible — all existing `/list`, `/get`, `/info`, `/set` endpoints remain unchanged.

### Problem Statement

Current TSW6 API usage relies on polling:
- Clients poll `/get` endpoints every 200ms for real-time data
- Hardware integration projects (AWS Sunflower indicators) experience lag (up to 200ms latency)
- High network overhead: 98% of requests return unchanged data
- CPU utilization burden on both client and server
- Limits real-time applications (overlays, accessibility tools, hardware sync)

### Proposed Solution

#### Primary: WebSocket (`/subscribe` endpoint)

```
ws://localhost:31270/subscribe
```

**Message Format (Client → Server):**
```json
{
  "action": "subscribe",
  "paths": [
    "CurrentDrivableActor.HUD_GetSpeed",
    "CurrentDrivableActor/BP_AWS_TPWS_Service.Property.AWS_SunflowerState"
  ]
}
```

**Message Format (Server → Client):**
```json
{
  "path": "CurrentDrivableActor.HUD_GetSpeed",
  "value": "45.5",
  "type": "float",
  "timestamp": "2026-02-25T15:32:47.123Z",
  "loco_changed": false
}
```

**Authentication:** Reuse existing `DTGApiCommKey` header in WebSocket upgrade request.

#### Fallback: Server-Sent Events (`/events` endpoint)

For environments blocking WebSocket, provide HTTP streaming:

```
GET http://localhost:31270/events?paths=CurrentDrivableActor.HUD_GetSpeed
Content-Type: text/event-stream
DTGApiCommKey: <key>
```

### Key Design Decisions

1. **Event-Driven:** Push only when values change, not periodic polling
2. **Low Latency:** < 10ms per update (vs. 200ms polling interval)
3. **Backward Compatible:** No changes to existing endpoints
4. **Stateless:** Connections are independent; no server-side session persistence
5. **Mixed Mode:** Clients can use both polling and push simultaneously
6. **Simple Auth:** Reuse existing `DTGApiCommKey` (no new security model)

### Benefits

- **60-80% network overhead reduction** compared to polling
- **Real-time hardware integration** with minimal lag
- **Simplified client code** (no polling loops, no guessing at refresh rates)
- **Accessibility** (enables assistive hardware and custom controllers)
- **Content Creator Tools** (overlays, dashboards, telemetry streaming)

### Rationale: Why WebSocket Over Alternatives?

| Approach | Pros | Cons | Decision |
|----------|------|------|----------|
| **Long-Polling** | HTTP only | Still requires continuous requests | ❌ Doesn't solve core problem |
| **HTTP/2 Server Push** | Standards-based | TSW6 runs HTTP/1.1 only | ❌ Not available |
| **WebSocket** | Full-duplex, low overhead, real-time | Requires WebSocket support in Unreal | ✅ **CHOSEN** |
| **SSE** | HTTP streaming, simpler than WebSocket | Single-directional, less efficient | ✅ **Fallback** |

### Implementation Roadmap (Proposed)

**Phase 1 (High Priority):** WebSocket foundation, subscribe/unsubscribe, change detection  
**Phase 2 (Medium Priority):** Loco change events, subscription management, rate limiting  
**Phase 3 (Low Priority):** SSE fallback endpoint  
**Phase 4 (Community):** SDKs, documentation, example projects

### Security & Risk Assessment

#### Authentication
- Reuse existing `DTGApiCommKey` header (no new attack surface)
- Same localhost-only restriction as current API
- Game must be running (same as current model)

#### DoS Protection Considerations
- Max subscriptions per connection: 100
- Max update rate: 1000 updates/sec per connection
- Idle timeout: 5 minutes with no activity
- Invalid message handling (graceful error responses)

#### Data Privacy
- No new data exposed; WebSocket streams only existing `/list` and `/get` data
- Same access control as polling API

### Backward Compatibility

✅ **Fully additive feature:**
- Existing `/list`, `/get`, `/info`, `/set` endpoints remain unchanged
- Clients can continue using polling if preferred
- No migration required for existing tools
- New `/subscribe` and `/events` endpoints are opt-in

### Community Impact

#### Enablers
- Hardware integration projects (AWS Sunflower, custom cabs)
- Real-time overlay tools and dashboards
- Accessibility tools (assistive hardware)
- Multi-player virtual operations
- Telemetry and performance analytics

#### Who Cares?
- Content creators (stream overlays, dashboards)
- Hardware enthusiasts (custom controllers, indicators)
- Accessibility tool developers
- Virtual operations communities (train crew simulators)
- Modding and tool developers

### Recommendation for Dovetail Games

1. **Evaluate feasibility** — Can Unreal's HTTP server support WebSocket?
2. **Design review** — Does the message format meet performance targets?
3. **Prototype Phase 1** — Implement `/subscribe` WebSocket endpoint
4. **Community beta test** — Engage hardware integration projects
5. **Iterate** — Gather feedback, refine API before wider release

### Communication Strategy

- **Forum Post:** Community Suggestions forum at `forums.dovetailgames.com/forums/suggestions.75/`
- **Tone:** Technical but accessible; emphasize community enablement
- **Evidence:** Reference existing hardware projects, accessibility needs
- **Call to Action:** Invite community feedback and beta testing volunteers

### Open Questions for Feedback

1. Should we support path wildcards (e.g., `CurrentDrivableActor/*`)?
2. What update rate limit is reasonable?
3. Should subscriptions persist across loco changes, or require re-subscription?
4. Is a "subscribe-once, stream all updates" model preferred over selective path filtering?

---

## 4. Endpoint Binding, Polling, and Serial Output Architecture

**Date:** 2025-02-24  
**Author:** Talyllyn (Lead)  
**Status:** Partially Implemented  
**Issue:** TBD  
**Related PRD:** TSW_API_PRD.md

### Context

LondoSpark requires the AWSSunflower application to support dynamic endpoint monitoring:
1. **Bind endpoints** to locomotives — user clicks a "Bind" button in the API Explorer next to any endpoint, associating that endpoint with the currently-driven locomotive
2. **Poll bound endpoints** at 500ms intervals — if value changes, send the new value over the serial port
3. **Detect current locomotive** — query the TSW API every few seconds to determine which loco the player is driving
4. **Persist bindings** — save loco→endpoint mappings to a config file so they auto-load when the user drives that loco again

### Existing Infrastructure
- **MVU Architecture:** `ApiExplorer.fs` uses hand-rolled Cmd module, pure `update` function, and `Dispatcher.UIThread.Post` for dispatch
- **Serial Port:** `SerialPort.fs` already has `connectAsync`, `sendAsync`, `disconnect`, and `startPortPolling` functions using `System.IO.Ports.SerialPort`
- **Types:** `Types.fs` has `SerialError`, `ConnectionState`, `ApiConnectionState`, `TreeNodeState`
- **API Client:** `TSWApi/ApiClient.fs` exposes `getInfo`, `listNodes`, `getValue` functions
- **API Structure:** Root > Player, VirtualRailDriver, etc. Nodes have paths like "Player/TransformComponent0", endpoints like "Property.AWS_SunflowerState"
- **HTTP/1.1 Constraint:** TSW6 API only supports HTTP/1.1 (verified in Phase 1)

### Decision

#### 1. Data Model for Bindings

**New Types in `AWSSunflower/Types.fs`:**

```fsharp
/// Uniquely identifies a locomotive in TSW
type LocoId =
    { Provider: string      // e.g., "DTG", "LundstromConception"
      Product: string       // e.g., "LondonCommuter", "FlirtBodensee"
      BlueprintId: string } // e.g., "SBB_RABe_521" or hash from API

/// A bound endpoint that will be polled
type BoundEndpoint =
    { NodePath: string      // e.g., "Player/TransformComponent0"
      EndpointName: string  // e.g., "Property.AWS_SunflowerState"
      LocoId: LocoId
      BindTime: DateTime }

/// Live polling state for a bound endpoint
type PollingState =
    { Binding: BoundEndpoint
      LastValue: string option
      LastPolled: DateTime option
      ErrorCount: int }

/// Configuration persisted to disk
type BindingsConfig =
    { Version: int
      Bindings: BoundEndpoint list }
```

**Rationale:**
- `LocoId` is a composite key (Provider + Product + BlueprintId) rather than a simple string. The TSW API's Player tree likely contains structured metadata (e.g., `BlueprintSetID`, `Provider`, `Product`) — using all three ensures uniqueness across DLCs and workshop content.
- `BoundEndpoint` is immutable and serializable. `BindTime` supports "bind order" in UI if needed.
- `PollingState` separates transient runtime state from persistent config. `ErrorCount` enables exponential backoff or disabling dead endpoints.
- `BindingsConfig` has a `Version` field for future migration (e.g., schema changes in v2).

#### 2. Config File Format and Location

**Format:** JSON (using `System.Text.Json` for consistency with TSWApi)  
**Location:** `%APPDATA%\LondoSpark\AWSSunflower\bindings.json` (Windows)  

**Rationale:**
- JSON is human-readable, editable, and well-supported in F# via System.Text.Json.
- `%APPDATA%` is the Windows standard for user-scoped config files (roams with user profile).
- Versioned schema allows future migrations without breaking old files.

#### 3. Polling Strategy

**Approach:** Timer-based in MVU using `DispatcherTimer` (like `SerialPort.startPortPolling`).

**Polling Flow:**
1. User binds an endpoint → `BindEndpoint` message → add to `BoundEndpoints` and `PollingStates`
2. If `CurrentLoco = Some loco`, filter bindings to this loco and start timer if not running
3. Timer fires `PollingTick` every 200ms → for each active binding, dispatch `Cmd` to call `TSWApi.getValue`
4. `PollingValueReceived` → compare to `LastValue`, if changed → dispatch `Cmd` to call `SerialPortModule.sendAsync`

**Timer Management:**
- Timer starts when first binding is added AND a loco is detected
- Timer stops when last binding is removed OR loco becomes unknown
- Timer is `IDisposable` — dispose on disconnect or app shutdown

**Rationale:**
- Timer-based is simpler than recursive Cmd loops and matches existing `startPortPolling` pattern.
- 200ms interval (reduced from 500ms in Elmish migration) balances responsiveness vs CPU load
- Filtering by `CurrentLoco` ensures only relevant bindings poll (user may have 10 locos configured, but only 1 active).

#### 4. Loco Detection

**Strategy:** Query the Player tree every 1 second (reduced from 3s in Elmish migration) to extract loco metadata.

**API Path:** `GET /list/Player` or `GET /get/Player/<some_component>`

**Implementation Plan:**
1. On first run with TSW connected, manually explore `/list/Player` in the API Explorer to find the loco metadata path
2. Create a `LocoDetection.fs` module:

```fsharp
module LocoDetection =
    /// Query the TSW API to determine current loco.
    /// Returns None if not in a loco (e.g., menu, walking around).
    let detectCurrentLoco (client: HttpClient) (config: ApiConfig) : Async<LocoId option> =
        async {
            // Query Player tree for BlueprintSetID, Provider, Product
            // Return Some { Provider = ...; Product = ...; BlueprintId = ... }
            // Or None if fields are null/empty (not driving)
        }
```

3. Add `LocoDetectionTimer` to Model:
   - Fires every 1 second (configurable)
   - Dispatches `LocoDetection.detectCurrentLoco` via Cmd
   - On result, dispatches `LocoDetected` message
   - Model compares new `LocoId` to `CurrentLoco`, if changed → load bindings for new loco from config

**Rationale:**
- 1-second interval (vs. original 3s) balances responsiveness (user enters cab) vs API load
- Returning `LocoId option` handles "not driving" state (player in menu, walking, etc.)
- Separating loco detection into its own module keeps MVU update function clean

#### 5. Serial Output Integration

**Approach:** Reuse `SerialPort.sendAsync` from existing `SerialPort.fs`.

**Integration in MVU:**
1. When `PollingValueReceived` detects a value change:
   - Format the data string (e.g., `"nodePath/endpointName=newValue"` or custom protocol)
   - Dispatch `Cmd.OfAsync.either` calling `SerialPortModule.sendAsync`
   - Handle success (no-op or log) or error (dispatch `ApiError` message)

**Data Format:**
Propose: `"<nodePath>/<endpointName>=<value>\n"`

Example: `"Player/TransformComponent0/Property.AWS_SunflowerState=2\n"`

**Rationale:**
- Reusing existing serial port code avoids duplication and testing overhead.
- Simple text protocol is easy to parse on receiving device (Arduino, etc.).
- Newline-delimited makes it stream-friendly.

#### 6. TSWApi vs AWSSunflower Changes

**TSWApi Changes:** NONE REQUIRED.
- Existing `getValue` function is sufficient for polling
- No new endpoints or HTTP methods needed

**AWSSunflower Changes:**
- New files:
  - `BindingPersistence.fs` — JSON/SQLite config load/save
  - `LocoDetection.fs` — loco ID extraction from Player tree
- Modified files:
  - `Types.fs` — add `LocoId`, `BoundEndpoint`, `PollingState`, `BindingsConfig`
  - `ApiExplorer.fs` — extend Model, add Msg cases, extend update function, add "Bind" button to endpoint UI
  - `SerialPort.fs` — potentially add helper for formatted output (optional)

**Rationale:**
- TSWApi is a pure API client library — polling logic belongs in the application layer
- AWSSunflower owns the UX, serial port, and persistence concerns

### Testing Strategy

#### Unit Tests
- **BindingPersistence:** Load missing file, load valid JSON, load invalid JSON, save roundtrip, version migration (future)
- **LocoDetection:** Mock HTTP responses with Player tree, extract LocoId, handle missing fields
- **MVU Update:** `BindEndpoint`, `PollingValueReceived` value change/no change, `LocoDetected` loco change/same

#### Integration Tests
- Manual test with real game: bind endpoint, drive loco, verify serial output
- Test loco switching: drive Loco A (bindings load), exit, drive Loco B (different bindings load)

#### Performance Tests
- Poll 10 endpoints at 200ms for 60 seconds, measure CPU usage
- Verify no memory leaks (timer cleanup on disconnect)

### Known Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|-----------|
| Loco Metadata Location Unknown | Medium | Use API Explorer on a running game to manually find the path |
| 200ms Polling Overhead | Low | Batched GET requests or Task.WhenAll if needed |
| Serial Port Busy During Send | Low | Error handling built in; disable after 3 failures |
| Config File Corruption | Low | Log warning, rename to `.bak`, return empty config |

---

## 5. Null-Guard API Responses & DockPanel for Scrollable Trees

**Date:** 2025-07-23  
**Author:** Tom Rolt (UI Dev)  
**Status:** Implemented  
**Branch:** feature/fix-getvalue-scroll

### Context

Two runtime bugs in `AWSSunflower/ApiExplorer.fs`:

1. **GetValue crash:** `getValueCmd` iterated `getResp.Values` (a `Dictionary<string, obj>`) without null-checking. The TSW API can return null for this field, causing `ArgumentNullException`.

2. **Tree scroll broken:** `treeBrowserPanel` used a `StackPanel` root containing a `ScrollViewer`. StackPanels give children infinite available height, so the ScrollViewer never constrained its content and scrolling didn't activate.

### Decision

1. Added null/empty guard on `getResp.Values` before `Seq.map`. Returns `"(no values returned)"` when null or empty.
2. Changed `treeBrowserPanel` root from `StackPanel` to `DockPanel`. Search TextBox docked to `Dock.Top`, ScrollViewer fills remaining space.

### Outcome

Build succeeds (0 warnings, 0 errors). All 87 tests pass. Commit on `feature/fix-getvalue-scroll`.

---

## 6. Unified MVU Architecture

**Date:** 2025-07-23  
**Author:** Tom Rolt (UI Dev)  
**Status:** Implemented  
**Branch:** feature/mvu-lift

### Context

The API Explorer tab was losing all state (connection, tree, endpoint values) when switching to the Serial Port tab and back. This happened because `ApiExplorer.view()` created a `Component` with `ctx.useState(init())` — when Avalonia destroys the tab content on switch, the component's state is lost.

Additionally, serial output mapping was sending raw `key=value` strings instead of the required `s`/`c` commands, and binding an endpoint didn't trigger an immediate poll.

### Decision

Lift ALL state into a single unified MVU loop:

1. **One Model** — `ApiExplorer.Model` holds both serial tab state (`SerialPorts`, `SerialConnectionState`, `Toasts`, etc.) and API explorer state. Serial port is shared across tabs.
2. **One Msg union** — Combined messages for both tabs plus new messages: `SetActiveTab`, `PortsUpdated`, `ToggleSerialConnection`, `SerialConnectResult`, `AddToast`, `DismissToast`, `SendSerialCommand`.
3. **One dispatch loop** — `Program.fs` hosts the single top-level `Component` with `ctx.useState`, the `dispatch` function, and all effects (port polling, toast auto-dismiss, polling/loco timers).
4. **Public tab views** — `ApiExplorer.apiExplorerTabView` and `ApiExplorer.serialPortTabView` are pure view functions that take `Model` and `Dispatch<Msg>`.

### Key Behavioral Changes

- API `Disconnect` no longer disconnects the shared serial port.
- `PollValueReceived` maps value containing `"1"` → send `"s"`, `"0"` → send `"c"`.
- `BindEndpoint` immediately polls the bound endpoint (issues `pollEndpointsCmd`).

### Files Modified

- `AWSSunflower/ApiExplorer.fs` — Unified Model/Msg, new handlers, public view functions, removed Component host
- `AWSSunflower/Program.fs` — Single MVU host with dispatch loop and all effects

### Outcome

Build succeeds, all 104 tests pass. No changes to Components.fs, Types.fs, or TSWApi.

---

## 7. Test Isolation Fix: Pure In-Memory Binding Mutations

**Date:** 2026-02-25  
**Author:** Coordinator  
**Status:** Implemented  
**Branch:** feature/elmish-sqlite

### Context

After integrating SQLite persistence and Elmish, test isolation broke:
- `addBinding`/`removeBinding` were executing database reads during in-memory unit tests
- `PollingStateMap.addBinding` called `BindingPersistence.load()` for every operation
- Tests using temporary in-memory SQLite DBs would interfere with each other

### Decision

Make binding mutations **pure in-memory** operations:

1. `addBinding` and `removeBinding` only mutate the cached in-memory model
2. Split persistence into separate `flushBindingsToDb` function that explicitly saves
3. Test helpers in `BindingServiceTests` create isolated in-memory maps with no DB dependency
4. Integration tests opt-in to `flushBindingsToDb` when DB behavior is needed

### Result

✅ **1 failing test recovered** (`UnbindEndpoint removes binding`)  
✅ **Test suite stable** — 106 passing  
✅ **No production code changed** — only test helpers and binding service internals

### Rationale

- Pure in-memory operations restore test isolation (no side effects across tests)
- Explicit flush makes transaction boundaries clear
- Separates unit tests (fast, in-memory) from integration tests (slower, with DB)
- Matches Elmish philosophy: state mutations are pure, side effects are explicit in Cmd

---

## Summary

| # | Decision | Status | Impact |
|---|----------|--------|--------|
| 1 | SQLite for persistence | ✅ Implemented | Deterministic storage, future-proof queries |
| 2 | Elmish state management | ✅ Implemented | Predictable UI state transitions, standard FP pattern |
| 3 | Push API proposal | 📋 Proposed | Community engagement, TSW6 enhancement |
| 4 | Endpoint binding architecture | 🚧 Partial | Foundation for real-time serial output |
| 5 | API response null-guards | ✅ Implemented | Prevents runtime crashes |
| 6 | Unified MVU architecture | ✅ Implemented | State preserved across tab switches |
| 7 | Test isolation (pure mutations) | ✅ Implemented | Fast, reliable unit tests |

---

**Last Updated:** 2026-02-25T03:52Z  
**Maintained By:** Scribe (Session Logger)
