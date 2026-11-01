module BindingPersistenceTests

open Xunit
open CounterApp

[<Fact>]
let ``addBinding adds to empty config creates new loco with binding``() =
    let emptyConfig = { Version = 1; Locos = [] }
    let binding = { NodePath = "Player"; EndpointName = "Speed"; Label = "Player.Speed" }
    let result = BindingPersistence.addBinding emptyConfig "Class 375" binding
    Assert.Equal(1, result.Locos.Length)
    Assert.Equal("Class 375", result.Locos[0].LocoName)
    Assert.Equal(1, result.Locos[0].BoundEndpoints.Length)
    Assert.Equal("Player", result.Locos[0].BoundEndpoints[0].NodePath)
    Assert.Equal("Speed", result.Locos[0].BoundEndpoints[0].EndpointName)

[<Fact>]
let ``addBinding to existing loco appends binding``() =
    let existingBinding = { NodePath = "Player"; EndpointName = "Speed"; Label = "Player.Speed" }
    let config = { Version = 1; Locos = [{ LocoName = "Class 375"; BoundEndpoints = [existingBinding] }] }
    let newBinding = { NodePath = "Reverser"; EndpointName = "State"; Label = "Reverser.State" }
    let result = BindingPersistence.addBinding config "Class 375" newBinding
    Assert.Equal(1, result.Locos.Length)
    Assert.Equal(2, result.Locos[0].BoundEndpoints.Length)
    Assert.Equal("Player", result.Locos[0].BoundEndpoints[0].NodePath)
    Assert.Equal("Reverser", result.Locos[0].BoundEndpoints[1].NodePath)

[<Fact>]
let ``addBinding duplicate binding does not add duplicate``() =
    let binding = { NodePath = "Player"; EndpointName = "Speed"; Label = "Player.Speed" }
    let config = { Version = 1; Locos = [{ LocoName = "Class 375"; BoundEndpoints = [binding] }] }
    let result = BindingPersistence.addBinding config "Class 375" binding
    Assert.Equal(1, result.Locos.Length)
    Assert.Equal(1, result.Locos[0].BoundEndpoints.Length)

[<Fact>]
let ``addBinding to different loco adds new loco entry``() =
    let binding1 = { NodePath = "Player"; EndpointName = "Speed"; Label = "Player.Speed" }
    let config = { Version = 1; Locos = [{ LocoName = "Class 375"; BoundEndpoints = [binding1] }] }
    let binding2 = { NodePath = "Reverser"; EndpointName = "State"; Label = "Reverser.State" }
    let result = BindingPersistence.addBinding config "Class 66" binding2
    Assert.Equal(2, result.Locos.Length)
    Assert.Equal("Class 375", result.Locos[0].LocoName)
    Assert.Equal("Class 66", result.Locos[1].LocoName)
    Assert.Equal(1, result.Locos[1].BoundEndpoints.Length)

[<Fact>]
let ``removeBinding removes existing binding``() =
    let binding1 = { NodePath = "Player"; EndpointName = "Speed"; Label = "Player.Speed" }
    let binding2 = { NodePath = "Reverser"; EndpointName = "State"; Label = "Reverser.State" }
    let config = { Version = 1; Locos = [{ LocoName = "Class 375"; BoundEndpoints = [binding1; binding2] }] }
    let result = BindingPersistence.removeBinding config "Class 375" "Player" "Speed"
    Assert.Equal(1, result.Locos.Length)
    Assert.Equal(1, result.Locos[0].BoundEndpoints.Length)
    Assert.Equal("Reverser", result.Locos[0].BoundEndpoints[0].NodePath)

[<Fact>]
let ``removeBinding non-existent binding leaves config unchanged``() =
    let binding = { NodePath = "Player"; EndpointName = "Speed"; Label = "Player.Speed" }
    let config = { Version = 1; Locos = [{ LocoName = "Class 375"; BoundEndpoints = [binding] }] }
    let result = BindingPersistence.removeBinding config "Class 375" "Reverser" "State"
    Assert.Equal(1, result.Locos.Length)
    Assert.Equal(1, result.Locos[0].BoundEndpoints.Length)
    Assert.Equal("Player", result.Locos[0].BoundEndpoints[0].NodePath)

[<Fact>]
let ``removeBinding from non-existent loco leaves config unchanged``() =
    let binding = { NodePath = "Player"; EndpointName = "Speed"; Label = "Player.Speed" }
    let config = { Version = 1; Locos = [{ LocoName = "Class 375"; BoundEndpoints = [binding] }] }
    let result = BindingPersistence.removeBinding config "Class 66" "Player" "Speed"
    Assert.Equal(1, result.Locos.Length)
    Assert.Equal(1, result.Locos[0].BoundEndpoints.Length)
    Assert.Equal("Class 375", result.Locos[0].LocoName)
