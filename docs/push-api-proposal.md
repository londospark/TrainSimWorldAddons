# TSW6 Push-Based API Proposal: Event-Driven Data Streaming

**Author:** LondoSpark & Community Contributors  
**Version:** 1.0  
**Date:** February 2026  
**Status:** Community Proposal — Feedback Welcome  

---

## Executive Summary

Train Sim World 6's HTTP API is powerful but limited by polling. This proposal introduces a **push-based subscription API** using WebSocket (with Server-Sent Events as a fallback) to enable real-time, event-driven data streaming. The new endpoints are **additive and backward-compatible** — all existing `/list`, `/get`, `/info`, and `/set` endpoints remain unchanged.

**Key Benefits:**
- ✅ **60-80% reduction in network overhead** vs. polling
- ✅ **Latency < 10ms** per update (event-driven vs. 200ms polling interval)
- ✅ **Simplified client code** — no polling loops, no guess work on refresh rates
- ✅ **Hardware integration** — enables real-time control of physical devices (AWS Sunflower indicators, custom cabs)
- ✅ **Accessibility** — supports adaptive controllers and assistive hardware

---

## Background: The Polling Problem

### Current State (HTTP Polling)
TSW6's API runs on `localhost:31270` with three main endpoints:

```
GET  /info              — Metadata and available routes
GET  /list              — Tree structure of all available paths
GET  /get               — Retrieve a single value
PATCH /set              — Write to writable endpoints
```

**Current Usage Pattern:**
```
Client Poll Loop:
  ├─ Every 200ms: GET /get?Path=CurrentDrivableActor.HUD_GetSpeed
  ├─ Every 200ms: GET /get?Path=CurrentDrivableActor.BP_AWS_TPWS_Service.AWS_SunflowerState
  ├─ Every 200ms: GET /get?Path=... (repeat for 10-20 endpoints)
  └─ Every 1s: GET /list (to detect new locos, changed hardware state)
```

**Problems with Polling:**
1. **Wasteful:** 98% of requests return unchanged data
2. **Unpredictable Latency:** Updates occur up to 200ms *after* the event (not ideal for hardware sync)
3. **CPU Overhead:** Client loop consumes thread(s); server context-switches for every request
4. **Scalability:** 10 concurrent clients = 50+ HTTP requests/second
5. **Hardware Integration Difficult:** Real-time indicators (AWS Sunflower) lag or flicker due to polling jitter

### Existing /subscription Endpoint (Limited)
TSW6 has a polling-based `/subscription` endpoint:

```
POST /subscription  { Path: "..." } → Creates a subscription set
GET  /subscription  ?Subscription=N  → Returns all subscribed values (still polling!)
```

This helps batch requests but **still requires polling the subscription endpoint for updates.**

---

## Proposed Solution: WebSocket Push API

### Design Principles

1. **Event-Driven:** Server pushes updates only when values *actually change*
2. **Backward Compatible:** All existing endpoints untouched; new `/subscribe` endpoint is additive
3. **Low Latency:** Real-time streaming (< 10ms per update)
4. **Flexible Authentication:** Reuses existing `DTGApiCommKey` header protocol
5. **Failover Support:** Server-Sent Events (SSE) fallback for restricted networks

### Endpoint 1: WebSocket Subscription (Primary)

**URL:**
```
ws://localhost:31270/subscribe
```

**Authentication:**
Include the auth header in the WebSocket upgrade request:
```
GET /subscribe HTTP/1.1
Host: localhost:31270
Upgrade: websocket
Connection: Upgrade
Sec-WebSocket-Key: ...
DTGApiCommKey: <key-from-CommAPIKey.txt>
```

**Client → Server: Subscribe Message**

```json
{
  "action": "subscribe",
  "paths": [
    "CurrentDrivableActor/BP_AWS_TPWS_Service.Property.AWS_SunflowerState",
    "CurrentDrivableActor.Function.HUD_GetSpeed",
    "CurrentDrivableActor.Function.HUD_GetPantograph",
    "CurrentDrivableActor.Function.HUD_GetHeadlights",
    "Root/CurrentDrivableActor.Function.ScenarioGetLocoName"
  ]
}
```

**Server → Client: Update Messages**

Sent whenever a subscribed value changes:

