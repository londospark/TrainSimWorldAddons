namespace CounterApp

open System
open System.IO
open System.Text.Json
open System.Text.Json.Serialization
open Microsoft.Data.Sqlite

module BindingPersistence =
    let private configDir =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LondoSpark", "AWSSunflower")

    let private dbPath = Path.Combine(configDir, "bindings.db")
    let private jsonPath = Path.Combine(configDir, "bindings.json")
    let private connectionString = $"Data Source={dbPath}"

    let private openConnection () =
        let conn = new SqliteConnection(connectionString)
        conn.Open()
        // Enable foreign keys
        use fkCmd = conn.CreateCommand()
        fkCmd.CommandText <- "PRAGMA foreign_keys = ON;"
        fkCmd.ExecuteNonQuery() |> ignore
        conn

    let private ensureSchema (conn: SqliteConnection) =
        use cmd = conn.CreateCommand()
        cmd.CommandText <- """
            CREATE TABLE IF NOT EXISTS Locos (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                loco_name TEXT NOT NULL UNIQUE
            );
            CREATE TABLE IF NOT EXISTS BoundEndpoints (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                loco_id INTEGER NOT NULL,
                node_path TEXT NOT NULL,
                endpoint_name TEXT NOT NULL,
                label TEXT NOT NULL,
                FOREIGN KEY (loco_id) REFERENCES Locos(id) ON DELETE CASCADE
            );
        """
        cmd.ExecuteNonQuery() |> ignore

    let private insertEndpoint (conn: SqliteConnection) (locoId: int64) (ep: BoundEndpoint) =
        use cmd = conn.CreateCommand()
        cmd.CommandText <- """
            INSERT INTO BoundEndpoints (loco_id, node_path, endpoint_name, label)
            VALUES (@locoId, @nodePath, @endpointName, @label);"""
        cmd.Parameters.AddWithValue("@locoId", locoId) |> ignore
        cmd.Parameters.AddWithValue("@nodePath", ep.NodePath) |> ignore
        cmd.Parameters.AddWithValue("@endpointName", ep.EndpointName) |> ignore
        cmd.Parameters.AddWithValue("@label", ep.Label) |> ignore
        cmd.ExecuteNonQuery() |> ignore

    let private migrateFromJson (conn: SqliteConnection) =
        if File.Exists(jsonPath) then
            try
                let jsonOptions =
                    let opts = JsonSerializerOptions(WriteIndented = true)
                    opts.Converters.Add(JsonFSharpConverter())
                    opts
                let json = File.ReadAllText(jsonPath)
                let config = JsonSerializer.Deserialize<BindingsConfig>(json, jsonOptions)
                for loco in config.Locos do
                    use insertLoco = conn.CreateCommand()
                    insertLoco.CommandText <- "INSERT OR IGNORE INTO Locos (loco_name) VALUES (@name);"
                    insertLoco.Parameters.AddWithValue("@name", loco.LocoName) |> ignore
                    insertLoco.ExecuteNonQuery() |> ignore

                    use getLocoId = conn.CreateCommand()
                    getLocoId.CommandText <- "SELECT id FROM Locos WHERE loco_name = @name;"
                    getLocoId.Parameters.AddWithValue("@name", loco.LocoName) |> ignore
                    let locoId = getLocoId.ExecuteScalar() :?> int64

                    for ep in loco.BoundEndpoints do
                        insertEndpoint conn locoId ep
            with ex ->
                eprintfn "[Persistence] JSON migration failed: %s" ex.Message

    let private ensureInitialized =
        let mutable initialized = false
        let lockObj = obj()
        fun () ->
            if not initialized then
                lock lockObj (fun () ->
                    if not initialized then
                        Directory.CreateDirectory(configDir) |> ignore
                        let dbExisted = File.Exists(dbPath)
                        use conn = openConnection()
                        ensureSchema conn
                        if not dbExisted then migrateFromJson conn
                        initialized <- true)

    let private readAllFromDb (conn: SqliteConnection) : BindingsConfig =
        use cmd = conn.CreateCommand()
        cmd.CommandText <- """
            SELECT l.loco_name, b.node_path, b.endpoint_name, b.label
            FROM Locos l
            LEFT JOIN BoundEndpoints b ON b.loco_id = l.id
            ORDER BY l.id, b.id;
        """
        use reader = cmd.ExecuteReader()
        let locoMap = System.Collections.Generic.Dictionary<string, ResizeArray<BoundEndpoint>>()
        let locoOrder = ResizeArray<string>()
        while reader.Read() do
            let locoName = reader.GetString(0)
            if not (locoMap.ContainsKey locoName) then
                locoOrder.Add(locoName)
                locoMap[locoName] <- ResizeArray<BoundEndpoint>()
            if not (reader.IsDBNull(1)) then
                locoMap[locoName].Add({
                    NodePath = reader.GetString(1)
                    EndpointName = reader.GetString(2)
                    Label = reader.GetString(3)
                })
        { Version = 1
          Locos = locoOrder |> Seq.map (fun name -> { LocoName = name; BoundEndpoints = locoMap[name] |> Seq.toList }) |> Seq.toList }

    let load () : BindingsConfig =
        try
            ensureInitialized()
            use conn = openConnection()
            readAllFromDb conn
        with ex ->
            eprintfn "[Persistence] Failed to load: %s" ex.Message
            { Version = 1; Locos = [] }

    let save (config: BindingsConfig) =
        try
            ensureInitialized()
            use conn = openConnection()
            use tx = conn.BeginTransaction()
            // Full replace: clear and reinsert
            use delEp = conn.CreateCommand()
            delEp.CommandText <- "DELETE FROM BoundEndpoints;"
            delEp.ExecuteNonQuery() |> ignore
            use delLoco = conn.CreateCommand()
            delLoco.CommandText <- "DELETE FROM Locos;"
            delLoco.ExecuteNonQuery() |> ignore

            for loco in config.Locos do
                use insertLoco = conn.CreateCommand()
                insertLoco.CommandText <- "INSERT INTO Locos (loco_name) VALUES (@name);"
                insertLoco.Parameters.AddWithValue("@name", loco.LocoName) |> ignore
                insertLoco.ExecuteNonQuery() |> ignore

                use getLocoId = conn.CreateCommand()
                getLocoId.CommandText <- "SELECT last_insert_rowid();"
                let locoId = getLocoId.ExecuteScalar() :?> int64

                for ep in loco.BoundEndpoints do
                    insertEndpoint conn locoId ep

            tx.Commit()
        with ex ->
            eprintfn "[Persistence] Failed to save: %s" ex.Message

    let addBinding (config: BindingsConfig) (locoName: string) (binding: BoundEndpoint) : BindingsConfig =
        let updatedLocos =
            match config.Locos |> List.tryFind (fun l -> l.LocoName = locoName) with
            | Some loco ->
                let alreadyBound =
                    loco.BoundEndpoints
                    |> List.exists (fun e -> e.NodePath = binding.NodePath && e.EndpointName = binding.EndpointName)
                if alreadyBound then config.Locos
                else
                    config.Locos |> List.map (fun l ->
                        if l.LocoName = locoName then { l with BoundEndpoints = l.BoundEndpoints @ [binding] }
                        else l)
            | None ->
                config.Locos @ [{ LocoName = locoName; BoundEndpoints = [binding] }]
        { config with Locos = updatedLocos }

    let removeBinding (config: BindingsConfig) (locoName: string) (nodePath: string) (endpointName: string) : BindingsConfig =
        let updatedLocos =
            config.Locos |> List.map (fun l ->
                if l.LocoName = locoName then
                    { l with BoundEndpoints = l.BoundEndpoints |> List.filter (fun e -> not (e.NodePath = nodePath && e.EndpointName = endpointName)) }
                else l)
        { config with Locos = updatedLocos }
