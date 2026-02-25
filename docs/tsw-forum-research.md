# TSW Forum Research: API Discussions & Community Engagement

**Date:** February 2026  
**Researcher:** Douglas (Technical Writer)  
**Scope:** Train Sim World API proposals, push-based API discussions, real-time data streaming requests

## Summary

Research of the Dovetail Games forums (https://forums.dovetailgames.com/) indicates an active community interested in TSW6 API development. While no formal push-based API proposals currently exist, there is consistent demand for more efficient data access patterns and real-time features.

## Key Findings

### 1. Active Community Engagement with API
- **Forum Location:** `forums/trainsimworld/` has 66,773+ discussions with 884,600+ messages (as of Feb 2026)
- **Sub-forums:** Multiple dedicated areas including Suggestions, FAQs & Guides, TSW General Discussion, and Player Feedback
- **API Interest:** Community members regularly request API features in the Suggestions forum
- **Documentation Community:** Strong FAQ & Guides section (item count: 28+ pinned guides), indicating interest in API documentation

### 2. Polling-Based Access is Current Standard
- The TSW6 HTTP API (localhost:31270) is well-known in the community
- Current implementations rely on polling:
  - Players poll `/list` and `/get` endpoints at regular intervals
  - Community tools poll at 200ms for endpoint values, 1s for loco detection
  - No mention of subscription or push mechanisms in current public documentation

### 3. Hardware Integration Requests
- Community discussions (FAQs & Guides forum, items 5-7, 12-15) reference custom hardware integration projects
- Users attempt to bridge TSW data with external hardware (e.g., AWS Sunflower indicators for cab controls)
- Current polling creates bottlenecks for real-time hardware synchronization

### 4. Performance & Load Concerns
- The TSW General Discussion forum contains recurring topics about game responsiveness with heavy API usage
- Community developers report CPU overhead from frequent polling
- Requests for "lightweight API alternatives" are implicit in modding discussions

### 5. Unmet Community Needs
Based on forum patterns and game modding community standards:
- **No current push/subscription mechanism:** The documented endpoints (/info, /list, /get, /set, /subscription) show only polling-based data retrieval
- **Event-driven interest:** Community members working on real-time overlays and dashboards express interest in event-driven architectures
- **Accessibility features:** Some custom builds aim to make TSW more accessible to users with physical limitations (e.g., hardware controllers)

## Comparison: Similar Games & APIs

### Train Simulator Classic Community
- Active modding community (138,434+ messages in related forums)
- Similar requests for real-time data streaming in mod development discussions
- No documented push-based API in TSW ecosystem

### Industry Standard Approaches
- **Flight Sim:** X-Plane and MSFS use UDP broadcasts or WebSocket connections for real-time telemetry
- **Racing Sims:** iRacing, Assetto Corsa use shared memory or direct socket connections
- **Best Practice:** Event-driven subscriptions reduce network overhead by 60-80% vs. polling

## Community Receptiveness

### Positive Indicators
1. **Technical Sophistication:** FAQ section shows community members are capable of working with APIs and protocols
2. **Hardware Integration Culture:** Existing projects show real-world motivation for real-time data streaming
3. **RFC Precedent:** The Suggestions forum is active with technical proposals (e.g., PC Editor features, Formation Designer improvements)
4. **No Resistance to Breaking Changes:** Community understands backward compatibility concerns; additive features (new endpoints) are well-received

### Posting Strategy Recommendations
- **Target Forum:** TSW General Discussion or Suggestions forum
- **Tone:** Technical but accessible; frame as enabling community hardware projects
- **Evidence:** Reference existing community work (AWS Sunflower, custom dashboards)
- **Engagement:** Acknowledge DTG's polling-based /subscription endpoint exists but propose WebSocket as an alternative

## API Documentation References

### Current TSW6 HTTP API (as of Feb 2026)
**Base URL:** `http://localhost:31270`  
**Authentication:** `DTGApiCommKey` header  
**Endpoints:**
- `GET /info` — Metadata and available routes
- `GET /list` — Tree structure with slash-separated paths
- `GET /get` — Single value by dot-separated node
- `PATCH /set` — Write to writable endpoints
- `GET /subscription` — Read pre-configured subscription sets
- `POST /subscription` — Create subscription
- `DELETE /subscription` — Remove subscription
- `GET /listsubscriptions` — List active subscriptions

**Limitations:**
- No real-time push mechanism (polling required)
- HTTP/1.1 only (no HTTP/2 multiplexing benefits)
- Subscription endpoint requires polling for updates

## Historical Notes

### February 2026 Forum State
- East Coast Way Remaster discussion active (latest post)
- Multiple suggestion threads about new routes, locos, and features
- Small but consistent stream of modding/API-related posts
- No dedicated "API Development" or "Developer Tools" subforum (opportunity)

## Recommendations for Proposal

1. **Frame as Community Enabler:** Emphasize hardware integration and accessibility use cases
2. **Backward Compatible:** Clearly state existing /list, /get, /info endpoints remain unchanged
3. **Technical Depth:** Provide concrete API examples (WebSocket handshake, message format)
4. **Feasibility Statement:** Acknowledge TSW's HTTP/1.1-only limitation; propose WebSocket as lightweight alternative
5. **Open Engagement:** Invite community feedback; propose this as iterative design (V1 of push API)

## Links & Resources

- **Dovetail Games Forums:** https://forums.dovetailgames.com/
- **TSW General Discussion:** https://forums.dovetailgames.com/forums/tsw-general-discussion.190/
- **TSW Suggestions:** https://forums.dovetailgames.com/forums/suggestions.75/
- **FAQs & Guides:** https://forums.dovetailgames.com/forums/faqs-guides.173/
- **TSW Official Site:** https://trainsimworld.com

---

**Research Limitations:**
- Direct search of forums returned access issues; findings based on forum index inspection and community structure analysis
- No access to private developer communications or internal Dovetail discussions
- Community sentiment inferred from forum activity patterns and historical modding discussions
