# ADR: Endpoint Binding, Polling, and Serial Output Architecture

**Date:** 2025-02-24  
**Author:** Talyllyn (Lead)  
**Status:** Proposed  
**Issue:** TBD  
**Related PRD:** TSW_API_PRD.md  

## Context

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

## Decision

### 1. Data Model for Bindings

#### New Types in `AWSSunflower/Types.fs`:

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

### 2. Config File Format and Location

**Format:** JSON (using `System.Text.Json` for consistency with TSWApi)  
**Location:** `%APPDATA%\LondoSpark\AWSSunflower\bindings.json` (Windows)  
**Example:**

```json
{
  "Version": 1,
  "Bindings": [
    {
      "NodePath": "Player/TransformComponent0",
      "EndpointName": "Property.AWS_SunflowerState",
      "LocoId": {
        "Provider": "DTG",
        "Product": "LondonCommuter",
        "BlueprintId": "SBB_RABe_521"
      },
      "BindTime": "2025-02-24T14:23:00Z"
    }
  ]
}
```

**Rationale:**
- JSON is human-readable, editable, and well-supported in F# via System.Text.Json.
- `%APPDATA%` is the Windows standard for user-scoped config files (roams with user profile).
- Versioned schema allows future migrations without breaking old files.

**New Module:** `AWSSunflower/BindingPersistence.fs`

```fsharp
module BindingPersistence =
    let private configDir () =
        let appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
        Path.Combine(appData, "LondoSpark", "AWSSunflower")

    let private configPath () = Path.Combine(configDir (), "bindings.json")

    let load () : Result<BindingsConfig, string> =
        // read JSON, deserialize, handle missing file
        
    let save (config: BindingsConfig) : Result<unit, string> =
        // ensure directory exists, serialize, write
```

### 3. Polling Strategy

**Approach:** Timer-based in MVU using `DispatcherTimer` (like `SerialPort.startPortPolling`).

**Implementation:**

```fsharp
// New message types
type Msg =
    | ... // existing messages
    | BindEndpoint of nodePath: string * endpointName: string
    | UnbindEndpoint of nodePath: string * endpointName: string
    | StartPolling
    | StopPolling
    | PollingTick
    | PollingValueReceived of binding: BoundEndpoint * value: string
    | LocoDetected of LocoId option

// Model extension
type Model =
    { ... // existing fields
      CurrentLoco: LocoId option
      BoundEndpoints: BoundEndpoint list
      PollingStates: Map<(string * string), PollingState>  // key: (nodePath, endpointName)
      PollingTimer: IDisposable option
      LocoDetectionTimer: IDisposable option }
```

**Polling Flow:**
1. User binds an endpoint → `BindEndpoint` message → add to `BoundEndpoints` and `PollingStates`
2. If `CurrentLoco = Some loco`, filter bindings to this loco and start timer if not running
3. Timer fires `PollingTick` every 500ms → for each active binding, dispatch `Cmd` to call `TSWApi.getValue`
4. `PollingValueReceived` → compare to `LastValue`, if changed → dispatch `Cmd` to call `SerialPortModule.sendAsync`

**Timer Management:**
- Timer starts when first binding is added AND a loco is detected
- Timer stops when last binding is removed OR loco becomes unknown
- Timer is `IDisposable` — dispose on disconnect or app shutdown

**Rationale:**
- Timer-based is simpler than recursive Cmd loops and matches existing `startPortPolling` pattern.
- 500ms interval is aggressive but acceptable for a desktop app with few endpoints. If CPU usage is high, make interval configurable.
- Filtering by `CurrentLoco` ensures only relevant bindings poll (user may have 10 locos configured, but only 1 active).

### 4. Loco Detection

**Strategy:** Query the Player tree every 3 seconds to extract loco metadata.

**API Path:** `GET /list/Player` or `GET /get/Player/<some_component>`

**Expected Structure (hypothesis):**
The Player tree likely contains a component with loco metadata. Common patterns in game APIs:
- `Player/VehicleComponent/BlueprintSetID`
- `Player/VehicleComponent/Provider`
- `Player/VehicleComponent/Product`
- Or a single endpoint like `Player/Property.CurrentVehicleID`

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
   - Fires every 3 seconds (configurable)
   - Dispatches `LocoDetection.detectCurrentLoco` via Cmd
   - On result, dispatches `LocoDetected` message
   - Model compares new `LocoId` to `CurrentLoco`, if changed → load bindings for new loco from config

