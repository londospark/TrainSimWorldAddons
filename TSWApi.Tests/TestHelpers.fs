module TSWApi.Tests.TestHelpers

open System.Net
open System.Net.Http
open System.Threading
open System.Threading.Tasks
open TSWApi.Types

/// Full-featured mock handler with request capture
type MockHandler(response: HttpResponseMessage) =
    inherit HttpMessageHandler()
    let mutable lastRequest: HttpRequestMessage option = None

    override _.SendAsync(request, _ct) =
        lastRequest <- Some request
        Task.FromResult(response)

    member _.LastRequest = lastRequest

/// Sequential/dynamic response handler
type CallbackMockHandler(responseProvider: int -> HttpResponseMessage) =
    inherit HttpMessageHandler()
    let mutable callCount = 0

    override _.SendAsync(_request, _ct) =
        let idx = Interlocked.Increment(&callCount)
        Task.FromResult(responseProvider idx)

    member _.CallCount = callCount

/// Create a response with status code and content
let makeResponse (statusCode: HttpStatusCode) (content: string) =
    let resp = new HttpResponseMessage(statusCode)
    resp.Content <- new StringContent(content)
    resp

/// Create a mock client that always returns the same response
let mockClient (statusCode: HttpStatusCode) (content: string) =
    new HttpClient(new MockHandler(makeResponse statusCode content))

/// Create a mock handler that captures requests
let capturingHandler (statusCode: HttpStatusCode) (content: string) =
    let handler = new MockHandler(makeResponse statusCode content)
    handler, new HttpClient(handler)

/// JSON helpers
let valueJson (v: string) =
    sprintf """{ "Result": "Success", "Values": { "Value": %s } }""" v

let constantClient (json: string) =
    new HttpClient(new CallbackMockHandler(fun _ -> makeResponse HttpStatusCode.OK json))

let sequentialClient (jsons: string list) =
    new HttpClient(
        new CallbackMockHandler(fun idx ->
            let i = min (idx - 1) (jsons.Length - 1)
            makeResponse HttpStatusCode.OK jsons[i]))

let errorClient () =
    new HttpClient(
        new CallbackMockHandler(fun _ -> makeResponse HttpStatusCode.InternalServerError "Server Error"))

/// Shared test ApiConfig
let testConfig =
    match CommKey.create "test-key" with
    | Ok key -> { BaseUrl = BaseUrl.defaultUrl; CommKey = key }
    | Error e -> failwith $"Test setup failed: {e}"

/// Shared JSON test data
module TestJson =
    let info = """{
  "Meta": {
    "Worker": "DTGCommWorkerRC",
    "GameName": "Train Sim World 6®",
    "GameBuildNumber": 749,
    "APIVersion": 1,
    "GameInstanceID": "A69D53564DFE46B7DE5AD7885CF0AA82"
  },
  "HttpRoutes": [
    { "Verb": "GET", "Path": "/info", "Description": "Get information about available commands." },
    { "Verb": "GET", "Path": "/list", "Description": "List all valid paths for commands." }
  ]
}"""

    let listWithEndpoints = """{
  "Result": "Success",
  "NodePath": "CurrentDrivableActor/AWS_TPWS_Service",
  "NodeName": "AWS_TPWS_Service",
  "Nodes": [],
  "Endpoints": [
    { "Name": "Property.bIsAWS_CutIn", "Writable": false },
    { "Name": "Property.AWS_SunflowerState", "Writable": false }
  ]
}"""

    let getResponse = """{
  "Result": "Success",
  "Values": { "Value": 1 }
}"""
