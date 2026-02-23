# Decision: TSWApi Phase 1 — Lead Review APPROVED

**Author:** Talyllyn (Lead)
**Date:** 2025-07-22
**Issue:** #13

## Decision

Phase 1 of the TSWApi library is **APPROVED** for release. The library correctly implements all three GET endpoints from the PRD (/info, /list, /get), with proper authentication, type-safe deserialization, tree navigation, and comprehensive error handling.

## Observations for Phase 2 Planning

These are non-blocking items to address before or during Phase 2 work:

1. **`CollapsedChildren` field missing from `Node` type** — The PRD JSON shows `"CollapsedChildren": 188` on some nodes. Add `CollapsedChildren: int option` to the Node record before Phase 2 tree features.

2. **`sendRequest` hardcodes GET** — Phase 2 needs POST/PATCH/DELETE for `/set` and `/subscription`. Recommend adding a `sendRequestWithMethod` variant or making the HTTP method a parameter.

3. **`GetResponse.Values` uses `Dictionary<string, obj>`** — Consider `Dictionary<string, JsonElement>` to preserve type information without forcing consumers to unbox. This is a breaking change so decide before 1.0.

4. **URL-encoded node names** — The PRD shows paths like `Electric%28PushButton%29`. No tests cover this pattern. Add test coverage before Phase 2.

## Rationale

The library meets all Phase 1 PRD requirements, follows F# idioms (DUs, option types, railway-oriented error handling), has 53 passing tests with good edge case coverage, and includes complete documentation. The architecture is extensible for Phase 2 with the noted modifications.
