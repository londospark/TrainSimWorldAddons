namespace CounterApp

open System
open System.Net.Http
open TSWApi
open TSWApi.Subscription
open CounterApp.CommandMapping
open CounterApp.ApplicationScreen
open CounterApp.ApplicationScreenHelpers
open global.Elmish
open Avalonia.Threading

module ApplicationScreenCommands =

    // ─── Shared HttpClient & Subscription ───

    let internal httpClient = new HttpClient()
    let internal currentSubscription : ISubscription option ref = ref None

    /// Unwrap a Result, raising an exception on Error. Used inside Cmd.OfAsync.either
    /// where exceptions are caught and converted to error messages.
    let okOrFail (formatMsg: 'E -> string) (r: Result<'T, 'E>) : 'T =
        match r with
        | Ok v -> v
        | Error e -> failwith (formatMsg e)

    // ─── Async commands ───

    let timedApiCall (apiCall: Async<ApiResult<'T>>) : Async<'T * TimeSpan> =
        async {
            let startTime = DateTime.Now
            let! result = apiCall
            let elapsed = DateTime.Now - startTime
            return (result |> okOrFail (sprintf "API error: %A"), elapsed)
        }

    let connectCmd (baseUrl: string) (commKey: string) =
        Cmd.OfAsync.either
            (fun () ->
                async {
                    let keyValue =
                        if String.IsNullOrWhiteSpace(commKey) then
                            let myGamesPath =
                                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                                |> fun docs -> IO.Path.Combine(docs, "My Games")
                            TSWApi.Http.discoverCommKey myGamesPath
                            |> okOrFail (sprintf "CommKey discovery failed: %A")
                            |> CommKey.value
                        else commKey
                    let config =
                        TSWApi.Http.createConfigWithUrl baseUrl keyValue
                        |> okOrFail (sprintf "Invalid configuration: %A")
                    let! (info, elapsed) = timedApiCall (TSWApi.ApiClient.getInfo httpClient config)
                    return (keyValue, config, info, elapsed)
                })
            ()
            (fun (key, config, info, elapsed) -> ConnectSuccess(key, config, info, elapsed))
            (fun ex -> ConnectError ex.Message)

    let loadRootNodesCmd (config: ApiConfig) =
        Cmd.OfAsync.either
            (fun () ->
                async {
                    let! (listResp, elapsed) = timedApiCall (TSWApi.ApiClient.listNodes httpClient config None)
                    let nodes =
                        listResp.Nodes
                        |> Option.defaultValue []
                        |> List.map (mapNodeToTreeState "")
                    return (nodes, elapsed)
                })
            ()
            (fun (nodes, elapsed) -> RootNodesLoaded(nodes, elapsed))
            (fun ex -> RootNodesError ex.Message)

    let expandNodeCmd (config: ApiConfig) (nodePath: string) =
        Cmd.OfAsync.either
            (fun () ->
                async {
                    let! (listResp, elapsed) = timedApiCall (TSWApi.ApiClient.listNodes httpClient config (Some nodePath))
                    let children =
                        listResp.Nodes
                        |> Option.defaultValue []
                        |> List.map (mapNodeToTreeState nodePath)
                    return (nodePath, children, listResp.Endpoints, elapsed)
                })
            ()
            (fun (p, ch, eps, elapsed) -> NodeExpanded(p, ch, eps, elapsed))
            (fun ex -> ApiError ex.Message)

    let getValueCmd (config: ApiConfig) (endpointPath: string) =
        Cmd.OfAsync.either
            (fun () ->
                async {
                    let! (getResp, elapsed) = timedApiCall (TSWApi.ApiClient.getValue httpClient config endpointPath)
                    let valueStr =
                        if isNull (getResp.Values :> obj) || getResp.Values.Count = 0 then
                            "(no values returned)"
                        else
                            getResp.Values
                            |> Seq.map (fun kvp -> $"{kvp.Key}: {kvp.Value}")
                            |> String.concat ", "
                    return (endpointPath, valueStr, elapsed)
                })
            ()
            (fun (p, v, elapsed) -> EndpointValueReceived(p, v, elapsed))
            (fun ex -> ApiError ex.Message)

    let detectLocoCmd (config: ApiConfig) =
        Cmd.OfAsync.either
            (fun () ->
                async {
                    let! getResult = TSWApi.ApiClient.getValue httpClient config "CurrentDrivableActor.ObjectName"
                    let getResp = getResult |> okOrFail (sprintf "Detect loco failed: %A")
                    if isNull (getResp.Values :> obj) || getResp.Values.Count = 0 then
                        return failwith "No ObjectName returned"
                    else
                        return getResp.Values["ObjectName"] |> string
                })
            ()
            LocoDetected
            (fun ex -> LocoDetectError ex.Message)

    let createSubscriptionCmd (config: ApiConfig) (bindings: BoundEndpoint list) =
        Cmd.ofEffect (fun dispatch ->
            currentSubscription.Value |> Option.iter (fun s -> s.Dispose())
            let subConfig =
                { TSWApi.Subscription.defaultConfig with
                    Interval = TimeSpan.FromMilliseconds(200.0)
                    OnChange = fun vc ->
                        Dispatcher.UIThread.Post(fun () -> dispatch (EndpointChanged vc))
                    OnError = fun _ _ -> () }
            let sub = TSWApi.Subscription.create httpClient config subConfig
            for b in bindings do
                sub.Add { NodePath = b.NodePath; EndpointName = b.EndpointName }
            currentSubscription.Value <- Some sub
        )

    let disposeSubscription () =
        currentSubscription.Value |> Option.iter (fun s -> s.Dispose())
        currentSubscription.Value <- None

    let resetSerialCmd (model: Model) =
        model.ActiveAddon
        |> Option.bind (fun addon -> CommandMapping.resetCommand addon |> Option.map CommandMapping.toWireString)
        |> Option.map (fun wire -> Cmd.ofMsg (SendSerialCommand wire))
        |> Option.defaultValue Cmd.none
