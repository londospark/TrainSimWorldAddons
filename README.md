# TrainSimWorldAddons

F# tools and libraries for working with the Train Sim World 6 API.

## Projects

| Project | Description |
|---------|-------------|
| **TSWApi** | F# library wrapping the TSW6 HTTP API with type-safe, async operations |
| **AWSSunflower** | Avalonia FuncUI desktop app for AWS/TPWS sunflower display |

## TSWApi Library

### Features

- **Type-safe API client** — Strongly typed F# records for all TSW6 API responses
- **Automatic auth discovery** — Finds DTGCommKey from your local game installation
- **Tree navigation** — Navigate the TSW object tree with path-based helpers
- **Railway-oriented error handling** — `Async<Result<'T, ApiError>>` with typed error cases
- **Zero external dependencies** — Built on System.Text.Json and System.Net.Http

### Quick Start

```fsharp
open System.Net.Http
open TSWApi.Types
open TSWApi.Http
open TSWApi.ApiClient

// Discover auth key from your TSW6 installation
let myGamesPath = @"C:\Users\YourName\Documents\My Games"
match discoverCommKey myGamesPath with
| Ok key ->
    let config = createConfig key
    let client = new HttpClient()

    async {
        // Get game info
        let! info = getInfo client config
        // List the node tree
        let! nodes = listNodes client config None
        // Read a specific value
        let! value = getValue client config "CurrentDrivableActor/AWS_TPWS_Service.Property.AWS_SunflowerState"
        ()
    } |> Async.RunSynchronously
| Error err ->
    printfn "Auth error: %A" err
```

### API Modules

- **`Types`** — Response models (`InfoResponse`, `ListResponse`, `GetResponse`), error types (`ApiError`)
- **`Http`** — CommKey discovery, config creation, authenticated HTTP requests
- **`ApiClient`** — High-level operations: `getInfo`, `listNodes`, `getValue`
- **`TreeNavigation`** — `parseNodePath`, `getNodeAtPath`, `findEndpoint`, `getChildNodes`

## Building

```bash
dotnet build
dotnet test
```

## Documentation

```bash
dotnet tool restore
dotnet fsdocs build
```

Generated docs are output to `output/`.

## Requirements

- .NET 10 SDK
- Train Sim World 6 (for the CommAPIKey.txt authentication file)

## License

See [LICENSE](LICENSE) for details.