```json
{
  "path": "CurrentDrivableActor.Function.HUD_GetSpeed",
  "value": "45.5",
  "type": "float",
  "timestamp": "2026-02-25T15:32:47.123Z",
  "loco_changed": false
}
```

**Loco Change Notification:**

When the player switches locomotives, the server sends a `loco_changed` event:

```json
{
  "event": "loco_changed",
  "new_loco": "GWR Dean Goods 2361",
  "timestamp": "2026-02-25T15:33:02.456Z",
  "available_paths": [
    "CurrentDrivableActor/BP_AWS_TPWS_Service.Property.AWS_SunflowerState",
    "CurrentDrivableActor.Function.HUD_GetSpeed",
    ...
  ]
}
```

**Client → Server: Unsubscribe Message**

```json
{
  "action": "unsubscribe",
  "paths": ["CurrentDrivableActor.Function.HUD_GetSpeed"]
}
```

**Client → Server: Resubscribe/Update Message**

```json
{
  "action": "subscribe",
  "paths": ["SomeNewPath/Property.NewValue"],
  "replace": false
}
```

Setting `"replace": true` clears previous subscriptions and uses only the new list.

---

### Endpoint 2: Server-Sent Events Fallback (Secondary)

For environments where WebSocket is restricted (e.g., strict corporate networks), use HTTP Server-Sent Events (SSE):

**URL:**
```
GET http://localhost:31270/events?paths=CurrentDrivableActor.HUD_GetSpeed,CurrentDrivableActor.HUD_GetPantograph
```

**Headers:**
```
DTGApiCommKey: <key>
Accept: text/event-stream
```

**Response (text/event-stream):**

```
data: {"path": "CurrentDrivableActor.HUD_GetSpeed", "value": "45.5", "type": "float", "timestamp": "2026-02-25T15:32:47.123Z"}

data: {"path": "CurrentDrivableActor.HUD_GetPantograph", "value": "1", "type": "int", "timestamp": "2026-02-25T15:32:48.456Z"}

data: {"event": "loco_changed", "new_loco": "GWR Dean Goods 2361"}
```

---

## Example: AWS Sunflower Hardware Integration

### Current (Polling) Implementation

```csharp
// Polling approach: wasteful, laggy
while (true)
{
    var response = await client.GetAsync("/get?Path=CurrentDrivableActor.BP_AWS_TPWS_Service.AWS_SunflowerState");
    var state = await response.Content.ReadAsAsync<int>();
    
    await serialPort.WriteAsync($"{state}\n");  // Send to hardware
    await Task.Delay(200);  // Poll every 200ms → updates lag up to 200ms
}
```

### Proposed (WebSocket Push) Implementation

```csharp
// Push approach: responsive, efficient
using var ws = new ClientWebSocket();
await ws.ConnectAsync(new Uri("ws://localhost:31270/subscribe"), CancellationToken.None);

var subscribeMsg = JsonSerializer.Serialize(new {
    action = "subscribe",
    paths = new[] {
        "CurrentDrivableActor/BP_AWS_TPWS_Service.Property.AWS_SunflowerState"
    }
});

await ws.SendAsync(
    Encoding.UTF8.GetBytes(subscribeMsg),
    WebSocketMessageType.Text,
    endOfMessage: true,
    CancellationToken.None
);

// Listen for updates
var buffer = new byte[1024 * 4];
while (true)
{
    var result = await ws.ReceiveAsync(buffer, CancellationToken.None);
    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
    var update = JsonSerializer.Deserialize<dynamic>(json);
    
    // Hardware updates in real-time with < 10ms latency
    await serialPort.WriteAsync($"{update["value"]}\n");
}
```

**Benefits:**
- ✅ No polling loop overhead
- ✅ < 10ms latency from game event to hardware (vs. 200ms with polling)
- ✅ Network traffic drops by 80% (only send when value changes)
- ✅ CPU utilization lower on client and server

---

## Technical Specifications

### Error Handling

**Invalid Path in Subscribe:**
```json
{
  "error": true,
  "code": "INVALID_PATH",
  "path": "InvalidPath/NoSuchProperty",
  "message": "Path does not exist in current locomotive or game state"
}
```