**Rationale:**
- 3-second interval balances responsiveness (user enters cab) vs API load
- Returning `LocoId option` handles "not driving" state (player in menu, walking, etc.)
- Separating loco detection into its own module keeps MVU update function clean

### 5. Serial Output Integration

**Approach:** Reuse `SerialPort.sendAsync` from existing `SerialPort.fs`.

**Changes to Serial Module:**
- None required. `sendAsync` already takes `port: SerialPort` and `data: string` and returns `Async<Result<unit, SerialError>>`.

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

### 6. MVU Model/Message Changes

**New Messages:**

```fsharp
type Msg =
    // ... existing messages
    | BindEndpoint of nodePath: string * endpointName: string
    | UnbindEndpoint of nodePath: string * endpointName: string
    | LoadBindings of Result<BindingsConfig, string>
    | SaveBindings
    | StartPolling
    | StopPolling
    | PollingTick
    | PollingValueReceived of binding: BoundEndpoint * value: string
    | LocoDetected of LocoId option
    | SerialSendSuccess of endpointPath: string
    | SerialSendError of endpointPath: string * SerialError
```

**Model Extensions:**

```fsharp
type Model =
    { BaseUrl: string
      CommKey: string
      ApiConfig: ApiConfig option
      ConnectionState: ApiConnectionState
      IsConnecting: bool
      TreeRoot: TreeNodeState list
      SelectedNode: TreeNodeState option
      EndpointValues: Map<string, string>
      LastResponseTime: TimeSpan option
      SearchQuery: string
      // NEW FIELDS:
      CurrentLoco: LocoId option
      BoundEndpoints: BoundEndpoint list
      PollingStates: Map<(string * string), PollingState>
      PollingTimer: IDisposable option
      LocoDetectionTimer: IDisposable option
      SerialPort: System.IO.Ports.SerialPort option  // if not already in model
      SerialConnectionState: ConnectionState }        // if not already in model
```

**Update Function Changes:**

```fsharp
| BindEndpoint (nodePath, endpointName) ->
    match model.CurrentLoco with
    | None -> model, Cmd.none  // can't bind without a known loco
    | Some loco ->
        let binding = { NodePath = nodePath; EndpointName = endpointName; LocoId = loco; BindTime = DateTime.UtcNow }
        let newBindings = binding :: model.BoundEndpoints
        let newPollingStates = Map.add (nodePath, endpointName) { Binding = binding; LastValue = None; LastPolled = None; ErrorCount = 0 } model.PollingStates
        { model with BoundEndpoints = newBindings; PollingStates = newPollingStates },
        Cmd.batch [ saveBindingsCmd newBindings; startPollingIfNeeded model ]

| PollingTick ->
    let activeBindings = model.BoundEndpoints |> List.filter (fun b -> Some b.LocoId = model.CurrentLoco)
    let pollCmds = activeBindings |> List.map (fun b -> pollEndpointCmd model.ApiConfig b)
    model, Cmd.batch pollCmds

| PollingValueReceived (binding, newValue) ->
    let key = (binding.NodePath, binding.EndpointName)
    match Map.tryFind key model.PollingStates with
    | Some state when state.LastValue <> Some newValue ->
        let updatedState = { state with LastValue = Some newValue; LastPolled = Some DateTime.UtcNow }
        { model with PollingStates = Map.add key updatedState model.PollingStates },
        sendSerialCmd model.SerialPort binding newValue
    | Some state ->
        let updatedState = { state with LastPolled = Some DateTime.UtcNow }
        { model with PollingStates = Map.add key updatedState model.PollingStates }, Cmd.none
    | None -> model, Cmd.none

| LocoDetected newLoco ->
    if newLoco <> model.CurrentLoco then
        // Loco changed or entered/exited loco
        { model with CurrentLoco = newLoco },
        match newLoco with
        | Some loco -> loadBindingsForLocoCmd loco
        | None -> stopPollingCmd
    else
        model, Cmd.none
```

### 7. TSWApi vs AWSSunflower Changes

