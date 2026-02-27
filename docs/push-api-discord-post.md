**🚂 Proposal: Push-Based API for TSW6**

Hey all — I've been building tools against the TSW6 HTTP API (`localhost:31270`) and the biggest limitation is **polling**. Right now we all have to loop `GET /get` every 200ms per endpoint. 98% of those requests return unchanged data, it wastes CPU, and hardware integrations (like an AWS Sunflower indicator) get jitter/lag.

**The idea:** Add a **WebSocket endpoint** (`ws://localhost:31270/subscribe`) where the server pushes updates *only when values actually change*. You'd send:
```json
{ "action": "subscribe", "paths": ["CurrentDrivableActor/BP_AWS_TPWS_Service.Property.AWS_SunflowerState", "CurrentDrivableActor.Function.HUD_GetSpeed"] }
```
…and the server streams back changes as they happen (<10ms latency vs 200ms polling). Plus a `loco_changed` event when you switch trains — no more polling `/list` to detect that.

**Benefits:** ~60-80% less network overhead, real-time hardware sync, simpler client code. Fully backward-compatible — existing `/list`, `/get`, `/set` stay the same.

**SSE fallback** (`GET /events?paths=...`) for restricted environments.

I've written up a full spec with message formats, auth (reuses `DTGApiCommKey`), error handling, and implementation notes. Happy to share the full doc if there's interest. Would love to hear if others have thought about this or have alternative approaches! 🙂
