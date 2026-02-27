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
let ``LocoDetected same loco is no-op`` () =
    let model = { connectedModel () with CurrentLoco = Some "SameLoco"; PollingValues = Map.ofList [("k", "v")] }
    let newModel, _ = update (LocoDetected "SameLoco") model
    Assert.Equal(Some "SameLoco", newModel.CurrentLoco)
    Assert.Equal(Some "v", Map.tryFind "k" newModel.PollingValues)

[<Fact>]
let ``LocoDetected different loco clears PollingValues`` () =
    let model = { connectedModel () with CurrentLoco = Some "OldLoco"; PollingValues = Map.ofList [("k", "v")]; IsPolling = true }
    let newModel, _ = update (LocoDetected "NewLoco") model
    Assert.Equal(Some "NewLoco", newModel.CurrentLoco)
    Assert.True(newModel.PollingValues.IsEmpty)

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

// ─── Loco change behavior ───

[<Fact>]
let ``LocoDetected with different loco clears polling values`` () =
    let model =
        { connectedModel () with
            CurrentLoco = Some "LocoA"
            PollingValues = Map.ofList [("k1", "v1"); ("k2", "v2")]
            BindingsConfig = { Version = 1; Locos = [] } }
    let newModel, _ = update (LocoDetected "LocoB") model
    Assert.Equal(Some "LocoB", newModel.CurrentLoco)
    Assert.True(newModel.PollingValues.IsEmpty)

[<Fact>]
let ``LocoDetected with different loco reloads bindings config`` () =
    let model =
        { connectedModel () with
            CurrentLoco = Some "LocoA"
            BindingsConfig = { Version = 1; Locos = [{ LocoName = "LocoA"; BoundEndpoints = [{ NodePath = "A"; EndpointName = "B"; Label = "A.B" }] }] } }
    let newModel, _ = update (LocoDetected "LocoB") model
    Assert.Equal(Some "LocoB", newModel.CurrentLoco)
    // Config is reloaded from persistence — for a new loco with no saved bindings, loco won't appear
    let locoBindings =
        newModel.BindingsConfig.Locos
        |> List.tryFind (fun l -> l.LocoName = "LocoB")
        |> Option.map (fun l -> l.BoundEndpoints)
        |> Option.defaultValue []
    Assert.True(locoBindings.IsEmpty)

[<Fact>]
let ``LocoDetected with same loco does not clear polling values`` () =
    let model =
        { connectedModel () with
            CurrentLoco = Some "SameLoco"
            PollingValues = Map.ofList [("k1", "v1"); ("k2", "v2")]
            BindingsConfig = { Version = 1; Locos = [] } }
    let newModel, _ = update (LocoDetected "SameLoco") model
    Assert.Equal(Some "SameLoco", newModel.CurrentLoco)
    Assert.Equal(2, newModel.PollingValues.Count)
    Assert.Equal(Some "v1", Map.tryFind "k1" newModel.PollingValues)
    Assert.Equal(Some "v2", Map.tryFind "k2" newModel.PollingValues)

// ─── Serial value mapping ───

[<Fact>]
let ``PollValueReceived with value containing 1 updates PollingValues`` () =
    let model = { connectedModel () with PollingValues = Map.empty; BindingsConfig = { Version = 1; Locos = [] } }
    let newModel, cmd = update (PollValueReceived ("endpoint.key", "Value: 1")) model
    Assert.Equal(Some "Value: 1", Map.tryFind "endpoint.key" newModel.PollingValues)
    Assert.True(cmd |> List.isEmpty)

[<Fact>]
let ``PollValueReceived with value containing 0 updates PollingValues`` () =
    let model = { connectedModel () with PollingValues = Map.empty; BindingsConfig = { Version = 1; Locos = [] } }
    let newModel, cmd = update (PollValueReceived ("endpoint.key", "Value: 0")) model
    Assert.Equal(Some "Value: 0", Map.tryFind "endpoint.key" newModel.PollingValues)
    Assert.True(cmd |> List.isEmpty)

