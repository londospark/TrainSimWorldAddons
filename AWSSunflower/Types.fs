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