**Authentication Failure:**
```json
{
  "error": true,
  "code": "AUTH_FAILED",
  "message": "Invalid or missing DTGApiCommKey"
}
```

**Connection Timeout:**
After 5 minutes of inactivity, server may close the connection with:
```
WebSocket Close Code: 1000 (Normal Closure)
Reason: "Idle timeout after 5 minutes with no subscriptions"
```

### Data Types

Responses include a `"type"` field for type safety:
- `"string"` — Text value
- `"int"` — Integer (32-bit)
- `"float"` — Floating-point
- `"bool"` — Boolean
- `"null"` — No value / endpoint missing

### Rate Limiting (Optional, for Future)

To prevent spam, the server *may* implement:
- Max 100 subscriptions per connection
- Max 1000 updates/second per connection
- Response codes: `429 Too Many Requests`

---

## Backward Compatibility

✅ **All existing endpoints remain unchanged:**
- `GET /info` — Still available
- `GET /list` — Still available
- `GET /get` — Still available
- `PATCH /set` — Still available
- `GET /subscription` → Polling endpoint (legacy)

✅ **Opt-in feature:** Clients can use new `/subscribe` endpoint OR continue with polling. No breaking changes.

✅ **Mixed Mode:** A client can use both polling (`/get`) and push (`/subscribe`) simultaneously.

---

## Use Cases Enabled

### 1. Hardware Integration (AWS Sunflower, Custom Cabs)
Real-time synchronization of physical indicators with in-game state:
- AWS Sunflower push-button status
- Pantograph position indicators
- Speed gauges
- Signal status displays

### 2. Real-Time Dashboards & Overlays
Streaming dashboards for content creators, racing leagues, and virtual operations:
- Speed, brake pressure, power consumption feeds
- Signal and point status
- In-cab information displays

### 3. Accessibility Tools
Adaptive controllers and assistive devices:
- Custom hardware for players with mobility limitations
- Simplified control interfaces with real-time feedback

### 4. Telemetry & Analytics
Lightweight logging of game events:
- Accident/incident recording (collision detection)
- Performance analysis (fuel consumption, speed profiles)
- Training metrics for virtual railway operators

### 5. Multi-Player Coordination
Virtual operations and train crew simulators:
- Real-time coordination of multiple locomotives
- Shared signaling/dispatch state
- Crew role assignments

---

## Implementation Roadmap (Proposed)

### Phase 1: WebSocket Foundation (Priority High)
- Implement `/subscribe` WebSocket endpoint
- Basic subscribe/unsubscribe messages
- Value change detection and broadcasting
- Error handling for invalid paths

**Estimated Effort:** 2-3 weeks (DTG Unreal Engine team)

### Phase 2: Enhanced Features (Priority Medium)
- Loco change detection and notification
- Subscription management (list active subscriptions)
- Configurable update rate limiting
- Connection timeout handling

**Estimated Effort:** 1-2 weeks

### Phase 3: Server-Sent Events Fallback (Priority Low)
- Implement `/events` endpoint for ESS support
- Duplicate the subscription logic over HTTP streaming

**Estimated Effort:** 1 week

### Phase 4: Documentation & SDKs (Community)
- Official API documentation
- Community SDK libraries (.NET, Python, Rust, etc.)
- Example projects (hardware integration, overlay tools)

---

## Security Considerations

### Authentication
- Reuse existing `DTGApiCommKey` header validation
- Same restrictions apply: localhost only, game must be running
- No additional authentication overhead

### DoS Protection
- Maximum subscriptions per connection (100)
- Maximum update rate throttling (1000/sec)
- Connection idle timeout (5 minutes)
- Invalid JSON message handling

### Data Privacy
- No new data is exposed; push API only streams existing `/list` and `/get` endpoints
- Same access control as polling API

---

## Alternative Approaches Considered

### 1. Long-Polling
**Pros:** HTTP only, simpler implementation  
**Cons:** Still requires continuous requests; minimal latency improvement over polling  
**Verdict:** ❌ Doesn't solve the core problem

### 2. Polling with Conditional ETag
**Pros:** Reduces payload size  
**Cons:** Still requires request-per-change; network overhead remains  
**Verdict:** ❌ Incremental improvement only

