namespace TSWApi

open System.Net.Http
open TSWApi.Types
open TSWApi.Http

/// Core API client operations for TSW6 GET endpoints.
module ApiClient =

    /// Get API information and available routes.
    let getInfo (client: HttpClient) (config: ApiConfig) : Async<ApiResult<InfoResponse>> =
        sendRequest<InfoResponse> client config "/info"

    /// List nodes at the given path, or root if None.
    let listNodes (client: HttpClient) (config: ApiConfig) (path: string option) : Async<ApiResult<ListResponse>> =
        let endpoint = path |> Option.map (sprintf "/list/%s") |> Option.defaultValue "/list"
        sendRequest<ListResponse> client config endpoint

    /// Get the value at the given endpoint path.
    let getValue (client: HttpClient) (config: ApiConfig) (path: string) : Async<ApiResult<GetResponse>> =
        sendRequest<GetResponse> client config $"/get/{path}"