**TSWApi Changes:** NONE REQUIRED.
- Existing `getValue` function is sufficient for polling
- No new endpoints or HTTP methods needed

**AWSSunflower Changes:**
- New files:
  - `BindingPersistence.fs` — JSON config load/save
  - `LocoDetection.fs` — loco ID extraction from Player tree
- Modified files:
  - `Types.fs` — add `LocoId`, `BoundEndpoint`, `PollingState`, `BindingsConfig`
  - `ApiExplorer.fs` — extend Model, add Msg cases, extend update function, add "Bind" button to endpoint UI
  - `SerialPort.fs` — potentially add helper for formatted output (optional)

**Rationale:**
- TSWApi is a pure API client library — polling logic belongs in the application layer
- AWSSunflower owns the UX, serial port, and persistence concerns

## Work Decomposition

### Phase 1: Data Model & Persistence (2-3 hours)
**Owner:** Tom Rolt (UI Dev)  
**Deliverables:**
1. Add `LocoId`, `BoundEndpoint`, `PollingState`, `BindingsConfig` to `Types.fs`
2. Implement `BindingPersistence.fs` with `load` and `save` functions
3. Write unit tests for persistence (load missing file, roundtrip serialize/deserialize, invalid JSON)

**Dependencies:** None

---

### Phase 2: Loco Detection (3-4 hours)
**Owner:** Tom Rolt (UI Dev)  
**Deliverables:**
1. Manually explore TSW Player tree to locate loco metadata (run game, use API Explorer)
2. Implement `LocoDetection.fs` with `detectCurrentLoco` function
3. Write unit tests using mock HTTP responses
4. Add `LocoDetectionTimer` to MVU Model, wire up `LocoDetected` message

**Dependencies:** Phase 1 (needs `LocoId` type)

---

### Phase 3: Binding UI (2-3 hours)
**Owner:** Tom Rolt (UI Dev)  
**Deliverables:**
1. Add "Bind" button next to each endpoint in `endpointViewerPanel` (ApiExplorer.fs)
2. Dispatch `BindEndpoint` message on click
3. Update `update` function to handle `BindEndpoint` (add to `BoundEndpoints`, save config)
4. Add UI to show currently bound endpoints (list in sidebar or tab)

**Dependencies:** Phase 1 (needs `BoundEndpoint` type), Phase 2 (needs `CurrentLoco`)

---

### Phase 4: Polling Engine (4-5 hours)
**Owner:** Tom Rolt (UI Dev)  
**Deliverables:**
1. Add `PollingStates` and `PollingTimer` to Model
2. Implement `PollingTick` → `pollEndpointCmd` → `PollingValueReceived` flow
3. Compare `LastValue` in `PollingValueReceived`, dispatch serial send on change
4. Add timer lifecycle (start on first bind, stop on last unbind)
5. Write update function tests for polling flow

**Dependencies:** Phase 1 (needs `PollingState`), Phase 3 (needs bindings in model)

---

### Phase 5: Serial Integration (1-2 hours)
**Owner:** Tom Rolt (UI Dev)  
**Deliverables:**
1. Add `sendSerialCmd` helper in ApiExplorer.fs that calls `SerialPortModule.sendAsync`
2. Format data string as `"<nodePath>/<endpointName>=<value>\n"`
3. Handle `SerialSendSuccess` / `SerialSendError` messages
4. Manual test: bind endpoint, change value in game, verify serial output

**Dependencies:** Phase 4 (needs polling to detect changes)

---

### Phase 6: Integration Testing & Polish (2-3 hours)
**Owner:** Tom Rolt (UI Dev)  
**Deliverables:**
1. End-to-end test: start app → connect to API → detect loco → bind endpoint → poll → serial output
2. Test loco switching (exit/enter different loco, verify bindings reload)
3. Test config persistence (restart app, verify bindings restored)
4. Error handling (API unreachable during poll, serial port disconnected during send)
5. Update README with "Endpoint Binding" section

**Dependencies:** All previous phases

---

### Total Estimate: 14-20 hours

**Critical Path:** Phase 1 → Phase 2 → Phase 3 → Phase 4 → Phase 5 → Phase 6

**Parallelization Opportunities:** None — each phase depends on previous deliverables.

