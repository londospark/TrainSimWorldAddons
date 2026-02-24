#r "TSWApi/bin/Debug/net10.0/TSWApi.dll"
open System
open System.Net.Http
open TSWApi
open TSWApi.Types

printfn "=== Test 1: Direct Async.RunSynchronously (known working) ==="
let httpClient = new HttpClient()
let myGamesPath = 
    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
    |> fun docs -> IO.Path.Combine(docs, "My Games")

let keyResult = Http.discoverCommKey myGamesPath
printfn "  discoverCommKey: %A" (keyResult |> Result.map (fun k -> CommKey.value k |> fun s -> s.Substring(0, 8) + "..."))

match keyResult with
| Ok key ->
    let keyStr = CommKey.value key
    let configResult = Http.createConfigWithUrl "http://localhost:31270" keyStr
    printfn "  createConfigWithUrl: %s" (match configResult with Ok _ -> "Ok" | Error e -> sprintf "Error %A" e)
    
    match configResult with
    | Ok config ->
        printfn "  Calling getInfo..."
        let infoResult = ApiClient.getInfo httpClient config |> Async.RunSynchronously
        printfn "  getInfo: %s" (match infoResult with Ok i -> sprintf "Ok - %s" i.Meta.GameName | Error e -> sprintf "Error - %A" e)
    | Error e -> printfn "  Config error: %A" e
| Error e -> printfn "  Key error: %A" e

printfn ""
printfn "=== Test 2: Async.StartImmediate (mimics Avalonia) ==="
let mutable result = "not set"
let mutable completed = false

async {
    try
        let keyResult2 = Http.discoverCommKey myGamesPath
        match keyResult2 with
        | Ok key ->
            let keyStr = CommKey.value key
            match Http.createConfigWithUrl "http://localhost:31270" keyStr with
            | Ok config ->
                let! infoResult = ApiClient.getInfo httpClient config
                match infoResult with
                | Ok info -> result <- sprintf "SUCCESS: %s" info.Meta.GameName
                | Error e -> result <- sprintf "API ERROR: %A" e
            | Error e -> result <- sprintf "CONFIG ERROR: %A" e
        | Error e -> result <- sprintf "KEY ERROR: %A" e
    with ex ->
        result <- sprintf "EXCEPTION: %s" ex.Message
    completed <- true
} |> Async.StartImmediate

// Wait for completion
while not completed do
    System.Threading.Thread.Sleep(100)

printfn "  Result: %s" result

printfn ""
printfn "=== Test 3: Check HttpClient version behavior ==="
let req = new HttpRequestMessage(HttpMethod.Get, "http://localhost:31270/info")
printfn "  Default request version: %A" req.Version
req.Version <- Version(1, 1)
printfn "  After set: %A" req.Version
printfn "  HttpClient.DefaultRequestVersion: %A" httpClient.DefaultRequestVersion
