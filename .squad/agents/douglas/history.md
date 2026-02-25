# Douglas — History

## Project Context
- **Project:** TrainSimWorldAddons — F# library wrapping the Train Sim World 6 HTTP API + AWSSunflower Avalonia FuncUI desktop app
- **Stack:** F#, .NET 10, HTTP, Avalonia 11.3, FuncUI
- **User:** LondoSpark
- **Team:** Talyllyn Railway engines squad

## Key Facts
- TSW6 exposes an HTTP API on localhost:31270 with DTGApiCommKey auth header
- API has /list (slash-separated paths) and /get (dot-separated last segment) endpoints
- Currently polling-based: 200ms for endpoint values, 1s for loco detection
- The API is read-only HTTP with no push/subscription mechanism currently

## Learnings

### 2026-02-25: TSW Forum Research & Push-Based API Proposal

**Task:** Research community API discussions and write a forum-ready proposal for push-based (WebSocket/SSE) data streaming.

**Findings:**
- Dovetail Games forums have active TSW community (66k+ discussions, 884k+ messages)
- No existing push/subscription mechanism proposals found in public forum discussions
- Community has strong hardware integration interest (AWS Sunflower, custom cabs, accessibility tools)
- Current polling creates 200ms latency; hardware projects struggle with flicker/lag
- FAQs & Guides section shows sophisticated technical community capable of using WebSocket APIs

**Deliverables Created:**
1. `docs/tsw-forum-research.md` — Community research, use cases, receptiveness analysis
2. `docs/push-api-proposal.md` — Complete forum-ready proposal with:
   - WebSocket `/subscribe` endpoint design
   - Server-Sent Events fallback for restricted networks
   - Concrete message format examples
   - Hardware integration use case walkthrough
   - 5-phase implementation roadmap
   - Security & backward compatibility analysis

**Tone & Strategy:**
- Professional, constructive, community-friendly language
- Emphasized backward compatibility (additive feature, existing endpoints unchanged)
- Included real-world use cases (hardware, overlays, accessibility)
- Provided concrete C# code examples
- Made actionable for DTG devs and community contributors

**Key Technical Decisions:**
- WebSocket primary, SSE fallback (handles restricted networks)
- Reuse DTGApiCommKey auth header (no new security model)
- Event-driven model (push only on value change, not periodic)
- Support mixed mode (polling + push in same client)
- Stateless connections (no server-side session persistence)
