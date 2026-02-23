# API Explorer Tab Architecture

**Context:** Issue #24 - Add API Explorer tab to AWSSunflower application for runtime TSW6 API exploration.

**Decision:** Implement as TabControl-based UI with lazy-loading tree navigation.

**Rationale:**
- **TabControl over separate window**: Keeps all functionality in one window, reduces complexity. Serial Port and API Explorer are parallel concerns, not parent-child. Tabs provide clear separation with easy switching.
- **Lazy tree loading over eager**: TSW6 node hierarchy can be large. Fetching entire tree upfront would be slow and memory-intensive. Load children on-demand when user expands node. Matches standard tree browser UX.
- **Lifted state pattern**: All API Explorer state (connection, tree, selection, values) managed via ctx.useState in component function. Consistent with existing AWSSunflower patterns. Enables reactive UI updates.
- **Shared HttpClient instance**: Created once at module level, reused for all requests. Follows best practices for HttpClient (avoid socket exhaustion, connection pooling).
- **Auto-discovery for CommKey**: Users shouldn't need to manually locate CommAPIKey.txt. TSWApi.Http.discoverCommKey searches My Games folders automatically. Falls back to manual entry if discovery fails.

**Alternatives Considered:**
- Separate window for API Explorer: Rejected - increases complexity, less discoverable
- Eager tree loading: Rejected - performance concerns, unnecessary network traffic
- Manual CommKey entry only: Rejected - poor UX, users don't know where file is located

**Implications:**
- Future tabs can be added easily (e.g., Scenario Editor, Log Viewer) using same TabItem pattern
- Tree state management is self-contained in ApiExplorer.fs - no cross-tab dependencies
- TSWApi library remains UI-agnostic - ApiExplorer.fs is thin adapter over API client

**Team Members:**
- Tom Rolt (UI Dev) - Implemented
- LondoSpark (User) - Requested
