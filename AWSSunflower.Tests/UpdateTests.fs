module UpdateTests

open Xunit
open CounterApp
open CounterApp.ApplicationScreen
open CounterApp.ApplicationScreenUpdate
open CounterApp.ApplicationScreenHelpers
open CounterApp.CommandMapping
open global.Elmish

let testModel () =
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
      BindingsConfig = { Version = 1; Locos = [] }
      PollingValues = Map.empty
      IsSubscriptionActive = false
      ActiveAddon = Some AWSSunflowerCommands.commandSet
      SerialPort = None
      SerialPortName = None }

[<Fact>]
let ``SetBaseUrl updates model BaseUrl``() =
    let model = testModel ()
    let newModel, _ = update (SetBaseUrl "http://example.com") model
    Assert.Equal("http://example.com", newModel.BaseUrl)

[<Fact>]
let ``SetCommKey updates model CommKey``() =
    let model = testModel ()
    let newModel, _ = update (SetCommKey "abc123") model
    Assert.Equal("abc123", newModel.CommKey)

[<Fact>]
let ``SetSearchQuery updates model SearchQuery``() =
    let model = testModel ()
    let newModel, _ = update (SetSearchQuery "test") model
    Assert.Equal("test", newModel.SearchQuery)

[<Fact>]
let ``Connect sets IsConnecting true and ConnectionState to Connecting``() =
    let model = testModel ()
    let newModel, _ = update Connect model
    Assert.True(newModel.IsConnecting)
    match newModel.ConnectionState with
    | ApiConnectionState.Connecting -> Assert.True(true)
    | _ -> Assert.Fail("Expected Connecting state")

[<Fact>]
let ``ConnectError sets Error state and IsConnecting false``() =
    let model = { testModel () with IsConnecting = true; ConnectionState = ApiConnectionState.Connecting }
    let newModel, _ = update (ConnectError "timeout") model
    Assert.False(newModel.IsConnecting)
    match newModel.ConnectionState with
    | ApiConnectionState.Error msg -> Assert.Equal("timeout", msg)
    | _ -> Assert.Fail("Expected Error state")

[<Fact>]
let ``Disconnect clears ApiConfig ConnectionState TreeRoot and PollingValues``() =
    let model = 
        { testModel () with 
            ConnectionState = ApiConnectionState.Connected { Meta = { Worker = "test"; GameName = "TSW6"; GameBuildNumber = 1; APIVersion = 1; GameInstanceID = "test-id" }; HttpRoutes = [] }
            TreeRoot = [{ Path = "Player"; Name = "Player"; IsExpanded = false; Children = None; Endpoints = None }]
            PollingValues = Map.ofList [("Player.Speed", "50")] }
    let newModel, _ = update Disconnect model
    Assert.Equal(None, newModel.ApiConfig)
    match newModel.ConnectionState with
    | ApiConnectionState.Disconnected -> Assert.True(true)
    | _ -> Assert.Fail("Expected Disconnected state")
    Assert.Empty(newModel.TreeRoot)
    Assert.Empty(newModel.PollingValues)

[<Fact>]
let ``SelectNode updates SelectedNode and clears EndpointValues``() =
    let node = { Path = "Player"; Name = "Player"; IsExpanded = false; Children = None; Endpoints = None }
    let model = { testModel () with EndpointValues = Map.ofList [("key", "value")] }
    let newModel, _ = update (SelectNode node) model
    Assert.Equal(Some node, newModel.SelectedNode)
    Assert.Empty(newModel.EndpointValues)

[<Fact>]
let ``CollapseNode sets node IsExpanded to false``() =
    let node = { Path = "Player"; Name = "Player"; IsExpanded = true; Children = Some []; Endpoints = None }
    let model = { testModel () with TreeRoot = [node] }
    let newModel, _ = update (CollapseNode "Player") model
    match newModel.TreeRoot with
    | [n] -> Assert.False(n.IsExpanded)
    | _ -> Assert.Fail("Expected single node in TreeRoot")

[<Fact>]
let ``EndpointValueReceived adds value to EndpointValues``() =
    let model = testModel ()
    let newModel, _ = update (EndpointValueReceived ("key", "val", System.TimeSpan.FromMilliseconds(100.0))) model
    Assert.Equal("val", newModel.EndpointValues.["key"])

[<Fact>]
let ``SetSerialPort updates SerialPortName``() =
    let model = testModel ()
    let newModel, _ = update (SetSerialPort (Some "COM3")) model
    Assert.Equal(Some "COM3", newModel.SerialPortName)

[<Fact>]
let ``PortsUpdated with single Arduino auto-selects when no port selected``() =
    let model = testModel ()
    let arduinoPort = 
        { PortDetection.PortName = "COM5"
          PortDetection.UsbInfo = Some { PortDetection.Vid = "2341"; PortDetection.Pid = "0043"; PortDetection.Description = "Arduino Uno" }
          PortDetection.IsArduino = true }
    let newModel, _ = update (PortsUpdated [arduinoPort]) model
    Assert.Equal(Some "COM5", newModel.SerialPortName)

[<Fact>]
let ``LocoDetectError leaves model unchanged``() =
    let model = testModel ()
    let newModel, _ = update (LocoDetectError "error") model
    Assert.Equal(model.CurrentLoco, newModel.CurrentLoco)
    Assert.Equal(model.ConnectionState, newModel.ConnectionState)

[<Fact>]
let ``ApiError sets ConnectionState to Error``() =
    let model = testModel ()
    let newModel, _ = update (ApiError "msg") model
    match newModel.ConnectionState with
    | ApiConnectionState.Error msg -> Assert.Equal("msg", msg)
    | _ -> Assert.Fail("Expected Error state")
