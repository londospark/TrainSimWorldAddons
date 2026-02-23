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
    /// <returns>Ok with the key string, or Error with AuthError details.</returns>
    let discoverCommKey (myGamesPath: string) : Result<string, ApiError> =
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
                    Ok key
                else
                    Error(AuthError $"CommAPIKey.txt not found at {keyPath}")
        with ex ->
            Error(AuthError $"Failed to discover CommKey: {ex.Message}")

    /// <summary>Create an API config with the default base URL (http://localhost:31270).</summary>
    /// <param name="commKey">The DTGCommKey authentication token.</param>
    let createConfig (commKey: string) : ApiConfig =
        { BaseUrl = "http://localhost:31270"
          CommKey = commKey }

    /// <summary>Create an API config with a custom base URL for network access.</summary>
    /// <param name="baseUrl">The base URL (e.g., "http://192.168.1.50:31270").</param>
    /// <param name="commKey">The DTGCommKey authentication token.</param>
    let createConfigWithUrl (baseUrl: string) (commKey: string) : ApiConfig =
        { BaseUrl = baseUrl
          CommKey = commKey }

    /// <summary>
    /// Send an authenticated GET request to the TSW API and deserialize the JSON response.
    /// Adds the DTGCommKey header automatically from the config.
    /// </summary>
    /// <param name="client">The HttpClient instance to use.</param>
    /// <param name="config">API configuration with base URL and auth key.</param>
    /// <param name="path">The API path (e.g., "/info", "/list/SomePath").</param>
    /// <returns>Async ApiResult with the deserialized response or an error.</returns>
    let sendRequest<'T> (client: HttpClient) (config: ApiConfig) (path: string) : Async<ApiResult<'T>> =
        async {
            try
                let url = $"{config.BaseUrl}{path}"
                use request = new HttpRequestMessage(HttpMethod.Get, url)
                request.Headers.Add("DTGCommKey", config.CommKey)

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
