module TSWApi.Tests.HttpTests

open System
open System.IO
open System.Net
open System.Net.Http
open System.Threading.Tasks
open Xunit
open TSWApi.Types
open TSWApi.Http

// ── Helper: mock HttpMessageHandler ──

type MockHandler(response: HttpResponseMessage) =
    inherit HttpMessageHandler()
    member val LastRequest: HttpRequestMessage option = None with get, set
    member val LastRequestBody: string option = None with get, set
    member val LastRequestMethod: HttpMethod option = None with get, set
    member val LastContentType: string option = None with get, set
    override this.SendAsync(request, _cancellationToken) = task {
        this.LastRequest <- Some request
        this.LastRequestMethod <- Some request.Method
        // Capture body and content-type before request is disposed
        match request.Content with
        | null -> 
            this.LastRequestBody <- None
            this.LastContentType <- None
        | content -> 
            let! body = content.ReadAsStringAsync()
            this.LastRequestBody <- if System.String.IsNullOrEmpty(body) then None else Some body
            this.LastContentType <- 
                if content.Headers.ContentType <> null then
                    Some content.Headers.ContentType.MediaType
                else
                    None
        return response
    }

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

// ── sendRequestWithMethod tests ──

[<Fact>]
let ``sendRequestWithMethod with GET and no body works same as sendRequest`` () = async {
    let json = """{"Result":"Success"}"""
    let handler = createMockHandler HttpStatusCode.OK json
    let client = new HttpClient(handler)
    match CommKey.create "key" with
    | Ok key ->
        let config = { BaseUrl = BaseUrl.defaultUrl; CommKey = key }
        let! result = sendRequestWithMethod<{| Result: string |}> client config HttpMethod.Get "/info" None
        match result with
        | Ok r -> Assert.Equal("Success", r.Result)
        | Error e -> Assert.Fail($"Expected Ok, got Error: {e}")
        
        // Verify request method
        Assert.Equal(Some HttpMethod.Get, handler.LastRequestMethod)
        
        // Verify no body
        Assert.True(handler.LastRequestBody.IsNone)
    | Error e -> Assert.Fail($"Failed to create test config: {e}")
}

[<Fact>]
let ``sendRequestWithMethod POST sends with body and application/json content-type`` () = async {
    let json = """{"Result":"Created"}"""
    let handler = createMockHandler HttpStatusCode.Created json
    let client = new HttpClient(handler)
    match CommKey.create "key" with
    | Ok key ->
        let config = { BaseUrl = BaseUrl.defaultUrl; CommKey = key }
        let requestBody = """{"name":"test"}"""
        let! result = sendRequestWithMethod<{| Result: string |}> client config HttpMethod.Post "/subscribe" (Some requestBody)
        match result with
        | Ok r -> Assert.Equal("Created", r.Result)
        | Error e -> Assert.Fail($"Expected Ok, got Error: {e}")
        
        // Verify request method
        Assert.Equal(Some HttpMethod.Post, handler.LastRequestMethod)
        
        // Verify body content
        Assert.Equal(Some requestBody, handler.LastRequestBody)
        
        // Verify content-type
        Assert.Equal(Some "application/json", handler.LastContentType)
    | Error e -> Assert.Fail($"Failed to create test config: {e}")
}

[<Fact>]
let ``sendRequestWithMethod PATCH sends with body and application/json content-type`` () = async {
    let json = """{"Result":"Updated"}"""
    let handler = createMockHandler HttpStatusCode.OK json
    let client = new HttpClient(handler)
    match CommKey.create "key" with
    | Ok key ->
        let config = { BaseUrl = BaseUrl.defaultUrl; CommKey = key }
        let requestBody = """{"value":"42"}"""
        let! result = sendRequestWithMethod<{| Result: string |}> client config (HttpMethod("PATCH")) "/set/test" (Some requestBody)
        match result with
        | Ok r -> Assert.Equal("Updated", r.Result)
        | Error e -> Assert.Fail($"Expected Ok, got Error: {e}")
        
        // Verify request method
        Assert.Equal(Some (HttpMethod("PATCH")), handler.LastRequestMethod)
        
        // Verify body content
        Assert.Equal(Some requestBody, handler.LastRequestBody)
        
        // Verify content-type
        Assert.Equal(Some "application/json", handler.LastContentType)
    | Error e -> Assert.Fail($"Failed to create test config: {e}")
}

[<Fact>]
let ``sendRequestWithMethod DELETE sends without body`` () = async {
    let json = """{"Result":"Deleted"}"""
    let handler = createMockHandler HttpStatusCode.OK json
    let client = new HttpClient(handler)
    match CommKey.create "key" with
    | Ok key ->
        let config = { BaseUrl = BaseUrl.defaultUrl; CommKey = key }
        let! result = sendRequestWithMethod<{| Result: string |}> client config HttpMethod.Delete "/subscribe/123" None
        match result with
        | Ok r -> Assert.Equal("Deleted", r.Result)
        | Error e -> Assert.Fail($"Expected Ok, got Error: {e}")
        
        // Verify request method
        Assert.Equal(Some HttpMethod.Delete, handler.LastRequestMethod)
        
        // Verify no body
        Assert.True(handler.LastRequestBody.IsNone)
    | Error e -> Assert.Fail($"Failed to create test config: {e}")
}

