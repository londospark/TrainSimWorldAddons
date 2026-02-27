module TSWApi.Tests.ApiClientTests

open System.Net
open System.Net.Http
open Xunit
open TSWApi.Types
open TSWApi.ApiClient
open TSWApi.Tests.TestHelpers

// ── getInfo ──

[<Fact>]
let ``getInfo returns InfoResponse on success`` () = async {
    let client = mockClient HttpStatusCode.OK TestJson.info
    let! result = getInfo client testConfig
    match result with
    | Ok info ->
        Assert.Equal("DTGCommWorkerRC", info.Meta.Worker)
        Assert.Equal("Train Sim World 6®", info.Meta.GameName)
        Assert.Equal(749, info.Meta.GameBuildNumber)
        Assert.Equal(2, info.HttpRoutes.Length)
    | Error e -> Assert.Fail($"Expected Ok, got Error: {e}")
}

[<Fact>]
let ``getInfo returns HttpError on failure`` () = async {
    let client = mockClient HttpStatusCode.InternalServerError "Server Error"
    let! result = getInfo client testConfig
    match result with
    | Error (HttpError(500, _)) -> ()
    | other -> Assert.Fail($"Expected HttpError 500, got {other}")
}

// ── listNodes ──

let listRootJson = """{
  "Result": "Success",
  "NodePath": "Root",
  "NodeName": "Root",
  "Nodes": [
    { "NodePath": "Root/VirtualRailDriver", "NodeName": "VirtualRailDriver" },
    { "NodePath": "Root/Player", "NodeName": "Player" }
  ]
}"""

[<Fact>]
let ``listNodes returns root nodes when no path specified`` () = async {
    let client = mockClient HttpStatusCode.OK listRootJson
    let! result = listNodes client testConfig None
    match result with
    | Ok list ->
        Assert.Equal("Success", list.Result)
        Assert.Equal("Root", list.NodeName)
        Assert.True(list.Nodes.IsSome)
        Assert.Equal(2, list.Nodes.Value.Length)
    | Error e -> Assert.Fail($"Expected Ok, got Error: {e}")
}

let listWithEndpointsJson = TestJson.listWithEndpoints

[<Fact>]
let ``listNodes returns endpoints when path specified`` () = async {
    let client = mockClient HttpStatusCode.OK listWithEndpointsJson
    let! result = listNodes client testConfig (Some "CurrentDrivableActor/AWS_TPWS_Service")
    match result with
    | Ok list ->
        Assert.Equal("AWS_TPWS_Service", list.NodeName)
        Assert.True(list.Endpoints.IsSome)
        Assert.Equal(2, list.Endpoints.Value.Length)
        Assert.Equal("Property.bIsAWS_CutIn", list.Endpoints.Value.[0].Name)
    | Error e -> Assert.Fail($"Expected Ok, got Error: {e}")
}

// ── getValue ──

let getJson = TestJson.getResponse

[<Fact>]
let ``getValue returns GetResponse`` () = async {
    let client = mockClient HttpStatusCode.OK getJson
    let! result = getValue client testConfig "CurrentDrivableActor/AWS_TPWS_Service.Property.AWS_SunflowerState"
    match result with
    | Ok resp ->
        Assert.Equal("Success", resp.Result)
        Assert.True(resp.Values.ContainsKey("Value"))
    | Error e -> Assert.Fail($"Expected Ok, got Error: {e}")
}

[<Fact>]
let ``getValue returns HttpError on auth failure`` () = async {
    let client = mockClient HttpStatusCode.Unauthorized "Unauthorized"
    let! result = getValue client testConfig "some/path"
    match result with
    | Error (HttpError(401, _)) -> ()
    | other -> Assert.Fail($"Expected HttpError 401, got {other}")
}
