module TSWApi.Tests.ApiExplorerUpdateTests

open System
open Xunit
open CounterApp
open CounterApp.ApiExplorer
open TSWApi.Types

// ─── Test helpers ───

let testInfo : InfoResponse =
    { Meta =
        { Worker = "test"
          GameName = "TSW6"
          GameBuildNumber = 1
          APIVersion = 1
          GameInstanceID = "test-id" }
      HttpRoutes = [] }

let testConfig =
    match TSWApi.Http.createConfig "test-key" with
    | Ok config -> config
    | Error _ -> failwith "test setup failed"

let testElapsed = TimeSpan.FromMilliseconds(42.0)

let makeNode path name =
    { Path = path; Name = name; IsExpanded = false; Children = None; Endpoints = None }

let connectedModel () =
    { init () with
        ApiConfig = Some testConfig
        ConnectionState = ApiConnectionState.Connected testInfo
        CommKey = "test-key" }

// ─── init ───

[<Fact>]
let ``init returns disconnected state`` () =
    let model = init ()
    Assert.Equal(ApiConnectionState.Disconnected, model.ConnectionState)
    Assert.Empty(model.TreeRoot)
    Assert.True(model.SelectedNode.IsNone)
    Assert.True(model.ApiConfig.IsNone)
    Assert.False(model.IsConnecting)
    Assert.True(model.EndpointValues.IsEmpty)
    Assert.True(model.LastResponseTime.IsNone)

// ─── Connect ───

[<Fact>]
let ``Connect sets Connecting state`` () =
    let model, cmd = update Connect (init ())
    Assert.True(model.IsConnecting)
    Assert.Equal(ApiConnectionState.Connecting, model.ConnectionState)
    Assert.False(cmd |> List.isEmpty)

// ─── ConnectSuccess ───

[<Fact>]
let ``ConnectSuccess sets Connected state`` () =
    let initial = { init () with IsConnecting = true; ConnectionState = ApiConnectionState.Connecting }
    let model, cmd = update (ConnectSuccess("test-key", testConfig, testInfo, testElapsed)) initial
    Assert.False(model.IsConnecting)
    Assert.Equal(ApiConnectionState.Connected testInfo, model.ConnectionState)
    Assert.True(model.ApiConfig.IsSome)
    Assert.Equal("test-key", model.CommKey)
    Assert.Equal(Some testElapsed, model.LastResponseTime)
    Assert.False(cmd |> List.isEmpty)

// ─── ConnectError ───

[<Fact>]
let ``ConnectError sets Error state`` () =
    let initial = { init () with IsConnecting = true; ConnectionState = ApiConnectionState.Connecting }
    let model, cmd = update (ConnectError "timeout") initial
    Assert.False(model.IsConnecting)
    Assert.Equal(ApiConnectionState.Error "timeout", model.ConnectionState)
    Assert.True(cmd |> List.isEmpty)

// ─── Disconnect ───

[<Fact>]
let ``Disconnect resets all state`` () =
    let initial =
        { connectedModel () with
            TreeRoot = [ makeNode "Root/A" "A" ]
            SelectedNode = Some (makeNode "Root/A" "A")
            EndpointValues = Map.ofList [ "x", "1" ]
            LastResponseTime = Some testElapsed }
    let model, cmd = update Disconnect initial
    Assert.Equal(ApiConnectionState.Disconnected, model.ConnectionState)
    Assert.True(model.ApiConfig.IsNone)
    Assert.Empty(model.TreeRoot)
    Assert.True(model.SelectedNode.IsNone)
    Assert.True(model.EndpointValues.IsEmpty)
    Assert.True(model.LastResponseTime.IsNone)
    Assert.True(cmd |> List.isEmpty)

// ─── RootNodesLoaded ───

[<Fact>]
let ``RootNodesLoaded populates tree`` () =
    let nodes = [ makeNode "Root/A" "A"; makeNode "Root/B" "B" ]
    let model, cmd = update (RootNodesLoaded(nodes, testElapsed)) (connectedModel ())
    Assert.Equal(2, model.TreeRoot.Length)
    Assert.Equal("Root/A", model.TreeRoot.[0].Path)
    Assert.Equal("Root/B", model.TreeRoot.[1].Path)
    Assert.Equal(Some testElapsed, model.LastResponseTime)
    Assert.True(cmd |> List.isEmpty)

// ─── ExpandNode ───