[<Fact>]
let ``sendRequestWithMethod includes DTGCommKey header for all methods`` () = async {
    let json = """{"Result":"Success"}"""
    let handler = createMockHandler HttpStatusCode.OK json
    let client = new HttpClient(handler)
    match CommKey.create "test-key-456" with
    | Ok key ->
        let config = { BaseUrl = BaseUrl.defaultUrl; CommKey = key }
        
        // Test POST
        let! _ = sendRequestWithMethod<{| Result: string |}> client config HttpMethod.Post "/test" (Some "{}")
        let req = handler.LastRequest.Value
        Assert.True(req.Headers.Contains("DTGCommKey"))
        let values = req.Headers.GetValues("DTGCommKey") |> Seq.toList
        Assert.Equal<string list>(["test-key-456"], values)
    | Error e -> Assert.Fail($"Failed to create test config: {e}")
}

[<Fact>]
let ``sendRequestWithMethod enforces HTTP/1.1 for all methods`` () = async {
    let json = """{"Result":"Success"}"""
    let handler = createMockHandler HttpStatusCode.OK json
    let client = new HttpClient(handler)
    match CommKey.create "key" with
    | Ok key ->
        let config = { BaseUrl = BaseUrl.defaultUrl; CommKey = key }
        
        // Test POST
        let! _ = sendRequestWithMethod<{| Result: string |}> client config HttpMethod.Post "/test" (Some "{}")
        let req = handler.LastRequest.Value
        Assert.Equal(Version(1, 1), req.Version)
    | Error e -> Assert.Fail($"Failed to create test config: {e}")
}

[<Fact>]
let ``sendRequestWithMethod with body None does not set content`` () = async {
    let json = """{"Result":"Success"}"""
    let handler = createMockHandler HttpStatusCode.OK json
    let client = new HttpClient(handler)
    match CommKey.create "key" with
    | Ok key ->
        let config = { BaseUrl = BaseUrl.defaultUrl; CommKey = key }
        let! _ = sendRequestWithMethod<{| Result: string |}> client config HttpMethod.Get "/info" None
        
        // Verify no content
        Assert.True(handler.LastRequestBody.IsNone)
    | Error e -> Assert.Fail($"Failed to create test config: {e}")
}

// ── Convenience wrapper tests ──

[<Fact>]
let ``sendPost calls sendRequestWithMethod with POST and body`` () = async {
    let json = """{"Result":"Created"}"""
    let handler = createMockHandler HttpStatusCode.Created json
    let client = new HttpClient(handler)
    match CommKey.create "key" with
    | Ok key ->
        let config = { BaseUrl = BaseUrl.defaultUrl; CommKey = key }
        let requestBody = """{"data":"value"}"""
        let! result = sendPost<{| Result: string |}> client config "/endpoint" requestBody
        match result with
        | Ok r -> Assert.Equal("Created", r.Result)
        | Error e -> Assert.Fail($"Expected Ok, got Error: {e}")
        
        Assert.Equal(Some HttpMethod.Post, handler.LastRequestMethod)
        Assert.Equal(Some requestBody, handler.LastRequestBody)
    | Error e -> Assert.Fail($"Failed to create test config: {e}")
}

[<Fact>]
let ``sendPatch calls sendRequestWithMethod with PATCH and body`` () = async {
    let json = """{"Result":"Updated"}"""
    let handler = createMockHandler HttpStatusCode.OK json
    let client = new HttpClient(handler)
    match CommKey.create "key" with
    | Ok key ->
        let config = { BaseUrl = BaseUrl.defaultUrl; CommKey = key }
        let requestBody = """{"value":"new"}"""
        let! result = sendPatch<{| Result: string |}> client config "/endpoint" requestBody
        match result with
        | Ok r -> Assert.Equal("Updated", r.Result)
        | Error e -> Assert.Fail($"Expected Ok, got Error: {e}")
        
        Assert.Equal(Some (HttpMethod("PATCH")), handler.LastRequestMethod)
        Assert.Equal(Some requestBody, handler.LastRequestBody)
    | Error e -> Assert.Fail($"Failed to create test config: {e}")
}

[<Fact>]
let ``sendDelete calls sendRequestWithMethod with DELETE and no body`` () = async {
    let json = """{"Result":"Deleted"}"""
    let handler = createMockHandler HttpStatusCode.OK json
    let client = new HttpClient(handler)
    match CommKey.create "key" with
    | Ok key ->
        let config = { BaseUrl = BaseUrl.defaultUrl; CommKey = key }
        let! result = sendDelete<{| Result: string |}> client config "/endpoint/123"
        match result with
        | Ok r -> Assert.Equal("Deleted", r.Result)
        | Error e -> Assert.Fail($"Expected Ok, got Error: {e}")
        
        Assert.Equal(Some HttpMethod.Delete, handler.LastRequestMethod)
        Assert.True(handler.LastRequestBody.IsNone)
    | Error e -> Assert.Fail($"Failed to create test config: {e}")
}
