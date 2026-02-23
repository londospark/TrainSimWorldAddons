namespace TSWApi

open System.Collections.Generic
open System.Text.Json.Serialization

/// Core types for the Train Sim World 6 API.
/// Contains all request/response models and error types used throughout the library.
[<AutoOpen>]
module Types =

    /// Errors that can occur when interacting with the TSW API.
    /// <example>
    /// <code>
    /// match error with
    /// | NetworkError ex -> printfn "Network failure: %s" ex.Message
    /// | HttpError(status, msg) -> printfn "HTTP %d: %s" status msg
    /// | AuthError msg -> printfn "Auth: %s" msg
    /// | ParseError msg -> printfn "Parse: %s" msg
    /// </code>
    /// </example>
    type ApiError =
        /// A network-level error (connection refused, timeout, etc.)
        | NetworkError of exn
        /// An HTTP error response with status code and message body.
        | HttpError of status: int * message: string
        /// An authentication error (missing or invalid DTGCommKey).
        | AuthError of string
        /// A JSON deserialization error.
        | ParseError of string

    /// Result type alias for API operations. Wraps Result&lt;'T, ApiError&gt;.
    type ApiResult<'T> = Result<'T, ApiError>

    /// Configuration for connecting to the TSW API.
    type ApiConfig =
        { /// The base URL of the TSW API (default: http://localhost:31270).
          BaseUrl: string
          /// The DTGCommKey authentication token read from CommAPIKey.txt.
          CommKey: string }

    /// An HTTP route advertised by the /info endpoint.
    type HttpRoute =
        { /// The HTTP verb (GET, POST, PATCH, DELETE).
          Verb: string
          /// The route path (e.g., "/info", "/list", "/get").
          Path: string
          /// A human-readable description of the route.
          Description: string }

    /// Metadata about the running game instance, returned by /info.
    type ApiMeta =
        { /// The communication worker name (e.g., "DTGCommWorkerRC").
          Worker: string
          /// The game name (e.g., "Train Sim World 6®").
          GameName: string
          /// The game build number.
          GameBuildNumber: int
          /// The API version number.
          APIVersion: int
          /// A unique identifier for this game instance.
          GameInstanceID: string }

    /// Response from the /info endpoint containing game metadata and available routes.
    type InfoResponse =
        { /// Game instance metadata.
          Meta: ApiMeta
          /// List of available HTTP routes.
          HttpRoutes: HttpRoute list }

    /// An endpoint (leaf value) on a node in the TSW object tree.
    type Endpoint =
        { /// The endpoint name (e.g., "Property.AWS_SunflowerState").
          Name: string
          /// Whether this endpoint can be written to via PATCH /set.
          Writable: bool }

    /// A node in the TSW object tree. Nodes can contain child nodes and/or endpoints.
    type Node =
        { /// The full path to this node (e.g., "Root/Player/TransformComponent0").
          NodePath: string
          /// The display name of this node (e.g., "TransformComponent0").
          NodeName: string
          /// Optional child nodes. None if not present in the response.
          [<JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)>]
          Nodes: Node list option
          /// Optional endpoints on this node. None if not present in the response.
          [<JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)>]
          Endpoints: Endpoint list option }

    /// Response from the /list endpoint containing the node tree.
    type ListResponse =
        { /// The result status (e.g., "Success").
          Result: string
          /// The path of the listed node.
          NodePath: string
          /// The name of the listed node.
          NodeName: string
          /// Optional child nodes.
          [<JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)>]
          Nodes: Node list option
          /// Optional endpoints on this node.
          [<JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)>]
          Endpoints: Endpoint list option }

    /// Response from the /get endpoint containing endpoint values.
    type GetResponse =
        { /// The result status (e.g., "Success").
          Result: string
          /// Key-value pairs of endpoint values.
          Values: Dictionary<string, obj> }
