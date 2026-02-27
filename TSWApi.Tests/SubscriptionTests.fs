module TSWApi.Tests.SubscriptionTests

open System
open System.Net
open System.Net.Http
open System.Threading
open Xunit
open TSWApi.Types
open TSWApi.Subscription
open TSWApi.Tests.TestHelpers

let testAddress =
    { NodePath = "TestNode"
      EndpointName = "TestEndpoint" }

let fastConfig onChange onError =
    { Interval = TimeSpan.FromMilliseconds(10.0)
      OnChange = onChange
      OnError = onError }

// ── endpointPath ──

[<Fact>]
let ``endpointPath joins NodePath and EndpointName with dot`` () =
    let addr =
        { NodePath = "Node/Sub"
          EndpointName = "Prop.Value" }

    Assert.Equal("Node/Sub.Prop.Value", endpointPath addr)

[<Fact>]
let ``endpointPath with simple names`` () =
    let addr =
        { NodePath = "Root"
          EndpointName = "Name" }

    Assert.Equal("Root.Name", endpointPath addr)

// ── defaultConfig ──

[<Fact>]
let ``defaultConfig has 200ms interval`` () =
    Assert.Equal(TimeSpan.FromMilliseconds(200.0), defaultConfig.Interval)

[<Fact>]
let ``defaultConfig OnChange does not throw`` () =
    defaultConfig.OnChange
        { Address = testAddress
          OldValue = None
          NewValue = "1" }

[<Fact>]
let ``defaultConfig OnError does not throw`` () =
    defaultConfig.OnError testAddress (NetworkError(exn "test"))

// ── First poll fires OnChange ──

[<Fact>]
let ``first poll fires OnChange with OldValue None`` () =
    let changes = ResizeArray<ValueChange>()
    let client = constantClient (valueJson "1")
    let config = fastConfig changes.Add (fun _ _ -> ())

    use sub = create client testConfig config
    sub.Add testAddress
    Thread.Sleep(150)

    Assert.True(changes.Count >= 1, $"Expected at least 1 change, got {changes.Count}")
    Assert.Equal(testAddress, changes[0].Address)
    Assert.True(changes[0].OldValue.IsNone, "First poll should have OldValue = None")
    Assert.Equal("1", changes[0].NewValue)

// ── Change detection ──

[<Fact>]
let ``OnChange fires with correct OldValue and NewValue on value change`` () =
    let changes = ResizeArray<ValueChange>()
    let jsons = [ valueJson "1"; valueJson "1"; valueJson "2"; valueJson "2" ]
    let client = sequentialClient jsons
    let config = fastConfig changes.Add (fun _ _ -> ())

    use sub = create client testConfig config
    sub.Add testAddress
    Thread.Sleep(200)

    // Should have at least 2 changes: initial (None→1) and change (1→2)
    Assert.True(changes.Count >= 2, $"Expected at least 2 changes, got {changes.Count}")
    Assert.Equal("1", changes[0].NewValue)
    Assert.True(changes[0].OldValue.IsNone)
    Assert.Equal("2", changes[1].NewValue)
    Assert.Equal(Some "1", changes[1].OldValue)

[<Fact>]
let ``same value twice does not fire OnChange again`` () =
    let changes = ResizeArray<ValueChange>()
    let client = constantClient (valueJson "42")
    let config = fastConfig changes.Add (fun _ _ -> ())

    use sub = create client testConfig config
    sub.Add testAddress
    Thread.Sleep(200)

    // Only the initial change should fire, even after multiple polls
    Assert.Equal(1, changes.Count)
    Assert.Equal("42", changes[0].NewValue)

// ── Add/Remove idempotency ──

[<Fact>]
let ``Add is idempotent - adding same address twice is no-op`` () =
    let client = constantClient (valueJson "1")
    let config = fastConfig (fun _ -> ()) (fun _ _ -> ())

    use sub = create client testConfig config
    sub.Add testAddress
    sub.Add testAddress

    Assert.Equal(1, sub.Endpoints.Length)

[<Fact>]
let ``Remove is idempotent - removing non-existent address is no-op`` () =
    let client = constantClient (valueJson "1")
    let config = fastConfig (fun _ -> ()) (fun _ _ -> ())

    use sub = create client testConfig config

    sub.Remove testAddress // should not throw
    Assert.Equal(0, sub.Endpoints.Length)

[<Fact>]
let ``Remove stops polling that endpoint`` () =
    let changes = ResizeArray<ValueChange>()
    let client = constantClient (valueJson "1")
    let config = fastConfig changes.Add (fun _ _ -> ())

    use sub = create client testConfig config
    sub.Add testAddress
    Thread.Sleep(100)

    let countBefore = changes.Count
    sub.Remove testAddress
    Thread.Sleep(100)

    // After removal, no more changes should fire
    Assert.Equal(countBefore, changes.Count)

// ── Dispose ──

