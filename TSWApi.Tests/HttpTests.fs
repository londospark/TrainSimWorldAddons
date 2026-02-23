module TSWApi.Tests.HttpTests

open System
open System.IO
open System.Net
open System.Net.Http
open System.Threading
open System.Threading.Tasks
open Xunit
open TSWApi.Types
open TSWApi.Http

// ── Helper: mock HttpMessageHandler ──

type MockHandler(response: HttpResponseMessage) =
    inherit HttpMessageHandler()
    member val LastRequest: HttpRequestMessage option = None with get, set
    override this.SendAsync(request, _cancellationToken) =
        this.LastRequest <- Some request
        Task.FromResult(response)

let createMockHandler (statusCode: HttpStatusCode) (content: string) =
    let response = new HttpResponseMessage(statusCode)
    response.Content <- new StringContent(content)
    new MockHandler(response)

// ── CommKey discovery ──

[<Fact>]
let ``discoverCommKey reads key from temp directory structure`` () =
    // Create a temp directory mimicking "My Games/TrainSimWorld6/Saved/Config/"
    let tempBase = Path.Combine(Path.GetTempPath(), $"TSWApiTest_{Guid.NewGuid()}")
    let tswDir = Path.Combine(tempBase, "TrainSimWorld6", "Saved", "Config")
    Directory.CreateDirectory(tswDir) |> ignore
    File.WriteAllText(Path.Combine(tswDir, "CommAPIKey.txt"), "test-comm-key-123")
    try
        let result = discoverCommKey tempBase
        Assert.Equal(Ok "test-comm-key-123", result)
    finally
        Directory.Delete(tempBase, true)

[<Fact>]
let ``discoverCommKey picks highest numbered TrainSimWorld directory`` () =
    let tempBase = Path.Combine(Path.GetTempPath(), $"TSWApiTest_{Guid.NewGuid()}")
    // Create TrainSimWorld3 and TrainSimWorld6 dirs
    let dir3 = Path.Combine(tempBase, "TrainSimWorld3", "Saved", "Config")
    let dir6 = Path.Combine(tempBase, "TrainSimWorld6", "Saved", "Config")
    Directory.CreateDirectory(dir3) |> ignore
    Directory.CreateDirectory(dir6) |> ignore
    File.WriteAllText(Path.Combine(dir3, "CommAPIKey.txt"), "old-key")
    File.WriteAllText(Path.Combine(dir6, "CommAPIKey.txt"), "new-key")
    try
        let result = discoverCommKey tempBase
        Assert.Equal(Ok "new-key", result)
    finally
        Directory.Delete(tempBase, true)

[<Fact>]
let ``discoverCommKey returns AuthError when no TrainSimWorld directory exists`` () =
    let tempBase = Path.Combine(Path.GetTempPath(), $"TSWApiTest_{Guid.NewGuid()}")
    Directory.CreateDirectory(tempBase) |> ignore
    try
        let result = discoverCommKey tempBase
        match result with
        | Error (AuthError _) -> ()
        | other -> Assert.Fail($"Expected AuthError, got {other}")
    finally
        Directory.Delete(tempBase, true)

[<Fact>]
let ``discoverCommKey returns AuthError when key file is missing`` () =
    let tempBase = Path.Combine(Path.GetTempPath(), $"TSWApiTest_{Guid.NewGuid()}")
    let tswDir = Path.Combine(tempBase, "TrainSimWorld6", "Saved", "Config")
    Directory.CreateDirectory(tswDir) |> ignore
    // No CommAPIKey.txt file created
    try
        let result = discoverCommKey tempBase
        match result with
        | Error (AuthError _) -> ()
        | other -> Assert.Fail($"Expected AuthError, got {other}")
    finally
        Directory.Delete(tempBase, true)

[<Fact>]
let ``discoverCommKey trims whitespace from key`` () =
    let tempBase = Path.Combine(Path.GetTempPath(), $"TSWApiTest_{Guid.NewGuid()}")
    let tswDir = Path.Combine(tempBase, "TrainSimWorld6", "Saved", "Config")
    Directory.CreateDirectory(tswDir) |> ignore
    File.WriteAllText(Path.Combine(tswDir, "CommAPIKey.txt"), "  key-with-spaces  \r\n")
    try
        let result = discoverCommKey tempBase
        Assert.Equal(Ok "key-with-spaces", result)
    finally
        Directory.Delete(tempBase, true)

// ── createConfig ──

[<Fact>]
let ``createConfig uses default base URL`` () =
    let config = createConfig "my-key"
    Assert.Equal("http://localhost:31270", config.BaseUrl)
    Assert.Equal("my-key", config.CommKey)

[<Fact>]
let ``createConfigWithUrl sets custom base URL`` () =
    let config = createConfigWithUrl "http://192.168.1.50:31270" "my-key"
    Assert.Equal("http://192.168.1.50:31270", config.BaseUrl)

// ── sendRequest ──

[<Fact>]
let ``sendRequest adds DTGCommKey header`` () = async {
    let handler = createMockHandler HttpStatusCode.OK """{"Result":"Success"}"""
    let client = new HttpClient(handler)
    let config = { BaseUrl = "http://localhost:31270"; CommKey = "secret-key" }

    let! _ = sendRequest<{| Result: string |}> client config "/info"
    let req = handler.LastRequest.Value
    Assert.True(req.Headers.Contains("DTGCommKey"))
    let values = req.Headers.GetValues("DTGCommKey") |> Seq.toList
    Assert.Equal<string list>(["secret-key"], values)
}

[<Fact>]
let ``sendRequest constructs correct URL`` () = async {
    let handler = createMockHandler HttpStatusCode.OK """{"Result":"Success"}"""
    let client = new HttpClient(handler)
    let config = { BaseUrl = "http://localhost:31270"; CommKey = "key" }

    let! _ = sendRequest<{| Result: string |}> client config "/list/SomePath"
    let req = handler.LastRequest.Value
    Assert.Equal("http://localhost:31270/list/SomePath", req.RequestUri.ToString())
}

[<Fact>]
let ``sendRequest returns Ok on successful response`` () = async {
    let json = """{"Result":"Success","Values":{"Value":42}}"""
    let handler = createMockHandler HttpStatusCode.OK json
    let client = new HttpClient(handler)
    let config = { BaseUrl = "http://localhost:31270"; CommKey = "key" }

    let! result = sendRequest<{| Result: string |}> client config "/get/test"
    match result with
    | Ok r -> Assert.Equal("Success", r.Result)
    | Error e -> Assert.Fail($"Expected Ok, got Error: {e}")
}

[<Fact>]
let ``sendRequest returns HttpError on non-success status`` () = async {
    let handler = createMockHandler HttpStatusCode.Forbidden "Forbidden"
    let client = new HttpClient(handler)
    let config = { BaseUrl = "http://localhost:31270"; CommKey = "bad-key" }

    let! result = sendRequest<{| Result: string |}> client config "/info"
    match result with
    | Error (HttpError(403, _)) -> ()
    | other -> Assert.Fail($"Expected HttpError 403, got {other}")
}

[<Fact>]
let ``sendRequest returns ParseError on invalid JSON`` () = async {
    let handler = createMockHandler HttpStatusCode.OK "not valid json"
    let client = new HttpClient(handler)
    let config = { BaseUrl = "http://localhost:31270"; CommKey = "key" }

    let! result = sendRequest<{| Result: string |}> client config "/info"
    match result with
    | Error (ParseError _) -> ()
    | other -> Assert.Fail($"Expected ParseError, got {other}")
}
