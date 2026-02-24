namespace CounterApp

open System
open System.IO
open System.Text.Json
open System.Text.Json.Serialization

module BindingPersistence =
    let private configDir =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LondoSpark", "AWSSunflower")

    let private configPath = Path.Combine(configDir, "bindings.json")

    let private jsonOptions =
        let opts = JsonSerializerOptions(WriteIndented = true)
        opts.Converters.Add(JsonFSharpConverter())
        opts

    let load () : BindingsConfig =
        try
            if File.Exists(configPath) then
                let json = File.ReadAllText(configPath)
                JsonSerializer.Deserialize<BindingsConfig>(json, jsonOptions)
            else
                { Version = 1; Locos = [] }
        with _ ->
            { Version = 1; Locos = [] }

    let save (config: BindingsConfig) =
        try
            Directory.CreateDirectory(configDir) |> ignore
            let json = JsonSerializer.Serialize(config, jsonOptions)
            File.WriteAllText(configPath, json)
        with ex ->
            eprintfn "[Persistence] Failed to save: %s" ex.Message

    let addBinding (config: BindingsConfig) (locoName: string) (binding: BoundEndpoint) : BindingsConfig =
        let existingLoco = config.Locos |> List.tryFind (fun l -> l.LocoName = locoName)
        let updatedLoco =
            match existingLoco with
            | Some loco ->
                let alreadyBound = loco.BoundEndpoints |> List.exists (fun b -> b.NodePath = binding.NodePath && b.EndpointName = binding.EndpointName)
                if alreadyBound then loco
                else { loco with BoundEndpoints = loco.BoundEndpoints @ [binding] }
            | None ->
                { LocoName = locoName; BoundEndpoints = [binding] }
        let updatedLocos =
            match existingLoco with
            | Some _ -> config.Locos |> List.map (fun l -> if l.LocoName = locoName then updatedLoco else l)
            | None -> config.Locos @ [updatedLoco]
        { config with Locos = updatedLocos }

    let removeBinding (config: BindingsConfig) (locoName: string) (nodePath: string) (endpointName: string) : BindingsConfig =
        let updatedLocos =
            config.Locos |> List.map (fun l ->
                if l.LocoName = locoName then
                    { l with BoundEndpoints = l.BoundEndpoints |> List.filter (fun b -> not (b.NodePath = nodePath && b.EndpointName = endpointName)) }
                else l)
        { config with Locos = updatedLocos }
