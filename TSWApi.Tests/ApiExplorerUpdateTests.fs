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
