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
