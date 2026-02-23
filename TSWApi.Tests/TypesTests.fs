module TSWApi.Tests.TypesTests

open Xunit
open System.Text.Json
open TSWApi.Types

let jsonOptions =
    let opts = JsonSerializerOptions(PropertyNameCaseInsensitive = true)
    opts

// ── ApiError DU ──

[<Fact>]
let ``ApiError NetworkError wraps exception`` () =
    let ex = System.Exception("connection refused")
    let err = NetworkError ex
    match err with
    | NetworkError e -> Assert.Equal("connection refused", e.Message)
    | _ -> Assert.Fail("Expected NetworkError")

[<Fact>]
let ``ApiError HttpError carries status and message`` () =
    let err = HttpError(404, "Not Found")
    match err with
    | HttpError(status, msg) -> Assert.Equal(404, status); Assert.Equal("Not Found", msg)
    | _ -> Assert.Fail("Expected HttpError")

[<Fact>]
let ``ApiError AuthError carries message`` () =
    let err = AuthError "Missing CommKey"
    match err with
    | AuthError msg -> Assert.Equal("Missing CommKey", msg)
    | _ -> Assert.Fail("Expected AuthError")

[<Fact>]
let ``ApiError ParseError carries message`` () =
    let err = ParseError "Invalid JSON"
    match err with
    | ParseError msg -> Assert.Equal("Invalid JSON", msg)
    | _ -> Assert.Fail("Expected ParseError")

// ── ApiConfig ──

[<Fact>]
let ``ApiConfig defaults to localhost`` () =
    let config = { BaseUrl = "http://localhost:31270"; CommKey = "test-key" }
    Assert.Equal("http://localhost:31270", config.BaseUrl)
    Assert.Equal("test-key", config.CommKey)

// ── InfoResponse deserialization ──

let infoJson = """
{
  "Meta": {
    "Worker": "DTGCommWorkerRC",
    "GameName": "Train Sim World 6®",
    "GameBuildNumber": 749,
    "APIVersion": 1,
    "GameInstanceID": "A69D53564DFE46B7DE5AD7885CF0AA82"
  },
  "HttpRoutes": [
    {
      "Verb": "GET",
      "Path": "/info",
      "Description": "Get information about available commands."
    },
    {
      "Verb": "GET",
      "Path": "/list",
      "Description": "List all valid paths for commands."
    }
  ]
}"""

[<Fact>]
let ``InfoResponse deserializes Meta fields`` () =
    let result = JsonSerializer.Deserialize<InfoResponse>(infoJson, jsonOptions)
    Assert.Equal("DTGCommWorkerRC", result.Meta.Worker)
    Assert.Equal("Train Sim World 6®", result.Meta.GameName)
    Assert.Equal(749, result.Meta.GameBuildNumber)
    Assert.Equal(1, result.Meta.APIVersion)
    Assert.Equal("A69D53564DFE46B7DE5AD7885CF0AA82", result.Meta.GameInstanceID)

[<Fact>]
let ``InfoResponse deserializes HttpRoutes`` () =
    let result = JsonSerializer.Deserialize<InfoResponse>(infoJson, jsonOptions)
    Assert.Equal(2, result.HttpRoutes.Length)
    Assert.Equal("GET", result.HttpRoutes.[0].Verb)
    Assert.Equal("/info", result.HttpRoutes.[0].Path)
    Assert.Equal("Get information about available commands.", result.HttpRoutes.[0].Description)

// ── ListResponse deserialization (root, no endpoints) ──

let listRootJson = """
{
  "Result": "Success",
  "NodePath": "Root",
  "NodeName": "Root",
  "Nodes": [
    {
      "NodePath": "Root/VirtualRailDriver",
      "NodeName": "VirtualRailDriver"
    },
    {
      "NodePath": "Root/Player",
      "NodeName": "Player",
      "Nodes": [
        {
          "NodePath": "Root/Player/TransformComponent0",
          "NodeName": "TransformComponent0"
        }
      ]
    }
  ]
}"""

[<Fact>]
let ``ListResponse deserializes root node`` () =
    let result = JsonSerializer.Deserialize<ListResponse>(listRootJson, jsonOptions)
    Assert.Equal("Success", result.Result)
    Assert.Equal("Root", result.NodePath)
    Assert.Equal("Root", result.NodeName)

