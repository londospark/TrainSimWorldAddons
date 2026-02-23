# TSWApi

F# library for interacting with the Train Sim World 6 HTTP API.

## Overview

TSWApi provides a type-safe, async-first F# wrapper around the Train Sim World 6 communication API. It handles authentication, HTTP requests, JSON deserialization, and tree navigation so you can focus on building tools and integrations.

## Getting Started

See the [Quickstart Guide](quickstart.html) for setup instructions.

## Features

- **Type-safe API client** — Strongly typed F# records for all API responses (`InfoResponse`, `ListResponse`, `GetResponse`)
- **Automatic auth discovery** — Finds the highest-numbered TrainSimWorld directory and reads the DTGCommKey automatically
- **Tree navigation** — Navigate the TSW object tree with `getNodeAtPath`, `findEndpoint`, and `getChildNodes`
- **Railway-oriented error handling** — All operations return `Async<Result<'T, ApiError>>` with typed error cases
- **Zero external dependencies** — Uses System.Text.Json and built-in .NET HTTP client

## Modules

| Module | Description |
|--------|-------------|
| `Types` | API response models, error types, and configuration records |
| `Http` | CommKey discovery, HTTP client configuration, authenticated requests |
| `ApiClient` | High-level operations: `getInfo`, `listNodes`, `getValue` |
| `TreeNavigation` | Path parsing, tree traversal, endpoint lookup |

## API Reference

See the [API Documentation](reference/index.html) for full type and function reference.