[<Fact>]
let ``ExpandNode with config returns Cmd`` () =
    let initial = { connectedModel () with TreeRoot = [ makeNode "Root/A" "A" ] }
    let model, cmd = update (ExpandNode "Root/A") initial
    Assert.False(cmd |> List.isEmpty)

[<Fact>]
let ``ExpandNode without config returns none`` () =
    let initial = { init () with TreeRoot = [ makeNode "Root/A" "A" ] }
    let model, cmd = update (ExpandNode "Root/A") initial
    Assert.True(cmd |> List.isEmpty)

// ─── NodeExpanded ───

[<Fact>]
let ``NodeExpanded updates tree with children`` () =
    let initial = { connectedModel () with TreeRoot = [ makeNode "Root/A" "A" ] }
    let children = [ makeNode "Root/A/C1" "C1"; makeNode "Root/A/C2" "C2" ]
    let model, cmd = update (NodeExpanded("Root/A", children, None, testElapsed)) initial
    let node = model.TreeRoot.[0]
    Assert.True(node.IsExpanded)
    Assert.True(node.Children.IsSome)
    Assert.Equal(2, node.Children.Value.Length)
    Assert.Equal("Root/A/C1", node.Children.Value.[0].Path)
    Assert.Equal(Some testElapsed, model.LastResponseTime)
    Assert.True(cmd |> List.isEmpty)

// ─── CollapseNode ───

[<Fact>]
let ``CollapseNode sets IsExpanded to false`` () =
    let expandedNode = { makeNode "Root/A" "A" with IsExpanded = true; Children = Some [ makeNode "Root/A/C1" "C1" ] }
    let initial = { connectedModel () with TreeRoot = [ expandedNode ] }
    let model, cmd = update (CollapseNode "Root/A") initial
    Assert.False(model.TreeRoot.[0].IsExpanded)
    Assert.True(cmd |> List.isEmpty)

// ─── SelectNode ───

[<Fact>]
let ``SelectNode sets selected and clears EndpointValues`` () =
    let node = makeNode "Root/A" "A"
    let initial = { connectedModel () with EndpointValues = Map.ofList [ "x", "1" ] }
    let model, cmd = update (SelectNode node) initial
    Assert.True(model.SelectedNode.IsSome)
    Assert.Equal("Root/A", model.SelectedNode.Value.Path)
    Assert.True(model.EndpointValues.IsEmpty)
    Assert.True(cmd |> List.isEmpty)

// ─── SetBaseUrl ───

[<Fact>]
let ``SetBaseUrl updates url`` () =
    let model, cmd = update (SetBaseUrl "http://example.com:9999") (init ())
    Assert.Equal("http://example.com:9999", model.BaseUrl)
    Assert.True(cmd |> List.isEmpty)

// ─── SetCommKey ───

[<Fact>]
let ``SetCommKey updates key`` () =
    let model, cmd = update (SetCommKey "new-key") (init ())
    Assert.Equal("new-key", model.CommKey)
    Assert.True(cmd |> List.isEmpty)

// ─── EndpointValueReceived ───

[<Fact>]
let ``EndpointValueReceived adds to map`` () =
    let initial = connectedModel ()
    let model, cmd = update (EndpointValueReceived("Root/A/prop", "42", testElapsed)) initial
    Assert.Equal("42", Map.find "Root/A/prop" model.EndpointValues)
    Assert.Equal(Some testElapsed, model.LastResponseTime)
    Assert.True(cmd |> List.isEmpty)

// ─── NodeExpanded with endpoints ───

[<Fact>]
let ``NodeExpanded sets parent endpoints`` () =
    let endpoints = Some [ { Name = "Property.Speed"; Writable = false } ]
    let initial = { connectedModel () with TreeRoot = [ makeNode "Player" "Player" ] }
    let children = [ makeNode "Player/TransformComponent0" "TransformComponent0" ]
    let model, cmd = update (NodeExpanded("Player", children, endpoints, testElapsed)) initial
    let node = model.TreeRoot.[0]
    Assert.True(node.IsExpanded)
    Assert.True(node.Children.IsSome)
    Assert.True(node.Endpoints.IsSome)
    Assert.Equal(1, node.Endpoints.Value.Length)
    Assert.Equal("Property.Speed", node.Endpoints.Value.[0].Name)
    Assert.True(cmd |> List.isEmpty)