[<Fact>]
let ``ListResponse deserializes top-level nodes`` () =
    let result = JsonSerializer.Deserialize<ListResponse>(listRootJson, jsonOptions)
    Assert.True(result.Nodes.IsSome)
    Assert.Equal(2, result.Nodes.Value.Length)
    Assert.Equal("Root/VirtualRailDriver", result.Nodes.Value.[0].NodePath)
    Assert.Equal("VirtualRailDriver", result.Nodes.Value.[0].NodeName)

[<Fact>]
let ``ListResponse deserializes nested child nodes`` () =
    let result = JsonSerializer.Deserialize<ListResponse>(listRootJson, jsonOptions)
    let playerNode = result.Nodes.Value.[1]
    Assert.Equal("Player", playerNode.NodeName)
    Assert.True(playerNode.Nodes.IsSome)
    Assert.Equal(1, playerNode.Nodes.Value.Length)
    Assert.Equal("TransformComponent0", playerNode.Nodes.Value.[0].NodeName)

[<Fact>]
let ``ListResponse Endpoints is None when absent`` () =
    let result = JsonSerializer.Deserialize<ListResponse>(listRootJson, jsonOptions)
    Assert.True(result.Endpoints.IsNone)

// ── ListResponse with Endpoints ──

let listWithEndpointsJson = """
{
  "Result": "Success",
  "NodePath": "CurrentDrivableActor/AWS_TPWS_Service",
  "NodeName": "AWS_TPWS_Service",
  "Nodes": [],
  "Endpoints": [
    {
      "Name": "Property.bIsAWS_CutIn",
      "Writable": false
    },
    {
      "Name": "Property.AWS_SunflowerState",
      "Writable": false
    }
  ]
}"""

[<Fact>]
let ``ListResponse deserializes endpoints`` () =
    let result = JsonSerializer.Deserialize<ListResponse>(listWithEndpointsJson, jsonOptions)
    Assert.True(result.Endpoints.IsSome)
    Assert.Equal(2, result.Endpoints.Value.Length)
    Assert.Equal("Property.bIsAWS_CutIn", result.Endpoints.Value.[0].Name)
    Assert.False(result.Endpoints.Value.[0].Writable)

[<Fact>]
let ``ListResponse nodes can be empty list`` () =
    let result = JsonSerializer.Deserialize<ListResponse>(listWithEndpointsJson, jsonOptions)
    Assert.True(result.Nodes.IsSome)
    Assert.Empty(result.Nodes.Value)

// ── GetResponse deserialization ──

let getResponseJson = """
{
  "Result": "Success",
  "Values": {
    "Value": 1
  }
}"""

[<Fact>]
let ``GetResponse deserializes result`` () =
    let result = JsonSerializer.Deserialize<GetResponse>(getResponseJson, jsonOptions)
    Assert.Equal("Success", result.Result)

[<Fact>]
let ``GetResponse deserializes values map`` () =
    let result = JsonSerializer.Deserialize<GetResponse>(getResponseJson, jsonOptions)
    Assert.True(result.Values.ContainsKey("Value"))

// ── Node without children ──

let leafNodeJson = """
{
  "NodePath": "Root/VirtualRailDriver",
  "NodeName": "VirtualRailDriver"
}"""

[<Fact>]
let ``Node without children has Nodes as None`` () =
    let result = JsonSerializer.Deserialize<Node>(leafNodeJson, jsonOptions)
    Assert.Equal("Root/VirtualRailDriver", result.NodePath)
    Assert.Equal("VirtualRailDriver", result.NodeName)
    Assert.True(result.Nodes.IsNone)

// ── Endpoint deserialization ──

[<Fact>]
let ``Endpoint deserializes writable flag`` () =
    let json = """{"Name": "Property.Speed", "Writable": true}"""
    let result = JsonSerializer.Deserialize<Endpoint>(json, jsonOptions)
    Assert.Equal("Property.Speed", result.Name)
    Assert.True(result.Writable)

// ── Round-trip: ApiResult type alias ──

[<Fact>]
let ``ApiResult Ok carries value`` () =
    let result: ApiResult<string> = Ok "hello"
    match result with
    | Ok v -> Assert.Equal("hello", v)
    | Error _ -> Assert.Fail("Expected Ok")

[<Fact>]
let ``ApiResult Error carries ApiError`` () =
    let result: ApiResult<string> = Error (AuthError "bad key")
    match result with
    | Error (AuthError msg) -> Assert.Equal("bad key", msg)
    | _ -> Assert.Fail("Expected Error AuthError")
