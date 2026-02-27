namespace CounterApp.Tests

open Xunit
open CounterApp
open CounterApp.ApplicationScreenHelpers
open TSWApi

module HelpersTests =

    // ─── stripRootPrefix ───

    [<Fact>]
    let ``stripRootPrefix with "Root/Player" returns "Player"``() =
        let result = stripRootPrefix "Root/Player"
        Assert.Equal("Player", result)

    [<Fact>]
    let ``stripRootPrefix with "SomethingElse" returns "SomethingElse"``() =
        let result = stripRootPrefix "SomethingElse"
        Assert.Equal("SomethingElse", result)

    [<Fact>]
    let ``stripRootPrefix with null returns null``() =
        let result = stripRootPrefix null
        Assert.Null(result)

    [<Fact>]
    let ``stripRootPrefix with empty string returns empty string``() =
        let result = stripRootPrefix ""
        Assert.Equal("", result)

    // ─── nullSafe ───

    [<Fact>]
    let ``nullSafe with null returns ""``() =
        let result = nullSafe null
        Assert.Equal("", result)

    [<Fact>]
    let ``nullSafe with "hello" returns "hello"``() =
        let result = nullSafe "hello"
        Assert.Equal("hello", result)

    [<Fact>]
    let ``nullSafe with empty string returns ""``() =
        let result = nullSafe ""
        Assert.Equal("", result)

    // ─── effectiveName ───

    [<Fact>]
    let ``effectiveName with NodeName set returns NodeName``() =
        let node: TSWApi.Types.Node = 
            { NodePath = "path"
              Name = "Name"
              NodeName = "NodeName"
              Nodes = None
              Endpoints = None }
        let result = effectiveName node
        Assert.Equal("NodeName", result)

    [<Fact>]
    let ``effectiveName with only Name set returns Name``() =
        let node: TSWApi.Types.Node = 
            { NodePath = "path"
              Name = "Name"
              NodeName = ""
              Nodes = None
              Endpoints = None }
        let result = effectiveName node
        Assert.Equal("Name", result)

    [<Fact>]
    let ``effectiveName with both empty returns ""``() =
        let node: TSWApi.Types.Node = 
            { NodePath = "path"
              Name = ""
              NodeName = ""
              Nodes = None
              Endpoints = None }
        let result = effectiveName node
        Assert.Equal("", result)

    // ─── endpointKey ───

    [<Fact>]
    let ``endpointKey with "path" and "name" returns "path.name"``() =
        let result = endpointKey "path" "name"
        Assert.Equal("path.name", result)

    // ─── getLocoBindings ───

    [<Fact>]
    let ``getLocoBindings with matching loco returns its bindings``() =
        let binding1 = { NodePath = "node1"; EndpointName = "endpoint1"; Label = "label1" }
        let binding2 = { NodePath = "node2"; EndpointName = "endpoint2"; Label = "label2" }
        let locoConfig = { LocoName = "TestLoco"; BoundEndpoints = [binding1; binding2] }
        let config = { Version = 1; Locos = [locoConfig] }
        let result = getLocoBindings config "TestLoco"
        Assert.Equal(2, result.Length)
        Assert.Equal(binding1, result.[0])
        Assert.Equal(binding2, result.[1])

    [<Fact>]
    let ``getLocoBindings without matching loco returns empty list``() =
        let locoConfig = { LocoName = "OtherLoco"; BoundEndpoints = [] }
        let config = { Version = 1; Locos = [locoConfig] }
        let result = getLocoBindings config "TestLoco"
        Assert.Equal(0, result.Length)

    [<Fact>]
    let ``getLocoBindings with empty config returns empty list``() =
        let config = { Version = 1; Locos = [] }
        let result = getLocoBindings config "TestLoco"
        Assert.Equal(0, result.Length)

    // ─── findNode ───

    let makeNode path name =
        { Path = path; Name = name; IsExpanded = false; Children = None; Endpoints = None }

    [<Fact>]
    let ``findNode finds root-level node``() =
        let node1 = makeNode "Root/Node1" "Node1"
        let node2 = makeNode "Root/Node2" "Node2"
        let nodes = [node1; node2]
        let result = findNode "Root/Node1" nodes
        Assert.True(result.IsSome)
        Assert.Equal("Node1", result.Value.Name)

    [<Fact>]
    let ``findNode finds nested node``() =
        let childNode = makeNode "Root/Parent/Child" "Child"
        let parentNode = { makeNode "Root/Parent" "Parent" with Children = Some [childNode] }
        let nodes = [parentNode]
        let result = findNode "Root/Parent/Child" nodes
        Assert.True(result.IsSome)
        Assert.Equal("Child", result.Value.Name)

    [<Fact>]
    let ``findNode returns None when not found``() =
        let node1 = makeNode "Root/Node1" "Node1"
        let nodes = [node1]
        let result = findNode "Root/NonExistent" nodes
        Assert.True(result.IsNone)

    // ─── updateTreeNode ───

    [<Fact>]
    let ``updateTreeNode updates root-level node``() =
        let node1 = makeNode "Root/Node1" "Node1"
        let node2 = makeNode "Root/Node2" "Node2"
        let nodes = [node1; node2]
        let updater n = { n with IsExpanded = true }
        let result = updateTreeNode "Root/Node1" updater nodes
        Assert.Equal(2, result.Length)
        Assert.True(result.[0].IsExpanded)
        Assert.False(result.[1].IsExpanded)

    [<Fact>]
    let ``updateTreeNode updates nested node and preserves parents``() =
        let childNode = makeNode "Root/Parent/Child" "Child"
        let parentNode = { makeNode "Root/Parent" "Parent" with Children = Some [childNode] }
        let nodes = [parentNode]
        let updater n = { n with IsExpanded = true }
        let result = updateTreeNode "Root/Parent/Child" updater nodes
        Assert.Equal(1, result.Length)
        Assert.False(result.[0].IsExpanded)
        Assert.True(result.[0].Children.Value.[0].IsExpanded)

    [<Fact>]
    let ``updateTreeNode with non-existent path returns list unchanged``() =
        let node1 = makeNode "Root/Node1" "Node1"
        let nodes = [node1]
        let updater n = { n with IsExpanded = true }
        let result = updateTreeNode "Root/NonExistent" updater nodes
        Assert.Equal(1, result.Length)
        Assert.False(result.[0].IsExpanded)

    // ─── filterTree ───

    [<Fact>]
    let ``filterTree with empty query returns all nodes``() =
        let node1 = makeNode "Root/Node1" "Node1"
        let node2 = makeNode "Root/Node2" "Node2"
        let nodes = [node1; node2]
        let result = filterTree "" nodes
        Assert.Equal(2, result.Length)

    [<Fact>]
    let ``filterTree with whitespace query returns all nodes``() =
        let node1 = makeNode "Root/Node1" "Node1"
        let node2 = makeNode "Root/Node2" "Node2"
        let nodes = [node1; node2]
        let result = filterTree "   " nodes
        Assert.Equal(2, result.Length)

    [<Fact>]
    let ``filterTree with query matching leaf returns leaf``() =
        let node1 = makeNode "Root/Player" "Player"
        let node2 = makeNode "Root/Vehicle" "Vehicle"
        let nodes = [node1; node2]
        let result = filterTree "Play" nodes
        Assert.Equal(1, result.Length)
        Assert.Equal("Player", result.[0].Name)

    [<Fact>]
    let ``filterTree with query matching parent returns parent with children``() =
        let childNode = makeNode "Root/Parent/Child" "Child"
        let parentNode = { makeNode "Root/Parent" "Parent" with Children = Some [childNode] }
        let nodes = [parentNode]
        let result = filterTree "Parent" nodes
        Assert.Equal(1, result.Length)
        Assert.Equal("Parent", result.[0].Name)
        Assert.True(result.[0].Children.IsSome)

    [<Fact>]
    let ``filterTree with query matching no nodes returns empty``() =
        let node1 = makeNode "Root/Node1" "Node1"
        let node2 = makeNode "Root/Node2" "Node2"
        let nodes = [node1; node2]
        let result = filterTree "NonExistent" nodes
        Assert.Equal(0, result.Length)

    [<Fact>]
    let ``filterTree is case insensitive``() =
        let node1 = makeNode "Root/Player" "Player"
        let nodes = [node1]
        let result = filterTree "PLAYER" nodes
        Assert.Equal(1, result.Length)
        Assert.Equal("Player", result.[0].Name)
