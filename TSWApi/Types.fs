namespace TSWApi

open System.Collections.Generic
open System.Text.Json.Serialization

/// Core types for the Train Sim World 6 API.
[<AutoOpen>]
module Types =

    /// Errors that can occur when interacting with the TSW API.
    type ApiError =
        | NetworkError of exn
        | HttpError of status: int * message: string
        | AuthError of string
        | ParseError of string

    /// Result type alias for API operations.
    type ApiResult<'T> = Result<'T, ApiError>

    /// Configuration for connecting to the TSW API.
    type ApiConfig =
        { BaseUrl: string
          CommKey: string }

    /// An HTTP route advertised by the API.
    type HttpRoute =
        { Verb: string
          Path: string
          Description: string }

    /// Metadata about the running game instance.
    type ApiMeta =
        { Worker: string
          GameName: string
          GameBuildNumber: int
          APIVersion: int
          GameInstanceID: string }

    /// Response from the /info endpoint.
    type InfoResponse =
        { Meta: ApiMeta
          HttpRoutes: HttpRoute list }

    /// An endpoint (leaf value) on a node.
    type Endpoint =
        { Name: string
          Writable: bool }

    /// A node in the TSW object tree.
    type Node =
        { NodePath: string
          NodeName: string
          [<JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)>]
          Nodes: Node list option
          [<JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)>]
          Endpoints: Endpoint list option }

    /// Response from the /list endpoint.
    type ListResponse =
        { Result: string
          NodePath: string
          NodeName: string
          [<JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)>]
          Nodes: Node list option
          [<JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)>]
          Endpoints: Endpoint list option }

    /// Response from the /get endpoint.
    type GetResponse =
        { Result: string
          Values: Dictionary<string, obj> }
