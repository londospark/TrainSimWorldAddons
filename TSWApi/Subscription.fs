namespace TSWApi

open System
open System.Collections.Generic
open System.Net.Http
open System.Threading
open TSWApi.Types
open TSWApi.ApiClient

/// Polling-based subscription module for TSW6 endpoint value changes.
/// Polls /get endpoints on a configurable timer, detects value changes,
/// and notifies consumers via callbacks. NOT related to TSW6's own
/// /subscription endpoints — this is our own polling layer.
module Subscription =

    /// Identifies a specific endpoint in the TSW node tree.
    type EndpointAddress =
        { /// The node path (e.g., "CurrentDrivableActor/BP_AWS_TPWS_Service").
          NodePath: string
          /// The endpoint name (e.g., "Property.AWS_SunflowerState").
          EndpointName: string }

    /// Construct the full API path for a /get request from an address.
    let endpointPath (addr: EndpointAddress) =
        $"{addr.NodePath}.{addr.EndpointName}"

    /// A detected value change on a subscribed endpoint.
    type ValueChange =
        { /// The endpoint that changed.
          Address: EndpointAddress
          /// The previous value (None on first poll).
          OldValue: string option
          /// The current value.
          NewValue: string }

    /// Configuration for a subscription instance.
    type SubscriptionConfig =
        { /// Polling interval. Default: 200ms.
          Interval: TimeSpan
          /// Called when an endpoint value changes. Fires on timer thread — consumer
          /// must marshal to UI thread if needed.
          OnChange: ValueChange -> unit
          /// Called when polling an endpoint fails. Does not stop the subscription.
          OnError: EndpointAddress -> ApiError -> unit }

    /// Default configuration: 200ms interval, no-op callbacks.
    let defaultConfig =
        { Interval = TimeSpan.FromMilliseconds(200.0)
          OnChange = fun _ -> ()
          OnError = fun _ _ -> () }

    /// A live subscription handle. Dispose to stop polling.
    type ISubscription =
        inherit IDisposable
        /// Add an endpoint to poll. Idempotent.
        abstract Add: EndpointAddress -> unit
        /// Remove an endpoint from polling. Idempotent.
        abstract Remove: EndpointAddress -> unit
        /// Currently subscribed endpoint addresses.
        abstract Endpoints: EndpointAddress list
        /// Whether the subscription is actively polling.
        abstract IsActive: bool

    /// Internal tracked state for a single endpoint.
    type internal EndpointState =
        { Address: EndpointAddress
          mutable LastValue: string option }

    /// Create a new subscription that polls endpoints via the TSW API.
    /// Polling starts immediately on the ThreadPool. Dispose to stop.
    let create (client: HttpClient) (apiConfig: ApiConfig) (subConfig: SubscriptionConfig) : ISubscription =
        let lockObj = obj ()
        let endpoints = Dictionary<string, EndpointState>()
        let mutable disposed = false
        let mutable polling = 0

        let pollOnce () =
            if Interlocked.CompareExchange(&polling, 1, 0) = 0 then
                try
                    let snapshot =
                        lock lockObj (fun () -> endpoints.Values |> Seq.toList)

                    for state in snapshot do
                        if disposed then () // bail early on dispose
                        else
                            let path = endpointPath state.Address

                            let result =
                                getValue client apiConfig path |> Async.RunSynchronously

                            match result with
                            | Ok resp ->
                                let newValue =
                                    if resp.Values.ContainsKey("Value") then
                                        string resp.Values["Value"]
                                    else
                                        ""

                                lock lockObj (fun () ->
                                    match state.LastValue with
                                    | Some old when old = newValue -> () // no change
                                    | oldVal ->
                                        state.LastValue <- Some newValue

                                        subConfig.OnChange
                                            { Address = state.Address
                                              OldValue = oldVal
                                              NewValue = newValue })
                            | Error err -> subConfig.OnError state.Address err
                finally
                    Interlocked.Exchange(&polling, 0) |> ignore

        let timer =
            new Timer((fun _ -> if not disposed then try pollOnce () with _ -> ()), null, TimeSpan.Zero, subConfig.Interval)

        { new ISubscription with
            member _.Add(addr) =
                let key = endpointPath addr

                lock lockObj (fun () ->
                    if not (endpoints.ContainsKey(key)) then
                        endpoints[key] <- { Address = addr; LastValue = None })

            member _.Remove(addr) =
                let key = endpointPath addr
                lock lockObj (fun () -> endpoints.Remove(key) |> ignore)

            member _.Endpoints =
                lock lockObj (fun () -> endpoints.Values |> Seq.map _.Address |> Seq.toList)

            member _.IsActive = not disposed

            member _.Dispose() =
                if not disposed then
                    disposed <- true
                    timer.Change(Timeout.Infinite, Timeout.Infinite) |> ignore
                    timer.Dispose() }
