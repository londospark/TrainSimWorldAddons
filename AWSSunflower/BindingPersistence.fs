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
                        use insertEp = conn.CreateCommand()
                        insertEp.CommandText <- """
                            INSERT INTO BoundEndpoints (loco_id, node_path, endpoint_name, label)
                            VALUES (@locoId, @nodePath, @endpointName, @label);
                        """
                        insertEp.Parameters.AddWithValue("@locoId", locoId) |> ignore
                        insertEp.Parameters.AddWithValue("@nodePath", ep.NodePath) |> ignore
                        insertEp.Parameters.AddWithValue("@endpointName", ep.EndpointName) |> ignore
                        insertEp.Parameters.AddWithValue("@label", ep.Label) |> ignore
                        insertEp.ExecuteNonQuery() |> ignore
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
        let mutable locoMap: Map<string, BoundEndpoint list> = Map.empty
        let mutable locoOrder: string list = []
        while reader.Read() do
            let locoName = reader.GetString(0)
            if not (locoMap.ContainsKey locoName) then
                locoOrder <- locoOrder @ [locoName]
                locoMap <- locoMap |> Map.add locoName []
            if not (reader.IsDBNull(1)) then
                let ep = {
                    NodePath = reader.GetString(1)
                    EndpointName = reader.GetString(2)
                    Label = reader.GetString(3)
                }
                locoMap <- locoMap |> Map.add locoName (locoMap.[locoName] @ [ep])
        { Version = 1
          Locos = locoOrder |> List.map (fun name -> { LocoName = name; BoundEndpoints = locoMap.[name] }) }

    let load () : BindingsConfig =
        try
            ensureInitialized()
            use conn = openConnection()
            readAllFromDb conn
        with _ ->
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
                    use insertEp = conn.CreateCommand()
                    insertEp.CommandText <- """
                        INSERT INTO BoundEndpoints (loco_id, node_path, endpoint_name, label)
                        VALUES (@locoId, @nodePath, @endpointName, @label);
                    """
                    insertEp.Parameters.AddWithValue("@locoId", locoId) |> ignore
                    insertEp.Parameters.AddWithValue("@nodePath", ep.NodePath) |> ignore
                    insertEp.Parameters.AddWithValue("@endpointName", ep.EndpointName) |> ignore
                    insertEp.Parameters.AddWithValue("@label", ep.Label) |> ignore
                    insertEp.ExecuteNonQuery() |> ignore

            tx.Commit()
        with ex ->
            eprintfn "[Persistence] Failed to save: %s" ex.Message

    let addBinding (config: BindingsConfig) (locoName: string) (binding: BoundEndpoint) : BindingsConfig =
        try
            ensureInitialized()
            use conn = openConnection()
            use tx = conn.BeginTransaction()

            // Ensure loco exists
            use insertLoco = conn.CreateCommand()
            insertLoco.CommandText <- "INSERT OR IGNORE INTO Locos (loco_name) VALUES (@name);"
            insertLoco.Parameters.AddWithValue("@name", locoName) |> ignore
            insertLoco.ExecuteNonQuery() |> ignore

            use getLocoId = conn.CreateCommand()
            getLocoId.CommandText <- "SELECT id FROM Locos WHERE loco_name = @name;"
            getLocoId.Parameters.AddWithValue("@name", locoName) |> ignore
            let locoId = getLocoId.ExecuteScalar() :?> int64

            // Check for duplicate
            use checkDup = conn.CreateCommand()
            checkDup.CommandText <- """
                SELECT COUNT(*) FROM BoundEndpoints
                WHERE loco_id = @locoId AND node_path = @nodePath AND endpoint_name = @endpointName;
            """
            checkDup.Parameters.AddWithValue("@locoId", locoId) |> ignore
            checkDup.Parameters.AddWithValue("@nodePath", binding.NodePath) |> ignore
            checkDup.Parameters.AddWithValue("@endpointName", binding.EndpointName) |> ignore
            let count = checkDup.ExecuteScalar() :?> int64

            if count = 0L then
                use insertEp = conn.CreateCommand()
                insertEp.CommandText <- """
                    INSERT INTO BoundEndpoints (loco_id, node_path, endpoint_name, label)
                    VALUES (@locoId, @nodePath, @endpointName, @label);
                """
                insertEp.Parameters.AddWithValue("@locoId", locoId) |> ignore
                insertEp.Parameters.AddWithValue("@nodePath", binding.NodePath) |> ignore
                insertEp.Parameters.AddWithValue("@endpointName", binding.EndpointName) |> ignore
                insertEp.Parameters.AddWithValue("@label", binding.Label) |> ignore
                insertEp.ExecuteNonQuery() |> ignore

            tx.Commit()
            readAllFromDb conn
        with _ ->
            config

    let removeBinding (config: BindingsConfig) (locoName: string) (nodePath: string) (endpointName: string) : BindingsConfig =
        try
            ensureInitialized()
            use conn = openConnection()
            use cmd = conn.CreateCommand()
            cmd.CommandText <- """
                DELETE FROM BoundEndpoints
                WHERE loco_id = (SELECT id FROM Locos WHERE loco_name = @locoName)
                  AND node_path = @nodePath
                  AND endpoint_name = @endpointName;
            """
            cmd.Parameters.AddWithValue("@locoName", locoName) |> ignore
            cmd.Parameters.AddWithValue("@nodePath", nodePath) |> ignore
            cmd.Parameters.AddWithValue("@endpointName", endpointName) |> ignore
            cmd.ExecuteNonQuery() |> ignore
            readAllFromDb conn
        with _ ->
            config