### 3. HTTP/2 Server Push
**Pros:** Standards-based, native HTTP  
**Cons:** TSW6 runs HTTP/1.1 only; HTTP/2 requires TLS on some clients  
**Verdict:** ❌ Not available in current TSW6 architecture

### 4. WebSocket Chosen ✅
**Pros:** Full-duplex, low overhead, real-time, widely supported  
**Cons:** Requires WebSocket support in Unreal HTTP server  
**Verdict:** ✅ Best fit for TSW6 constraints and community needs

---

## Call to Action

### For Dovetail Games
We respectfully request evaluation of this proposal. The community is ready to help:
- **Beta testing** real hardware integration scenarios
- **SDK contributions** in various languages
- **Documentation & examples** for API adoption
- **Feedback** on design decisions and tradeoffs

### For the Community
If you're interested in real-time hardware integration, accessibility tools, or overlay development:
- React to this thread with interest
- Share your use cases and requirements
- Propose refinements to this design
- Volunteer to contribute client-side SDKs

### For DTG Developers
Questions? Contact the community through this thread. We're excited to help shape this feature!

---

## FAQ

**Q: Will this break existing polling-based tools?**  
A: No. All existing `/list`, `/get`, `/info`, `/set` endpoints remain unchanged. This is a new, optional feature.

**Q: What if I'm behind a firewall that blocks WebSocket?**  
A: The Server-Sent Events fallback provides HTTP streaming. Both can coexist.

**Q: Can I mix polling and push in the same client?**  
A: Yes, absolutely. Use push for performance-critical paths and polling for infrequent queries.

**Q: How do I authenticate with the WebSocket endpoint?**  
A: Include the standard `DTGApiCommKey` header in the WebSocket upgrade request (same as HTTP requests).

**Q: Will push API consume more server resources?**  
A: No — fewer requests overall. Server only pushes when values change (event-driven). Less total work than 200ms polling.

**Q: Can I subscribe to the same path multiple times?**  
A: Recommended: Subscribe once per client connection. The server will bundle all updates for that path and send once.

**Q: What happens if I disconnect mid-stream?**  
A: The connection closes. Reconnect and resubscribe to resume. The API is stateless — no server-side session persistence.

**Q: Is this compatible with the existing `/subscription` polling endpoint?**  
A: Yes. They can coexist. The new WebSocket API is an alternative, not a replacement.

---

## Appendix: Example Messages

### WebSocket Message Flow (AWS Sunflower Example)

```
[CLIENT → SERVER]
{
  "action": "subscribe",
  "paths": [
    "CurrentDrivableActor/BP_AWS_TPWS_Service.Property.AWS_SunflowerState"
  ]
}

[SERVER → CLIENT]
{
  "status": "subscribed",
  "paths": ["CurrentDrivableActor/BP_AWS_TPWS_Service.Property.AWS_SunflowerState"],
  "current_values": {
    "CurrentDrivableActor/BP_AWS_TPWS_Service.Property.AWS_SunflowerState": {
      "value": "0",
      "type": "int",
      "timestamp": "2026-02-25T15:30:00.000Z"
    }
  }
}

[SERVER → CLIENT] (when state changes)
{
  "path": "CurrentDrivableActor/BP_AWS_TPWS_Service.Property.AWS_SunflowerState",
  "value": "1",
  "type": "int",
  "timestamp": "2026-02-25T15:30:00.050Z",
  "loco_changed": false
}

[CLIENT → SERVER]
{
  "action": "unsubscribe",
  "paths": ["CurrentDrivableActor/BP_AWS_TPWS_Service.Property.AWS_SunflowerState"]
}

[SERVER → CLIENT]
{
  "status": "unsubscribed",
  "paths": ["CurrentDrivableActor/BP_AWS_TPWS_Service.Property.AWS_SunflowerState"]
}
```

---

## Feedback & Discussion

**Share your thoughts in this thread:**
- ✅ Use cases you'd like to see supported
- ✅ Design feedback (message format, latency requirements)
- ✅ Alternative approaches or improvements
- ✅ Commit to helping test or develop client SDKs

**Thank you for reading! Let's make real-time TSW integration a reality.** 🚂

---

*Proposal prepared by LondoSpark & community. Special thanks to the TrainSimWorldAddons team for research and technical feedback.*
