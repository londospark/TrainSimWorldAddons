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
open TSWApi

// Initialize the client (discovers CommKey automatically)
let! client = Http.initializeClient()

match client with
| Ok httpClient ->
    // Get game info
    let! info = ApiClient.getInfo httpClient
    
    // List all nodes
    let! nodes = ApiClient.listNodes httpClient None
    
    // Get a specific value
    let! value = ApiClient.getValue<int> httpClient 
                    "Root/CurrentDrivableActor/AWS_TPWS_Service.Property.AWS_SunflowerState"
    ()
| Error err ->
    printfn "Failed to connect: %A" err
```

## Configuration

By default, TSWApi connects to `http://localhost:31270`. To connect over a network:

```fsharp
let! client = Http.initializeClient(baseUrl = "http://192.168.1.100:31270")
```