[<Fact>]
let ``NodeExpanded with nested path updates correct node`` () =
    let parent = makeNode "Player" "Player"
    let child = makeNode "Player/TransformComponent0" "TransformComponent0"
    let parentWithChild = { parent with IsExpanded = true; Children = Some [ child ] }
    let initial = { connectedModel () with TreeRoot = [ parentWithChild ] }
    let grandchildren = [ makeNode "Player/TransformComponent0/Position" "Position" ]
    let endpoints = Some [ { Name = "Property.X"; Writable = true } ]
    let model, cmd = update (NodeExpanded("Player/TransformComponent0", grandchildren, endpoints, testElapsed)) initial
    let updatedParent = model.TreeRoot.[0]
    Assert.True(updatedParent.Children.IsSome)
    let updatedChild = updatedParent.Children.Value.[0]
    Assert.True(updatedChild.IsExpanded)
    Assert.True(updatedChild.Children.IsSome)
    Assert.Equal(1, updatedChild.Children.Value.Length)
    Assert.True(updatedChild.Endpoints.IsSome)
    Assert.Equal("Property.X", updatedChild.Endpoints.Value.[0].Name)
    Assert.True(cmd |> List.isEmpty)

[<Fact>]
let ``ToggleExpand on expanded child collapses it`` () =
    let parent = makeNode "Player" "Player"
    let child = { makeNode "Player/TransformComponent0" "TransformComponent0" with 
                    IsExpanded = true
                    Children = Some [ makeNode "Player/TransformComponent0/Position" "Position" ] }
    let parentWithChild = { parent with IsExpanded = true; Children = Some [ child ] }
    let initial = { connectedModel () with TreeRoot = [ parentWithChild ] }
    let model, cmd = update (ToggleExpand "Player/TransformComponent0") initial
    let updatedParent = model.TreeRoot.[0]
    Assert.True(updatedParent.Children.IsSome)
    let updatedChild = updatedParent.Children.Value.[0]
    Assert.False(updatedChild.IsExpanded)
    Assert.True(cmd |> List.isEmpty)

[<Fact>]
let ``ToggleExpand on unexpanded child with no children triggers expand`` () =
    let parent = makeNode "Player" "Player"
    let child = makeNode "Player/TransformComponent0" "TransformComponent0"
    let parentWithChild = { parent with IsExpanded = true; Children = Some [ child ] }
    let initial = { connectedModel () with TreeRoot = [ parentWithChild ] }
    let model, cmd = update (ToggleExpand "Player/TransformComponent0") initial
    Assert.False(cmd |> List.isEmpty)

// ─── SetSearchQuery ───

[<Fact>]
let ``SetSearchQuery updates model`` () =
    let model, cmd = update (SetSearchQuery "Player") (connectedModel ())
    Assert.Equal("Player", model.SearchQuery)
    Assert.True(cmd |> List.isEmpty)

[<Fact>]
let ``SetSearchQuery with empty string clears search`` () =
    let initial = { connectedModel () with SearchQuery = "Player" }
    let model, cmd = update (SetSearchQuery "") initial
    Assert.Equal("", model.SearchQuery)
    Assert.True(cmd |> List.isEmpty)

[<Fact>]
let ``Initial model has empty SearchQuery`` () =
    let model = init ()
    Assert.Equal("", model.SearchQuery)

// ─── Binding ───

let connectedWithLoco () =
    { connectedModel () with
        CurrentLoco = Some "TestLoco_123"
        BindingsConfig = { Version = 1; Locos = [] } }

[<Fact>]
let ``BindEndpoint adds binding when loco is known`` () =
    let model = connectedWithLoco ()
    let newModel, _ = update (BindEndpoint ("CurrentDrivableActor/BP_AWS", "Property.Sunflower")) model
    let loco = newModel.BindingsConfig.Locos |> List.find (fun l -> l.LocoName = "TestLoco_123")
    Assert.Equal(1, loco.BoundEndpoints.Length)
    Assert.Equal("CurrentDrivableActor/BP_AWS", loco.BoundEndpoints.[0].NodePath)
    Assert.Equal("Property.Sunflower", loco.BoundEndpoints.[0].EndpointName)
    Assert.True(newModel.IsPolling)

[<Fact>]
let ``BindEndpoint does nothing when no loco detected`` () =
    let model = { connectedModel () with CurrentLoco = None; BindingsConfig = { Version = 1; Locos = [] } }
    let newModel, _ = update (BindEndpoint ("SomePath", "SomeEndpoint")) model
    Assert.True(newModel.BindingsConfig.Locos.IsEmpty)

