namespace CounterApp

open System

/// Result of a serial port operation
type SerialError =
    | PortInUse of portName: string
    | PortNotFound of portName: string
    | OpenFailed of message: string
    | SendFailed of message: string
    | Disconnected

/// Current connection state
[<RequireQualifiedAccess>]
type ConnectionState =
    | Disconnected
    | Connecting
    | Connected of portName: string
    | Error of SerialError

/// Toast notification data
type Toast =
    {
        Id: Guid
        Message: string
        IsError: bool
        CreatedAt: DateTime
    }

/// API connection state
[<RequireQualifiedAccess>]
type ApiConnectionState =
    | Disconnected
    | Connecting
    | Connected of info: TSWApi.Types.InfoResponse
    | Error of message: string

/// Tree node UI state (for TreeView)
type TreeNodeState =
    {
        Path: string
        Name: string
        IsExpanded: bool
        Children: TreeNodeState list option
        Endpoints: TSWApi.Types.Endpoint list option
    }