---

## Risks & Mitigations

### Risk 1: Loco Metadata Location Unknown
**Impact:** Medium — blocks Phase 2  
**Mitigation:** Use API Explorer on a running game to manually find the path. If metadata doesn't exist, fall back to hashing the entire Player tree as a LocoId (less user-friendly but functional).

### Risk 2: 500ms Polling Overhead
**Impact:** Low — may cause UI lag with many bindings  
**Mitigation:** Start with timer-based approach. If CPU usage is high, switch to batched GET requests (query all bound endpoints in one call if API supports it, or use Task.WhenAll for parallel queries).

### Risk 3: Serial Port Busy During Send
**Impact:** Low — `sendAsync` returns `Result<unit, SerialError>`  
**Mitigation:** Already handled by error type. On `SerialSendError`, increment `ErrorCount` in `PollingState`, disable polling for that endpoint after 3 consecutive failures.

### Risk 4: Config File Corruption
**Impact:** Low — user edits JSON manually, breaks schema  
**Mitigation:** `load` returns `Result<BindingsConfig, string>`. On error, log warning, return empty config, rename corrupt file to `bindings.json.bak`.

---

## Testing Strategy

### Unit Tests
- **BindingPersistence:** Load missing file, load valid JSON, load invalid JSON, save roundtrip, version migration (future)
- **LocoDetection:** Mock HTTP responses with Player tree, extract LocoId, handle missing fields
- **MVU Update:** `BindEndpoint`, `PollingValueReceived` value change/no change, `LocoDetected` loco change/same

### Integration Tests
- Manual test with real game: bind endpoint, drive loco, verify serial output
- Test loco switching: drive Loco A (bindings load), exit, drive Loco B (different bindings load)

### Performance Tests
- Poll 10 endpoints at 500ms for 60 seconds, measure CPU usage
- Verify no memory leaks (timer cleanup on disconnect)

---

## Open Questions

1. **Loco Metadata Path:** Need to inspect Player tree in running game. (Action: Tom to explore with API Explorer)
2. **Serial Protocol:** Is `"nodePath/endpointName=value\n"` acceptable? (Action: LondoSpark to confirm or specify alternative)
3. **Polling Interval Configurable?** Should 500ms be hardcoded or user-adjustable? (Action: LondoSpark to specify)
4. **UI for Bindings:** List in sidebar, separate tab, or overlay on endpoints? (Action: Tom to propose mockup)

---

## Alternatives Considered

### Alternative 1: Cmd-based Polling (Recursive Messages)
Instead of a timer, dispatch a `PollingTick` message that schedules itself after 500ms delay using `Async.Sleep`.

**Rejected Reason:** More complex to manage (no IDisposable handle to stop), harder to test, less idiomatic than timer-based approach in Avalonia apps.

### Alternative 2: Embed Bindings in TSWApi Library
Store config and polling logic in TSWApi, expose a `BindingManager` class.

**Rejected Reason:** Violates separation of concerns — TSWApi is a pure HTTP client library, not an application framework. Serial port and UI concerns should not leak into the library.

### Alternative 3: SQLite for Config
Use SQLite database instead of JSON file for bindings.

**Rejected Reason:** Overkill for a simple list of bindings. JSON is human-readable and editable. SQLite adds dependency and complexity with no clear benefit (no complex queries, no multi-user access).

---

## References

- **PRD:** `TSW_API_PRD.md`
- **Existing Code:** `AWSSunflower/ApiExplorer.fs`, `AWSSunflower/SerialPort.fs`, `TSWApi/ApiClient.fs`
- **Decision History:** `.squad/decisions.md` (MVU pattern, TDD directive, Git Flow)
- **Phase 1 Review:** `.squad/agents/talyllyn/history.md` (TSWApi architecture baseline)

---

## Approval Checklist

- [ ] Code review by Talyllyn (Lead)
- [ ] Manual integration test with TSW6 running
- [ ] Documentation updated (README.md)
- [ ] Tests passing (unit + integration)
- [ ] Feature branch merged to `develop`
- [ ] User acceptance (LondoSpark)

---

**Sign-off:**  
Talyllyn (Lead) — 2025-02-24  
*Awaiting team feedback and LondoSpark approval*
