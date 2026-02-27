namespace CounterApp

open System
open TSWApi
open TSWApi.Subscription
open CounterApp.CommandMapping
open CounterApp.PortDetection

module ApplicationScreen =

    // ─── Model ───

    type Model =
        { // Serial port
          DetectedPorts: DetectedPort list
          SerialConnectionState: ConnectionState
          SerialIsConnecting: bool
          // API Explorer
          BaseUrl: string
          CommKey: string
          ApiConfig: ApiConfig option
          ConnectionState: ApiConnectionState
          IsConnecting: bool
          TreeRoot: TreeNodeState list
          SelectedNode: TreeNodeState option
          EndpointValues: Map<string, string>
          LastResponseTime: TimeSpan option
          SearchQuery: string
          CurrentLoco: string option
          BindingsConfig: BindingsConfig
          PollingValues: Map<string, string>
          // Command mapping
          ActiveAddon: AddonCommandSet option
          // Shared serial port
          SerialPort: IO.Ports.SerialPort option
          SerialPortName: string option }

    // ─── Messages ───

    type Msg =
        | SetBaseUrl of string
        | SetCommKey of string
        | SetSearchQuery of string
        | Connect
        | Disconnect
        | ConnectSuccess of commKey: string * config: ApiConfig * info: InfoResponse * elapsed: TimeSpan
        | ConnectError of string
        | RootNodesLoaded of nodes: TreeNodeState list * elapsed: TimeSpan
        | RootNodesError of string
        | ExpandNode of path: string
        | NodeExpanded of path: string * children: TreeNodeState list * endpoints: Endpoint list option * elapsed: TimeSpan
        | CollapseNode of path: string
        | ToggleExpand of path: string
        | SelectNode of TreeNodeState
        | GetEndpointValue of path: string
        | EndpointValueReceived of path: string * value: string * elapsed: TimeSpan
        | ApiError of string
        // Binding
        | BindEndpoint of nodePath: string * endpointName: string
        | UnbindEndpoint of nodePath: string * endpointName: string
        // Loco detection
        | DetectLoco
        | LocoDetected of string
        | LocoDetectError of string
        // Subscription-based polling
        | EndpointChanged of ValueChange
        // Serial (API Explorer internal)
        | SetSerialPort of string option
        | DisconnectSerial
        // Serial tab (unified)
        | PortsUpdated of DetectedPort list
        | ToggleSerialConnection
        | SerialConnectResult of Result<IO.Ports.SerialPort, SerialError>
        | SendSerialCommand of string

    // ─── Init ───

    let init () =
        { DetectedPorts = []
          SerialConnectionState = ConnectionState.Disconnected
          SerialIsConnecting = false
          BaseUrl = "http://localhost:31270"
          CommKey = ""
          ApiConfig = None
          ConnectionState = ApiConnectionState.Disconnected
          IsConnecting = false
          TreeRoot = []
          SelectedNode = None
          EndpointValues = Map.empty
          LastResponseTime = None
          SearchQuery = ""
          CurrentLoco = None
          BindingsConfig = BindingPersistence.load ()
          PollingValues = Map.empty
          ActiveAddon = Some AWSSunflowerCommands.commandSet
          SerialPort = None
          SerialPortName = None }