[<Fact>]
let ``Dispose stops polling and sets IsActive to false`` () =
    let changes = ResizeArray<ValueChange>()
    let client = constantClient (valueJson "1")
    let config = fastConfig changes.Add (fun _ _ -> ())

    let sub = create client testConfig config
    sub.Add testAddress
    Thread.Sleep(100)
    Assert.True(sub.IsActive)

    sub.Dispose()
    Assert.False(sub.IsActive)

    let countAfterDispose = changes.Count
    Thread.Sleep(100)
    Assert.Equal(countAfterDispose, changes.Count)

[<Fact>]
let ``Dispose is idempotent`` () =
    let client = constantClient (valueJson "1")
    let config = fastConfig (fun _ -> ()) (fun _ _ -> ())

    let sub = create client testConfig config
    sub.Dispose()
    sub.Dispose() // should not throw

// ── Error handling ──

[<Fact>]
let ``poll error calls OnError and does not stop subscription`` () =
    let errors = ResizeArray<EndpointAddress * ApiError>()
    let client = errorClient ()
    let config = fastConfig (fun _ -> ()) (fun addr err -> errors.Add(addr, err))

    use sub = create client testConfig config
    sub.Add testAddress
    Thread.Sleep(150)

    Assert.True(errors.Count >= 1, $"Expected at least 1 error, got {errors.Count}")
    Assert.Equal(testAddress, fst errors[0])

    match snd errors[0] with
    | HttpError(500, _) -> ()
    | other -> Assert.Fail($"Expected HttpError 500, got {other}")

    Assert.True(sub.IsActive, "Subscription should still be active after errors")

[<Fact>]
let ``error on one endpoint does not prevent polling others`` () =
    let changes = ResizeArray<ValueChange>()
    let errors = ResizeArray<EndpointAddress * ApiError>()
    let callCount = ref 0

    // First endpoint errors, second succeeds
    let addr1 =
        { NodePath = "ErrorNode"
          EndpointName = "Endpoint" }

    let addr2 =
        { NodePath = "GoodNode"
          EndpointName = "Endpoint" }

    let handler =
        new CallbackMockHandler(fun _ ->
            let n = Interlocked.Increment(callCount)

            if n % 2 = 1 then
                // Odd calls = first endpoint (error)
                makeResponse HttpStatusCode.InternalServerError "fail"
            else
                // Even calls = second endpoint (success)
                makeResponse HttpStatusCode.OK (valueJson "\"ok\""))

    let client = new HttpClient(handler)

    let config =
        fastConfig changes.Add (fun addr err -> errors.Add(addr, err))

    use sub = create client testConfig config
    sub.Add addr1
    sub.Add addr2
    Thread.Sleep(200)

    Assert.True(errors.Count >= 1, "Should have received errors for bad endpoint")
    Assert.True(changes.Count >= 1, "Should have received changes for good endpoint")

// ── Multiple endpoints ──

[<Fact>]
let ``multiple endpoints are polled and changes fire for each`` () =
    let changes = ResizeArray<ValueChange>()

    let addr1 =
        { NodePath = "Node1"
          EndpointName = "Ep1" }

    let addr2 =
        { NodePath = "Node2"
          EndpointName = "Ep2" }

    let client = constantClient (valueJson "\"hello\"")
    let config = fastConfig changes.Add (fun _ _ -> ())

    use sub = create client testConfig config
    sub.Add addr1
    sub.Add addr2
    Thread.Sleep(200)

    let addr1Changes = changes |> Seq.filter (fun c -> c.Address = addr1) |> Seq.length
    let addr2Changes = changes |> Seq.filter (fun c -> c.Address = addr2) |> Seq.length
    Assert.True(addr1Changes >= 1, $"Expected changes for addr1, got {addr1Changes}")
    Assert.True(addr2Changes >= 1, $"Expected changes for addr2, got {addr2Changes}")

// ── Endpoints property ──

[<Fact>]
let ``Endpoints returns empty list initially`` () =
    let client = constantClient (valueJson "1")
    let config = fastConfig (fun _ -> ()) (fun _ _ -> ())

    use sub = create client testConfig config
    Assert.Empty(sub.Endpoints)

[<Fact>]
let ``Endpoints reflects added addresses`` () =
    let client = constantClient (valueJson "1")
    let config = fastConfig (fun _ -> ()) (fun _ _ -> ())

    use sub = create client testConfig config
    sub.Add testAddress
    Assert.Equal(1, sub.Endpoints.Length)
    Assert.Equal(testAddress, sub.Endpoints[0])

[<Fact>]
let ``Endpoints updates after Remove`` () =
    let client = constantClient (valueJson "1")
    let config = fastConfig (fun _ -> ()) (fun _ _ -> ())

    use sub = create client testConfig config
    sub.Add testAddress
    Assert.Equal(1, sub.Endpoints.Length)
    sub.Remove testAddress
    Assert.Empty(sub.Endpoints)

// ── IsActive ──

[<Fact>]
let ``IsActive is true after creation`` () =
    let client = constantClient (valueJson "1")
    let config = fastConfig (fun _ -> ()) (fun _ _ -> ())

    use sub = create client testConfig config
    Assert.True(sub.IsActive)