[<Fact>]
let ``PollValueReceived with unchanged value does not trigger change`` () =
    let model =
        { connectedModel () with
            PollingValues = Map.ofList [("endpoint.key", "42")]
            BindingsConfig = { Version = 1; Locos = [] } }
    let newModel, cmd = update (PollValueReceived ("endpoint.key", "42")) model
    Assert.Equal(Some "42", Map.tryFind "endpoint.key" newModel.PollingValues)
    Assert.True(cmd |> List.isEmpty)

// ─── BindEndpoint with immediate poll ───

[<Fact>]
let ``BindEndpoint returns poll command when api config present`` () =
    let model = connectedWithLoco ()
    let _, cmd = update (BindEndpoint ("NodePath", "EndpointName")) model
    Assert.False(cmd |> List.isEmpty)

[<Fact>]
let ``BindEndpoint returns no command when api config absent`` () =
    let model =
        { init () with
            CurrentLoco = Some "TestLoco_123"
            ApiConfig = None
            BindingsConfig = { Version = 1; Locos = [] } }
    let _, cmd = update (BindEndpoint ("NodePath", "EndpointName")) model
    Assert.True(cmd |> List.isEmpty)

// ─── Pure in-memory persistence functions ───

open CounterApp.BindingPersistence

[<Fact>]
let ``addBinding adds to empty config`` () =
    let config = { Version = 1; Locos = [] }
    let binding = { NodePath = "A"; EndpointName = "B"; Label = "A.B" }
    let result = addBinding config "TestLoco" binding
    let loco = result.Locos |> List.find (fun l -> l.LocoName = "TestLoco")
    Assert.Equal(1, loco.BoundEndpoints.Length)
    Assert.Equal("A", loco.BoundEndpoints.[0].NodePath)
    Assert.Equal("B", loco.BoundEndpoints.[0].EndpointName)

[<Fact>]
let ``addBinding does not duplicate`` () =
    let config = { Version = 1; Locos = [] }
    let binding = { NodePath = "A"; EndpointName = "B"; Label = "A.B" }
    let once = addBinding config "TestLoco" binding
    let twice = addBinding once "TestLoco" binding
    let loco = twice.Locos |> List.find (fun l -> l.LocoName = "TestLoco")
    Assert.Equal(1, loco.BoundEndpoints.Length)

[<Fact>]
let ``removeBinding removes specific endpoint`` () =
    let config =
        { Version = 1
          Locos = [{ LocoName = "TestLoco"
                     BoundEndpoints = [{ NodePath = "A"; EndpointName = "B"; Label = "A.B" }
                                       { NodePath = "C"; EndpointName = "D"; Label = "C.D" }] }] }
    let result = removeBinding config "TestLoco" "A" "B"
    let loco = result.Locos |> List.find (fun l -> l.LocoName = "TestLoco")
    Assert.Equal(1, loco.BoundEndpoints.Length)
    Assert.Equal("C", loco.BoundEndpoints.[0].NodePath)

[<Fact>]
let ``removeBinding is no-op for missing endpoint`` () =
    let config =
        { Version = 1
          Locos = [{ LocoName = "TestLoco"
                     BoundEndpoints = [{ NodePath = "A"; EndpointName = "B"; Label = "A.B" }] }] }
    let result = removeBinding config "TestLoco" "X" "Y"
    let loco = result.Locos |> List.find (fun l -> l.LocoName = "TestLoco")
    Assert.Equal(1, loco.BoundEndpoints.Length)
    Assert.Equal("A", loco.BoundEndpoints.[0].NodePath)

