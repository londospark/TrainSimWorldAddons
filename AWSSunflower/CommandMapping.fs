namespace CounterApp

/// Translates API endpoint values into serial commands for Arduino addons.
/// Each addon defines its own command set. The mapping layer is type-safe,
/// extensible, and handles "no mapping" gracefully via Option.
module CommandMapping =

    // ─── Logical Actions ───

    /// A logical action that an addon can perform.
    /// This is the semantic layer — what does the value MEAN, not what string to send.
    [<RequireQualifiedAccess>]
    type Action =
        /// A binary on/off state changed to "on" (e.g., AWS sunflower showing).
        | Activate
        /// A binary on/off state changed to "off" (e.g., AWS sunflower hidden).
        | Deactivate
        /// A continuous value changed (e.g., throttle position 0.0–1.0).
        | SetValue of float
        /// A momentary pulse (e.g., horn button pressed — send once, no "off" equivalent).
        | Pulse

    // ─── Value Interpretation ───

    /// Defines how to interpret an API value string as a logical Action.
    /// Different endpoints have different value semantics.
    [<RequireQualifiedAccess>]
    type ValueInterpreter =
        /// Boolean-like: "1"/"True"/"true" → Activate, "0"/"False"/"false" → Deactivate.
        /// Exact match only — no substring matching.
        | Boolean
        /// Numeric float: parse as float, map to SetValue.
        /// Falls back to None if not a valid float.
        | Continuous
        /// Enum-like: map specific string values to specific Actions.
        /// Any value not in the map → None.
        | Mapped of Map<string, Action>
        /// Custom function for complex interpretation logic.
        | Custom of (string -> Action option)

    // ─── Serial Command ───

    /// A concrete serial command to send to the Arduino.
    [<RequireQualifiedAccess>]
    type SerialCommand =
        /// A simple string command (e.g., "s", "c", "t1").
        | Text of string
        /// A formatted value command (e.g., "T:0.75\n" for throttle at 75%).
        | Formatted of string

    // ─── Endpoint Mapping ───

    /// Mapping for a single endpoint: how to interpret its value and what command to produce.
    type EndpointMapping =
        { /// Which endpoint this mapping applies to (matches BoundEndpoint.EndpointName).
          EndpointName: string
          /// How to interpret the raw API value string.
          Interpreter: ValueInterpreter
          /// Maps a logical Action to a serial command.
          /// Returns None if this Action should not produce a serial command.
          ToCommand: Action -> SerialCommand option }

    // ─── Addon Command Set ───

    /// A complete command set for an addon.
    type AddonCommandSet =
        { /// Human-readable addon name (e.g., "AWS Sunflower", "TPWS").
          Name: string
          /// Endpoint mappings for this addon. Keyed by endpoint name for O(1) lookup.
          Mappings: Map<string, EndpointMapping>
          /// Command to send on reset/cleanup (loco change, unbind, disconnect).
          /// None if the addon has no reset behavior.
          ResetCommand: SerialCommand option }

    // ─── Core Functions ───

    /// Interpret a raw API value string using the given interpreter.
    /// Returns None if the value doesn't map to any action.
    let interpret (interpreter: ValueInterpreter) (value: string) : Action option =
        match interpreter with
        | ValueInterpreter.Boolean ->
            let trimmed = value.Trim()
            match trimmed with
            | "1" | "True" | "true" -> Some Action.Activate
            | "0" | "False" | "false" -> Some Action.Deactivate
            | _ -> None
        | ValueInterpreter.Continuous ->
            match System.Double.TryParse(value.Trim()) with
            | true, v -> Some (Action.SetValue v)
            | false, _ -> None
        | ValueInterpreter.Mapped map ->
            Map.tryFind (value.Trim()) map
        | ValueInterpreter.Custom fn ->
            fn value

    /// Translate an API value change into a serial command.
    /// Looks up the endpoint in the addon's mappings, interprets the value,
    /// then maps the action to a command. Returns None at any stage if
    /// no mapping exists.
    let translate (addon: AddonCommandSet) (endpointName: string) (value: string) : SerialCommand option =
        addon.Mappings
        |> Map.tryFind endpointName
        |> Option.bind (fun mapping ->
            interpret mapping.Interpreter value
            |> Option.bind mapping.ToCommand)

    /// Get the reset command for an addon (if any).
    let resetCommand (addon: AddonCommandSet) : SerialCommand option =
        addon.ResetCommand

    /// Convert a SerialCommand to the raw string to send over the wire.
    let toWireString : SerialCommand -> string = function
        | SerialCommand.Text s | SerialCommand.Formatted s -> s

    // ─── Concrete Addon Definitions ───

    /// The AWS Sunflower addon command set.
    /// Sends "s" when the sunflower activates, "c" when it deactivates.
    module AWSSunflowerCommands =

        let commandSet : AddonCommandSet =
            { Name = "AWS Sunflower"
              Mappings =
                [ "Property.AWS_SunflowerState",
                  { EndpointName = "Property.AWS_SunflowerState"
                    Interpreter = ValueInterpreter.Boolean
                    ToCommand = function
                        | Action.Activate -> Some (SerialCommand.Text "s")
                        | Action.Deactivate -> Some (SerialCommand.Text "c")
                        | _ -> None } ]
                |> Map.ofList
              ResetCommand = Some (SerialCommand.Text "c") }
