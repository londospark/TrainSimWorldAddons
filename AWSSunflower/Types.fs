namespace CounterApp

open System

/// Result of a serial port operation
[<RequireQualifiedAccess>]
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

/// A bound endpoint for polling
type BoundEndpoint = {
    /// Relative path from CurrentDrivableActor (e.g., "BP_AWS_TPWS_Service")
    NodePath: string
    /// Endpoint name (e.g., "Property.AWS_SunflowerState")  
    EndpointName: string
    /// User-friendly label
    Label: string
}

/// Configuration for a specific locomotive
type LocoConfig = {
    /// Object name from the API (e.g., "RVM_LNWR_Class350-2_DMS1_C_2147475158")
    LocoName: string
    /// Bound endpoints for this loco
    BoundEndpoints: BoundEndpoint list
}

/// Persisted bindings configuration
type BindingsConfig = {
    /// Schema version for forward compatibility
    Version: int
    /// Per-loco configurations
    Locos: LocoConfig list
}