[<Fact>]
let ``UnbindEndpoint removes binding`` () =
    let model =
        { connectedWithLoco () with
            BindingsConfig =
                { Version = 1
                  Locos = [ { LocoName = "TestLoco_123"
                              BoundEndpoints = [ { NodePath = "A"; EndpointName = "B"; Label = "A.B" }
                                                 { NodePath = "C"; EndpointName = "D"; Label = "C.D" } ] } ] } }
    let newModel, _ = update (UnbindEndpoint ("A", "B")) model
    let loco = newModel.BindingsConfig.Locos |> List.find (fun l -> l.LocoName = "TestLoco_123")
    Assert.Equal(1, loco.BoundEndpoints.Length)
    Assert.Equal("C", loco.BoundEndpoints.[0].NodePath)

// ─── Loco Detection ───

[<Fact>]
let ``LocoDetected sets CurrentLoco`` () =
    let model = connectedModel ()
    let newModel, _ = update (LocoDetected "RVM_Class350_ABC") model
    Assert.Equal(Some "RVM_Class350_ABC", newModel.CurrentLoco)

[<Fact>]
let ``LocoDetectError does not change model`` () =
    let model = connectedModel ()
    let newModel, _ = update (LocoDetectError "some error") model
    Assert.True(newModel.CurrentLoco.IsNone)

// ─── Polling ───

[<Fact>]
let ``StartPolling sets IsPolling`` () =
    let model = connectedModel ()
    let newModel, _ = update StartPolling model
    Assert.True(newModel.IsPolling)

[<Fact>]
let ``StopPolling clears IsPolling`` () =
    let model = { connectedModel () with IsPolling = true }
    let newModel, _ = update StopPolling model
    Assert.False(newModel.IsPolling)

[<Fact>]
let ``PollValueReceived updates PollingValues`` () =
    let model = connectedModel ()
    let newModel, _ = update (PollValueReceived ("key1", "42")) model
    Assert.Equal(Some "42", Map.tryFind "key1" newModel.PollingValues)

[<Fact>]
let ``PollingTick with no bindings produces no command`` () =
    let model = { connectedWithLoco () with IsPolling = true }
    let _, cmd = update PollingTick model
    Assert.True(cmd |> List.isEmpty)

[<Fact>]
let ``PollingTick with bindings produces command`` () =
    let model =
        { connectedWithLoco () with
            IsPolling = true
            BindingsConfig =
                { Version = 1
                  Locos = [ { LocoName = "TestLoco_123"
                              BoundEndpoints = [ { NodePath = "A"; EndpointName = "B"; Label = "A.B" } ] } ] } }
    let _, cmd = update PollingTick model
    Assert.False(cmd |> List.isEmpty)

[<Fact>]
let ``PollingTick without config produces no command`` () =
    let model = { init () with CurrentLoco = Some "Loco"; IsPolling = true }
    let _, cmd = update PollingTick model
    Assert.True(cmd |> List.isEmpty)

// ─── Serial ───

[<Fact>]
let ``SetSerialPort updates SerialPortName`` () =
    let model = connectedModel ()
    let newModel, _ = update (SetSerialPort (Some "COM3")) model
    Assert.Equal(Some "COM3", newModel.SerialPortName)

[<Fact>]
let ``DisconnectSerial clears SerialPort`` () =
    let model = connectedModel ()
    let newModel, _ = update DisconnectSerial model
    Assert.True(newModel.SerialPort.IsNone)

// ─── Init with new fields ───

[<Fact>]
let ``init has no CurrentLoco`` () =
    let model = init ()
    Assert.True(model.CurrentLoco.IsNone)

[<Fact>]
let ``init has polling disabled`` () =
    let model = init ()
    Assert.False(model.IsPolling)
    Assert.True(model.PollingValues.IsEmpty)

[<Fact>]
let ``init has no serial port`` () =
    let model = init ()
    Assert.True(model.SerialPort.IsNone)
    Assert.True(model.SerialPortName.IsNone)

[<Fact>]
let ``Disconnect clears binding state`` () =
    let model =
        { connectedWithLoco () with
            IsPolling = true
            PollingValues = Map.ofList [("a", "1")] }
    let newModel, _ = update Disconnect model
    Assert.True(newModel.CurrentLoco.IsNone)
    Assert.False(newModel.IsPolling)
    Assert.True(newModel.PollingValues.IsEmpty)
    Assert.True(newModel.SerialPort.IsNone)
