# Decision: SQLite for Binding Persistence

**Date:** 2025-07-XX  
**Author:** Dolgoch (Core Dev)  
**Branch:** feature/elmish-sqlite  
**Status:** Implemented  

## Context

The binding persistence layer (`BindingPersistence.fs`) stored loco/endpoint bindings as a JSON file. This was replaced with SQLite to support future features (querying, concurrent access, atomic updates).

## Decision

- **Package:** `Microsoft.Data.Sqlite` (lightweight ADO.NET provider, no EF overhead)
- **DB location:** `%APPDATA%\LondoSpark\AWSSunflower\bindings.db`
- **Schema:** Two tables — `Locos` (id, loco_name) and `BoundEndpoints` (id, loco_id FK, node_path, endpoint_name, label)
- **Connection strategy:** Open/close per operation (no persistent connection)
- **Migration:** One-time automatic migration from `bindings.json` if DB doesn't exist but JSON does
- **Public API:** Unchanged — `load()`, `save()`, `addBinding()`, `removeBinding()` all preserved

## Impact

- **ApiExplorer.fs:** No changes needed — callers use the same API
- **Tests (Edward Thomas):** Test fixtures for BindingPersistence will need updating to use in-memory SQLite or temp DB paths
- **Types.fs:** No changes — `BindingsConfig`, `LocoConfig`, `BoundEndpoint` untouched