[<Fact>]
let ``Tree expansion works at 5 levels deep`` () =
    // Level 0: Root with one node
    let root = makeNode "CF" "CurrentFormation"
    let initial = { connectedModel () with TreeRoot = [ root ] }

    // Level 1: Expand CF -> get child "0"
    let ch1 = [ makeNode "CF/0" "0" ]
    let m1, _ = update (NodeExpanded("CF", ch1, None, testElapsed)) initial
    Assert.True(m1.TreeRoot.[0].IsExpanded)
    Assert.Equal(1, m1.TreeRoot.[0].Children.Value.Length)

    // Level 2: ToggleExpand on "CF/0" -> should trigger ExpandNode
    let m2, cmd2 = update (ToggleExpand "CF/0") m1
    Assert.False(cmd2 |> List.isEmpty) // Should produce expand command

    // Level 2: NodeExpanded for "CF/0"
    let ch2 = [ makeNode "CF/0/Sim" "Simulation" ]
    let m3, _ = update (NodeExpanded("CF/0", ch2, None, testElapsed)) m2

    // Level 3: ToggleExpand on "CF/0/Sim" -> should trigger ExpandNode
    let m4, cmd4 = update (ToggleExpand "CF/0/Sim") m3
    Assert.False(cmd4 |> List.isEmpty) // Should produce expand command

    // Level 3: NodeExpanded for "CF/0/Sim"
    let ch3 = [ makeNode "CF/0/Sim/Bogie" "Bogie_2" ]
    let m5, _ = update (NodeExpanded("CF/0/Sim", ch3, None, testElapsed)) m4

    // Level 4: ToggleExpand on "CF/0/Sim/Bogie" -> should trigger ExpandNode
    let m6, cmd6 = update (ToggleExpand "CF/0/Sim/Bogie") m5
    Assert.False(cmd6 |> List.isEmpty) // Should produce expand command

    // Level 4: NodeExpanded for "CF/0/Sim/Bogie"
    let ch4 = [ makeNode "CF/0/Sim/Bogie/Children" "Children" ]
    let m7, _ = update (NodeExpanded("CF/0/Sim/Bogie", ch4, None, testElapsed)) m6

    // Level 5: ToggleExpand on "CF/0/Sim/Bogie/Children" -> should trigger ExpandNode
    let m8, cmd8 = update (ToggleExpand "CF/0/Sim/Bogie/Children") m7
    Assert.False(cmd8 |> List.isEmpty) // Should produce expand command at level 5

    // Verify the full tree structure
    let cfNode = m8.TreeRoot.[0]
    Assert.True(cfNode.IsExpanded)
    let node0 = cfNode.Children.Value.[0]
    Assert.True(node0.IsExpanded)
    let sim = node0.Children.Value.[0]
    Assert.True(sim.IsExpanded)
    let bogie = sim.Children.Value.[0]
    Assert.True(bogie.IsExpanded)
    let childrenNode = bogie.Children.Value.[0]
    Assert.True(childrenNode.Children.IsNone) // Not yet expanded

[<Fact>]
let ``ToggleExpand on pre-populated node expands without API call`` () =
    // Simulate a node pre-populated with children (from recursive mapping of root /list)
    let grandchild = makeNode "Player/TC0" "TransformComponent0"
    let parent = { makeNode "Player" "Player" with Children = Some [ grandchild ] }
    let initial = { connectedModel () with TreeRoot = [ parent ] }
    // ToggleExpand on parent should NOT trigger an API call (case 3: already has children)
    let model, cmd = update (ToggleExpand "Player") initial
    Assert.True(model.TreeRoot.[0].IsExpanded)
    Assert.True(cmd |> List.isEmpty) // No API call needed
    Assert.Equal(1, model.TreeRoot.[0].Children.Value.Length)
    Assert.Equal("Player/TC0", model.TreeRoot.[0].Children.Value.[0].Path)

[<Fact>]
let ``ToggleExpand on pre-populated child triggers API expand`` () =
    // Pre-populated grandchild has Children = None (not yet loaded)
    let grandchild = makeNode "Player/TC0" "TransformComponent0"
    let parent = { makeNode "Player" "Player" with IsExpanded = true; Children = Some [ grandchild ] }
    let initial = { connectedModel () with TreeRoot = [ parent ] }
    // ToggleExpand on grandchild should trigger API call (case 2: Children.IsNone)
    let _, cmd = update (ToggleExpand "Player/TC0") initial
    Assert.False(cmd |> List.isEmpty) // Should trigger API expand
