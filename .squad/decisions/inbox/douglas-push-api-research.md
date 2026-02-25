# Decision: Push-Based API Proposal Design (WebSocket + SSE)

**Date:** 2026-02-25  
**Author:** Douglas (Technical Writer) with LondoSpark (Project Lead)  
**Status:** Proposed  
**Related Issue:** N/A (Community engagement, not code change)  

## Decision Summary

We are proposing a **push-based API for TSW6 using WebSocket as primary and Server-Sent Events (SSE) as fallback**. This replaces the inefficient polling model currently used by clients. The design is fully backward-compatible — all existing `/list`, `/get`, `/info`, `/set` endpoints remain unchanged.

## Problem Statement

Current TSW6 API usage relies on polling:
- Clients poll `/get` endpoints every 200ms for real-time data
- Hardware integration projects (AWS Sunflower indicators) experience lag (up to 200ms latency)
- High network overhead: 98% of requests return unchanged data
- CPU utilization burden on both client and server
- Limits real-time applications (overlays, accessibility tools, hardware sync)

## Proposed Solution

### Primary: WebSocket (`/subscribe` endpoint)

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

### Fallback: Server-Sent Events (`/events` endpoint)

For environments blocking WebSocket, provide HTTP streaming:

```
GET http://localhost:31270/events?paths=CurrentDrivableActor.HUD_GetSpeed
Content-Type: text/event-stream
DTGApiCommKey: <key>
```

## Key Design Decisions

1. **Event-Driven:** Push only when values change, not periodic polling
2. **Low Latency:** < 10ms per update (vs. 200ms polling interval)
3. **Backward Compatible:** No changes to existing endpoints
4. **Stateless:** Connections are independent; no server-side session persistence
5. **Mixed Mode:** Clients can use both polling and push simultaneously
6. **Simple Auth:** Reuse existing `DTGApiCommKey` (no new security model)

## Benefits

- **60-80% network overhead reduction** compared to polling
- **Real-time hardware integration** with minimal lag
- **Simplified client code** (no polling loops, no guessing at refresh rates)
- **Accessibility** (enables assistive hardware and custom controllers)
- **Content Creator Tools** (overlays, dashboards, telemetry streaming)

## Rationale: Why WebSocket Over Alternatives?

| Approach | Pros | Cons | Decision |
|----------|------|------|----------|
| **Long-Polling** | HTTP only | Still requires continuous requests | ❌ Doesn't solve core problem |
| **HTTP/2 Server Push** | Standards-based | TSW6 runs HTTP/1.1 only | ❌ Not available |
| **WebSocket** | Full-duplex, low overhead, real-time | Requires WebSocket support in Unreal | ✅ **CHOSEN** |
| **SSE** | HTTP streaming, simpler than WebSocket | Single-directional, less efficient | ✅ **Fallback** |

## Implementation Roadmap (Proposed)

**Phase 1 (High Priority):** WebSocket foundation, subscribe/unsubscribe, change detection  
**Phase 2 (Medium Priority):** Loco change events, subscription management, rate limiting  
**Phase 3 (Low Priority):** SSE fallback endpoint  
**Phase 4 (Community):** SDKs, documentation, example projects  

## Security & Risk Assessment

### Authentication
- Reuse existing `DTGApiCommKey` header (no new attack surface)
- Same localhost-only restriction as current API
- Game must be running (same as current model)

### DoS Protection Considerations
- Max subscriptions per connection: 100
- Max update rate: 1000 updates/sec per connection
- Idle timeout: 5 minutes with no activity
- Invalid message handling (graceful error responses)

### Data Privacy
- No new data exposed; WebSocket streams only existing `/list` and `/get` data
- Same access control as polling API

## Backward Compatibility

✅ **Fully additive feature:**
- Existing `/list`, `/get`, `/info`, `/set` endpoints remain unchanged
- Clients can continue using polling if preferred
- No migration required for existing tools
- New `/subscribe` and `/events` endpoints are opt-in

## Community Impact

### Enablers
- Hardware integration projects (AWS Sunflower, custom cabs)
- Real-time overlay tools and dashboards
- Accessibility tools (assistive hardware)
- Multi-player virtual operations
- Telemetry and performance analytics

### Who Cares?
- Content creators (stream overlays, dashboards)
- Hardware enthusiasts (custom controllers, indicators)
- Accessibility tool developers
- Virtual operations communities (train crew simulators)
- Modding and tool developers

## Recommendation for Dovetail Games

1. **Evaluate feasibility** — Can Unreal's HTTP server support WebSocket?
2. **Design review** — Does the message format meet performance targets?
3. **Prototype Phase 1** — Implement `/subscribe` WebSocket endpoint
4. **Community beta test** — Engage hardware integration projects
5. **Iterate** — Gather feedback, refine API before wider release

## Communication Strategy

- **Forum Post:** Community Suggestions forum at `forums.dovetailgames.com/forums/suggestions.75/`
- **Tone:** Technical but accessible; emphasize community enablement
- **Evidence:** Reference existing hardware projects, accessibility needs
- **Call to Action:** Invite community feedback and beta testing volunteers

## Open Questions for Feedback

1. Should we support path wildcards (e.g., `CurrentDrivableActor/*`)?
2. What update rate limit is reasonable?
3. Should subscriptions persist across loco changes, or require re-subscription?
4. Is a "subscribe-once, stream all updates" model preferred over selective path filtering?

---

**This decision will be presented to the community via forum post. Team feedback and DTG response will inform next steps.**
