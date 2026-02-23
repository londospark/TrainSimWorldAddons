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
        match result with
        | Ok key -> Assert.Equal("test-comm-key-123", CommKey.value key)
        | Error e -> Assert.Fail($"Expected Ok, got Error: {e}")
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
        match result with
        | Ok key -> Assert.Equal("new-key", CommKey.value key)
        | Error e -> Assert.Fail($"Expected Ok, got Error: {e}")
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
        match result with
        | Ok key -> Assert.Equal("key-with-spaces", CommKey.value key)
        | Error e -> Assert.Fail($"Expected Ok, got Error: {e}")
    finally
        Directory.Delete(tempBase, true)

// ── createConfig ──

[<Fact>]
let ``createConfig uses default base URL`` () =
    let result = createConfig "my-key"
    match result with
    | Ok config ->
        Assert.Equal("http://localhost:31270", BaseUrl.value config.BaseUrl)
        Assert.Equal("my-key", CommKey.value config.CommKey)
    | Error e -> Assert.Fail($"Expected Ok, got Error: {e}")

[<Fact>]
let ``createConfig rejects empty commKey`` () =
    let result = createConfig ""
    match result with
    | Error (AuthError _) -> ()
    | other -> Assert.Fail($"Expected AuthError, got {other}")

[<Fact>]
let ``createConfigWithUrl sets custom base URL`` () =
    let result = createConfigWithUrl "http://192.168.1.50:31270" "my-key"
    match result with
    | Ok config -> Assert.Equal("http://192.168.1.50:31270", BaseUrl.value config.BaseUrl)
    | Error e -> Assert.Fail($"Expected Ok, got Error: {e}")

[<Fact>]
let ``createConfigWithUrl rejects invalid URL`` () =
    let result = createConfigWithUrl "ftp://invalid" "my-key"
    match result with
    | Error (ConfigError _) -> ()
    | other -> Assert.Fail($"Expected ConfigError, got {other}")

[<Fact>]
let ``createConfigWithUrl rejects empty commKey`` () =
    let result = createConfigWithUrl "http://localhost:31270" ""
    match result with
    | Error (AuthError _) -> ()
    | other -> Assert.Fail($"Expected AuthError, got {other}")

// ── sendRequest ──

[<Fact>]
let ``sendRequest adds DTGCommKey header`` () = async {
    let handler = createMockHandler HttpStatusCode.OK """{"Result":"Success"}"""
    let client = new HttpClient(handler)
    match CommKey.create "secret-key" with
    | Ok key ->
        let config = { BaseUrl = BaseUrl.defaultUrl; CommKey = key }
        let! _ = sendRequest<{| Result: string |}> client config "/info"
        let req = handler.LastRequest.Value
        Assert.True(req.Headers.Contains("DTGCommKey"))
        let values = req.Headers.GetValues("DTGCommKey") |> Seq.toList
        Assert.Equal<string list>(["secret-key"], values)
    | Error e -> Assert.Fail($"Failed to create test config: {e}")
}

[<Fact>]
let ``sendRequest constructs correct URL`` () = async {
    let handler = createMockHandler HttpStatusCode.OK """{"Result":"Success"}"""
    let client = new HttpClient(handler)
    match CommKey.create "key" with
    | Ok key ->
        let config = { BaseUrl = BaseUrl.defaultUrl; CommKey = key }
        let! _ = sendRequest<{| Result: string |}> client config "/list/SomePath"
        let req = handler.LastRequest.Value
        Assert.Equal("http://localhost:31270/list/SomePath", req.RequestUri.ToString())
    | Error e -> Assert.Fail($"Failed to create test config: {e}")
}

[<Fact>]
let ``sendRequest returns Ok on successful response`` () = async {
    let json = """{"Result":"Success","Values":{"Value":42}}"""
    let handler = createMockHandler HttpStatusCode.OK json
    let client = new HttpClient(handler)
    match CommKey.create "key" with
    | Ok key ->
        let config = { BaseUrl = BaseUrl.defaultUrl; CommKey = key }
        let! result = sendRequest<{| Result: string |}> client config "/get/test"
        match result with
        | Ok r -> Assert.Equal("Success", r.Result)
        | Error e -> Assert.Fail($"Expected Ok, got Error: {e}")
    | Error e -> Assert.Fail($"Failed to create test config: {e}")
}

[<Fact>]
let ``sendRequest returns HttpError on non-success status`` () = async {
    let handler = createMockHandler HttpStatusCode.Forbidden "Forbidden"
    let client = new HttpClient(handler)
    match CommKey.create "bad-key" with
    | Ok key ->
        let config = { BaseUrl = BaseUrl.defaultUrl; CommKey = key }
        let! result = sendRequest<{| Result: string |}> client config "/info"
        match result with
        | Error (HttpError(403, _)) -> ()
        | other -> Assert.Fail($"Expected HttpError 403, got {other}")
    | Error e -> Assert.Fail($"Failed to create test config: {e}")
}

[<Fact>]
let ``sendRequest returns ParseError on invalid JSON`` () = async {
    let handler = createMockHandler HttpStatusCode.OK "not valid json"
    let client = new HttpClient(handler)
    match CommKey.create "key" with
    | Ok key ->
        let config = { BaseUrl = BaseUrl.defaultUrl; CommKey = key }
        let! result = sendRequest<{| Result: string |}> client config "/info"
        match result with
        | Error (ParseError _) -> ()
        | other -> Assert.Fail($"Expected ParseError, got {other}")
    | Error e -> Assert.Fail($"Failed to create test config: {e}")
}
