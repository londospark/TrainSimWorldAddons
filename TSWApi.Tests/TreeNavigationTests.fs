module TSWApi.Tests.TreeNavigationTests

open Xunit
open TSWApi.Types
open TSWApi.TreeNavigation

// ── parseNodePath ──

[<Fact>]
let ``parseNodePath splits slash-separated path`` () =
    let result = parseNodePath "Root/Player/TransformComponent0"
    Assert.Equal<string list>([ "Root"; "Player"; "TransformComponent0" ], result)

[<Fact>]
let ``parseNodePath returns single element for leaf`` () =
    let result = parseNodePath "VirtualRailDriver"
    Assert.Equal<string list>([ "VirtualRailDriver" ], result)

[<Fact>]
let ``parseNodePath returns empty list for empty string`` () =
    let result = parseNodePath ""
    Assert.Empty(result)

// ── buildNodePath ──

[<Fact>]
let ``buildNodePath joins segments with slash`` () =
    let result = buildNodePath [ "Root"; "Player"; "TransformComponent0" ]
    Assert.Equal("Root/Player/TransformComponent0", result)

[<Fact>]
let ``buildNodePath returns empty for empty list`` () =
    let result = buildNodePath []
    Assert.Equal("", result)

// ── getNodeAtPath ──

let sampleTree: Node list =
    [ { NodePath = "Root/VirtualRailDriver"
        NodeName = "VirtualRailDriver"
        Name = null
        Nodes = None
        Endpoints = None }
      { NodePath = "Root/Player"
        NodeName = "Player"
        Name = null
        Nodes =
            Some
                [ { NodePath = "Root/Player/TransformComponent0"
                    NodeName = "TransformComponent0"
                    Name = null
                    Nodes = None
                    Endpoints = None }
                  { NodePath = "Root/Player/PC_InputComponent0"
                    NodeName = "PC_InputComponent0"
                    Name = null
                    Nodes = None
                    Endpoints = None } ]
        Endpoints = None }
      { NodePath = "Root/CurrentDrivableActor"
        NodeName = "CurrentDrivableActor"
        Name = null
        Nodes =
            Some
                [ { NodePath = "Root/CurrentDrivableActor/AWS_TPWS_Service"
                    NodeName = "AWS_TPWS_Service"
                    Name = null
                    Nodes = Some []
                    Endpoints =
                        Some
                            [ { Name = "Property.AWS_SunflowerState"
                                Writable = false } ] } ]
        Endpoints = None } ]

[<Fact>]
let ``getNodeAtPath finds top-level node`` () =
    let result = getNodeAtPath sampleTree [ "VirtualRailDriver" ]
    Assert.True(result.IsSome)
    Assert.Equal("VirtualRailDriver", result.Value.NodeName)

[<Fact>]
let ``getNodeAtPath finds nested node`` () =
    let result = getNodeAtPath sampleTree [ "Player"; "TransformComponent0" ]
    Assert.True(result.IsSome)
    Assert.Equal("TransformComponent0", result.Value.NodeName)

[<Fact>]
let ``getNodeAtPath finds deeply nested node`` () =
    let result = getNodeAtPath sampleTree [ "CurrentDrivableActor"; "AWS_TPWS_Service" ]
    Assert.True(result.IsSome)
    Assert.Equal("AWS_TPWS_Service", result.Value.NodeName)

[<Fact>]
let ``getNodeAtPath returns None for non-existent path`` () =
    let result = getNodeAtPath sampleTree [ "NonExistent" ]
    Assert.True(result.IsNone)

[<Fact>]
let ``getNodeAtPath returns None for non-existent nested path`` () =
    let result = getNodeAtPath sampleTree [ "Player"; "NonExistent" ]
    Assert.True(result.IsNone)

[<Fact>]
let ``getNodeAtPath returns None for empty path`` () =
    let result = getNodeAtPath sampleTree []
    Assert.True(result.IsNone)

// ── findEndpoint ──

[<Fact>]
let ``findEndpoint finds endpoint on node`` () =
    let node = getNodeAtPath sampleTree [ "CurrentDrivableActor"; "AWS_TPWS_Service" ]
    let endpoint = findEndpoint node.Value "Property.AWS_SunflowerState"
    Assert.True(endpoint.IsSome)
    Assert.Equal("Property.AWS_SunflowerState", endpoint.Value.Name)
    Assert.False(endpoint.Value.Writable)

[<Fact>]
let ``findEndpoint returns None for non-existent endpoint`` () =
    let node = getNodeAtPath sampleTree [ "CurrentDrivableActor"; "AWS_TPWS_Service" ]
    let endpoint = findEndpoint node.Value "Property.NonExistent"
    Assert.True(endpoint.IsNone)

[<Fact>]
let ``findEndpoint returns None when node has no endpoints`` () =
    let node = getNodeAtPath sampleTree [ "VirtualRailDriver" ]
    let endpoint = findEndpoint node.Value "Property.Something"
    Assert.True(endpoint.IsNone)

// ── getChildNodes ──

[<Fact>]
let ``getChildNodes returns children of node`` () =
    let node = getNodeAtPath sampleTree [ "Player" ]
    let children = getChildNodes node.Value
    Assert.Equal(2, children.Length)
    Assert.Equal("TransformComponent0", children.[0].NodeName)

[<Fact>]
let ``getChildNodes returns empty for leaf node`` () =
    let node = getNodeAtPath sampleTree [ "VirtualRailDriver" ]
    let children = getChildNodes node.Value
    Assert.Empty(children)
