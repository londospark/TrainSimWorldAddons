namespace TSWApi

open System
open System.IO
open System.Net.Http
open System.Text.Json
open System.Text.RegularExpressions
open TSWApi.Types

/// HTTP client infrastructure for the TSW6 API.
module Http =

    let private jsonOptions =
        JsonSerializerOptions(PropertyNameCaseInsensitive = true)

    /// <summary>
    /// Discover the DTGCommKey from the My Games folder.
    /// Scans for TrainSimWorld* directories and picks the highest-numbered one,
    /// then reads the CommAPIKey.txt file.
    /// </summary>
    /// <param name="myGamesPath">Path to the user's "My Games" directory.</param>
    /// <returns>Ok with the validated CommKey, or Error with AuthError details.</returns>
    let discoverCommKey (myGamesPath: string) : Result<CommKey, ApiError> =
        try
            let dirs =
                if Directory.Exists(myGamesPath) then
                    Directory.GetDirectories(myGamesPath, "TrainSimWorld*")
                else
                    [||]

            if dirs.Length = 0 then
                Error(AuthError "No TrainSimWorld directory found")
            else
                // Pick the highest-numbered directory
                let sorted =
                    dirs
                    |> Array.sortByDescending (fun d ->
                        let name = Path.GetFileName(d)
                        let m = Regex.Match(name, @"TrainSimWorld(\d*)")
                        if m.Success && m.Groups.[1].Value <> "" then
                            int m.Groups.[1].Value
                        else
                            0)

                let keyPath = Path.Combine(sorted.[0], "Saved", "Config", "CommAPIKey.txt")

                if File.Exists(keyPath) then
                    let key = File.ReadAllText(keyPath).Trim()
                    CommKey.create key
                else
                    Error(AuthError $"CommAPIKey.txt not found at {keyPath}")
        with ex ->
            Error(AuthError $"Failed to discover CommKey: {ex.Message}")

    /// <summary>Create an API config with the default base URL (http://localhost:31270).</summary>
    /// <param name="commKey">The DTGCommKey authentication token.</param>
    let createConfig (commKey: string) : Result<ApiConfig, ApiError> =
        CommKey.create commKey
        |> Result.map (fun key -> { BaseUrl = BaseUrl.defaultUrl; CommKey = key })

    /// <summary>Create an API config with a custom base URL for network access.</summary>
    /// <param name="baseUrl">The base URL (e.g., "http://192.168.1.50:31270").</param>
    /// <param name="commKey">The DTGCommKey authentication token.</param>
    let createConfigWithUrl (baseUrl: string) (commKey: string) : Result<ApiConfig, ApiError> =
        match BaseUrl.create baseUrl, CommKey.create commKey with
        | Ok url, Ok key -> Ok { BaseUrl = url; CommKey = key }
        | Error e, _     -> Error e
        | _, Error e     -> Error e

    /// <summary>
    /// Send an authenticated HTTP request to the TSW API with a specified HTTP method and optional body.
    /// Adds the DTGCommKey header automatically from the config.
    /// </summary>
    /// <param name="client">The HttpClient instance to use.</param>
    /// <param name="config">API configuration with base URL and auth key.</param>
    /// <param name="httpMethod">The HTTP method to use (GET, POST, PATCH, DELETE, etc.).</param>
    /// <param name="path">The API path (e.g., "/info", "/list/SomePath").</param>
    /// <param name="body">Optional request body string (typically JSON).</param>
    /// <returns>Async ApiResult with the deserialized response or an error.</returns>
    let sendRequestWithMethod<'T> (client: HttpClient) (config: ApiConfig) (httpMethod: HttpMethod) (path: string) (body: string option) : Async<ApiResult<'T>> =
        async {
            try
                let url = $"{BaseUrl.value config.BaseUrl}{path}"
                use request = new HttpRequestMessage(httpMethod, url)
                request.Version <- Version(1, 1)
                request.Headers.Add("DTGCommKey", CommKey.value config.CommKey)

                // Set body content if provided
                match body with
                | Some content ->
                    request.Content <- new StringContent(content, Text.Encoding.UTF8, "application/json")
                | None -> ()

                let! response =
                    client.SendAsync(request) |> Async.AwaitTask

                if not response.IsSuccessStatusCode then
                    let! body = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                    return Error(HttpError(int response.StatusCode, body))
                else
                    let! body = response.Content.ReadAsStringAsync() |> Async.AwaitTask

                    try
                        let result = JsonSerializer.Deserialize<'T>(body, jsonOptions)
                        return Ok result
                    with ex ->
                        return Error(ParseError $"Failed to parse response: {ex.Message}")
            with ex ->
                return Error(NetworkError ex)
        }

    /// <summary>
    /// Send an authenticated GET request to the TSW API and deserialize the JSON response.
    /// Adds the DTGCommKey header automatically from the config.
    /// </summary>
    /// <param name="client">The HttpClient instance to use.</param>
    /// <param name="config">API configuration with base URL and auth key.</param>
    /// <param name="path">The API path (e.g., "/info", "/list/SomePath").</param>
    /// <returns>Async ApiResult with the deserialized response or an error.</returns>
    let sendRequest<'T> (client: HttpClient) (config: ApiConfig) (path: string) : Async<ApiResult<'T>> =
        sendRequestWithMethod<'T> client config HttpMethod.Get path None

    /// <summary>Send a POST request with a JSON body.</summary>
    let sendPost<'T> (client: HttpClient) (config: ApiConfig) (path: string) (body: string) : Async<ApiResult<'T>> =
        sendRequestWithMethod<'T> client config HttpMethod.Post path (Some body)

    /// <summary>Send a PATCH request with a JSON body.</summary>
    let sendPatch<'T> (client: HttpClient) (config: ApiConfig) (path: string) (body: string) : Async<ApiResult<'T>> =
        sendRequestWithMethod<'T> client config (HttpMethod("PATCH")) path (Some body)

    /// <summary>Send a DELETE request without a body.</summary>
    let sendDelete<'T> (client: HttpClient) (config: ApiConfig) (path: string) : Async<ApiResult<'T>> =
        sendRequestWithMethod<'T> client config HttpMethod.Delete path None
