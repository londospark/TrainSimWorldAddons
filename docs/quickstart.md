# Quickstart

## Prerequisites

- .NET 10 SDK
- Train Sim World 6 installed (for the CommAPIKey.txt file)

## Installation

Add a reference to `TSWApi` in your F# project:

```xml
<ProjectReference Include="..\TSWApi\TSWApi.fsproj" />
```

## Basic Usage

```fsharp
open System.Net.Http
open TSWApi.Types
open TSWApi.Http
open TSWApi.ApiClient

// 1. Discover the CommKey from your local TSW installation
let myGamesPath = @"C:\Users\YourName\Documents\My Games"
let commKey =
    match discoverCommKey myGamesPath with
    | Ok key -> key
    | Error err -> failwith $"Could not find CommKey: %A{err}"

// 2. Create the API config
let config = createConfig commKey

// 3. Create an HttpClient and make requests
let client = new HttpClient()

async {
    // Get game info
    let! info = getInfo client config
    match info with
    | Ok response -> printfn "Game: %s (Build %d)" response.Meta.GameName response.Meta.GameBuildNumber
    | Error err -> printfn "Error: %A" err

    // List root nodes
    let! nodes = listNodes client config None
    match nodes with
    | Ok response ->
        response.Nodes
        |> Option.iter (List.iter (fun n -> printfn "  %s" n.NodeName))
    | Error err -> printfn "Error: %A" err

    // Get a specific value
    let! value = getValue client config "CurrentDrivableActor/AWS_TPWS_Service.Property.AWS_SunflowerState"
    match value with
    | Ok response -> printfn "Value: %A" response.Values.["Value"]
    | Error err -> printfn "Error: %A" err
}
|> Async.RunSynchronously
```

## Tree Navigation

```fsharp
open TSWApi.TreeNavigation

// Parse and navigate node paths
let segments = parseNodePath "Root/Player/TransformComponent0"
// segments = ["Root"; "Player"; "TransformComponent0"]

// Find a node in a tree
let node = getNodeAtPath nodeList ["Player"; "TransformComponent0"]

// Find an endpoint on a node
let endpoint = findEndpoint someNode "Property.AWS_SunflowerState"

// Get child nodes
let children = getChildNodes someNode
```

## Network Configuration

By default, TSWApi connects to `http://localhost:31270`. To connect over a network:

```fsharp
let config = createConfigWithUrl "http://192.168.1.100:31270" commKey
```

## Error Handling

All API operations return `Async<Result<'T, ApiError>>`. Pattern match on the error cases:

```fsharp
match result with
| Ok data -> // use data
| Error (NetworkError ex) -> printfn "Network issue: %s" ex.Message
| Error (HttpError(status, msg)) -> printfn "HTTP %d: %s" status msg
| Error (AuthError msg) -> printfn "Auth failed: %s" msg
| Error (ParseError msg) -> printfn "Parse error: %s" msg
```
