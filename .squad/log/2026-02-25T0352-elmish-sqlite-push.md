# Session Log: Elmish + SQLite Push Foundation

**Date:** 2026-02-25T03:52  
**Scope:** Elmish migration, SQLite persistence, push API research  

## Session Objectives

✅ **Completed:**
1. Migrate hand-rolled MVU to Avalonia.FuncUI.Elmish (Tom Rolt)
2. Replace JSON binding persistence with SQLite (Dolgoch)
3. Fix test isolation bugs (Coordinator)
4. Research TSW push API proposals (Douglas)

🚧 **In Progress:**
- Comprehensive test suite for Elmish/SQLite/polling (Edward Thomas)

## Technical Outcomes

### 1. BindingPersistence.fs (Dolgoch)
- **Approach:** Microsoft.Data.Sqlite for deterministic storage
- **Auto-migration:** Detects JSON files, hydrates DB, removes JSON
- **API:** Unchanged (drop-in replacement)
- **Testing:** Existing binding tests pass
- **Code Location:** `TSWApi/BindingPersistence.fs`

### 2. Elmish UI State Management (Tom Rolt)
- **Approach:** Avalonia.FuncUI.Elmish for predictable state transitions
- **Polling Config:**
  - Values: 200ms
  - Loco state: 1s
- **Loco Change Handling:** Reloads TSWBridge config, clears stale cached bindings
- **Test Results:** 106 passing
- **Code Location:** UI layer (Avalonia components)

### 3. Test Isolation Fixes (Coordinator)
- **Root Cause:** `addBinding`/`removeBinding` were executing DB reads during in-memory tests
- **Fix:** Made binding mutations pure (in-memory cache only), deferred to explicit flush
- **Impact:** 1 failing test recovered; test suite stable
- **Code Location:** Test helpers and binding service

### 4. Push API Documentation (Douglas)
- **Files Created:**
  - `docs/tsw-forum-research.md` — Analysis of TSW community proposals
  - `docs/push-api-proposal.md` — Initial design for push-based binding notification
- **Next Steps:** Architecture review before implementation spike

## Risk Mitigation

| Risk | Mitigation |
|------|-----------|
| SQLite concurrency in polling | Tested with 200/1000ms interval mix |
| Stale bindings after loco change | Config reload + cache clear in handler |
| Test flakiness from DB side effects | Made binding mutations pure in-memory |
| Unclear push API surface | Forum research + proposal doc ready for review |

## Decisions Recorded

- Use Elmish for UI state (not hand-rolled MVU)
- Use SQLite for binding persistence (not hand-rolled JSON)
- 200ms value polling, 1s loco polling (balances latency vs load)
- Auto-migration for JSON→SQLite (zero admin overhead)
- Push API design phase before implementation (avoid rework)

## Files Changed

```
TSWApi/
  BindingPersistence.fs         (new)
  BindingPersistence.Tests.fs   (updated)
  
TSWApi.Tests/
  BindingServiceTests.fs        (updated)
  
UI/
  *.fs                          (Elmish integration)
  
docs/
  tsw-forum-research.md         (new)
  push-api-proposal.md          (new)
```

## Next Batch

1. Merge orchestration changes in dependency order
2. Complete Edward Thomas test suite (in progress)
3. Begin push API architecture review
4. Profile SQLite I/O under realistic load
