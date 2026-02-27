namespace CounterApp.Tests

open Xunit
open CounterApp
open CounterApp.CommandMapping

module CommandMappingTests =

    // ─── interpret with ValueInterpreter.Boolean ───

    [<Fact>]
    let ``interpret Boolean with "1" returns Some Activate``() =
        let result = interpret ValueInterpreter.Boolean "1"
        Assert.Equal(Some Action.Activate, result)

    [<Fact>]
    let ``interpret Boolean with "True" returns Some Activate``() =
        let result = interpret ValueInterpreter.Boolean "True"
        Assert.Equal(Some Action.Activate, result)

    [<Fact>]
    let ``interpret Boolean with "true" returns Some Activate``() =
        let result = interpret ValueInterpreter.Boolean "true"
        Assert.Equal(Some Action.Activate, result)

    [<Fact>]
    let ``interpret Boolean with "0" returns Some Deactivate``() =
        let result = interpret ValueInterpreter.Boolean "0"
        Assert.Equal(Some Action.Deactivate, result)

    [<Fact>]
    let ``interpret Boolean with "False" returns Some Deactivate``() =
        let result = interpret ValueInterpreter.Boolean "False"
        Assert.Equal(Some Action.Deactivate, result)

    [<Fact>]
    let ``interpret Boolean with "false" returns Some Deactivate``() =
        let result = interpret ValueInterpreter.Boolean "false"
        Assert.Equal(Some Action.Deactivate, result)

    [<Fact>]
    let ``interpret Boolean with random string returns None``() =
        let result = interpret ValueInterpreter.Boolean "randomstring"
        Assert.Equal(None, result)

    [<Fact>]
    let ``interpret Boolean with whitespace-padded "1" returns Some Activate``() =
        let result = interpret ValueInterpreter.Boolean " 1 "
        Assert.Equal(Some Action.Activate, result)

    [<Fact>]
    let ``interpret Boolean with whitespace-padded "0" returns Some Deactivate``() =
        let result = interpret ValueInterpreter.Boolean " 0 "
        Assert.Equal(Some Action.Deactivate, result)

    // ─── interpret with ValueInterpreter.Continuous ───

    [<Fact>]
    let ``interpret Continuous with "0.5" returns Some SetValue 0.5``() =
        let result = interpret ValueInterpreter.Continuous "0.5"
        Assert.Equal(Some (Action.SetValue 0.5), result)

    [<Fact>]
    let ``interpret Continuous with "1.0" returns Some SetValue 1.0``() =
        let result = interpret ValueInterpreter.Continuous "1.0"
        Assert.Equal(Some (Action.SetValue 1.0), result)

    [<Fact>]
    let ``interpret Continuous with "not a number" returns None``() =
        let result = interpret ValueInterpreter.Continuous "not a number"
        Assert.Equal(None, result)

    [<Fact>]
    let ``interpret Continuous with whitespace-padded "0.75" returns Some SetValue 0.75``() =
        let result = interpret ValueInterpreter.Continuous " 0.75 "
        Assert.Equal(Some (Action.SetValue 0.75), result)

    // ─── interpret with ValueInterpreter.Mapped ───

    [<Fact>]
    let ``interpret Mapped with known key returns Some mapped action``() =
        let mapping = Map.ofList [("key1", Action.Pulse); ("key2", Action.Activate)]
        let result = interpret (ValueInterpreter.Mapped mapping) "key1"
        Assert.Equal(Some Action.Pulse, result)

    [<Fact>]
    let ``interpret Mapped with unknown key returns None``() =
        let mapping = Map.ofList [("key1", Action.Pulse); ("key2", Action.Activate)]
        let result = interpret (ValueInterpreter.Mapped mapping) "unknownkey"
        Assert.Equal(None, result)

    [<Fact>]
    let ``interpret Mapped with whitespace-padded key returns Some mapped action``() =
        let mapping = Map.ofList [("key1", Action.Pulse); ("key2", Action.Activate)]
        let result = interpret (ValueInterpreter.Mapped mapping) " key2 "
        Assert.Equal(Some Action.Activate, result)

    // ─── interpret with ValueInterpreter.Custom ───

    [<Fact>]
    let ``interpret Custom with fn returning Some returns Some``() =
        let customFn (value: string) = if value = "special" then Some Action.Pulse else None
        let result = interpret (ValueInterpreter.Custom customFn) "special"
        Assert.Equal(Some Action.Pulse, result)

    [<Fact>]
    let ``interpret Custom with fn returning None returns None``() =
        let customFn (value: string) = if value = "special" then Some Action.Pulse else None
        let result = interpret (ValueInterpreter.Custom customFn) "notspecial"
        Assert.Equal(None, result)

    // ─── translate ───

    let testMapping: EndpointMapping =
        { EndpointName = "Property.TestEndpoint"
          Interpreter = ValueInterpreter.Boolean
          ToCommand = function
              | Action.Activate -> Some (SerialCommand.Text "on")
              | Action.Deactivate -> Some (SerialCommand.Text "off")
              | _ -> None }

    let testCommandSet: AddonCommandSet =
        { Name = "Test Addon"
          Mappings = Map.ofList [("Property.TestEndpoint", testMapping)]
          ResetCommand = Some (SerialCommand.Text "reset") }

    [<Fact>]
    let ``translate known endpoint with valid value returns Some SerialCommand``() =
        let result = translate testCommandSet "Property.TestEndpoint" "1"
        Assert.Equal(Some (SerialCommand.Text "on"), result)

    [<Fact>]
    let ``translate unknown endpoint returns None``() =
        let result = translate testCommandSet "Property.UnknownEndpoint" "1"
        Assert.Equal(None, result)

    [<Fact>]
    let ``translate known endpoint with invalid value returns None``() =
        let result = translate testCommandSet "Property.TestEndpoint" "invalid"
        Assert.Equal(None, result)

    // ─── toWireString ───

    [<Fact>]
    let ``toWireString with Text returns string``() =
        let result = toWireString (SerialCommand.Text "s")
        Assert.Equal("s", result)

    [<Fact>]
    let ``toWireString with Formatted returns string``() =
        let result = toWireString (SerialCommand.Formatted "T:0.75\n")
        Assert.Equal("T:0.75\n", result)

    // ─── resetCommand ───

    [<Fact>]
    let ``resetCommand with addon having ResetCommand returns Some``() =
        let result = resetCommand testCommandSet
        Assert.Equal(Some (SerialCommand.Text "reset"), result)

    [<Fact>]
    let ``resetCommand with addon without ResetCommand returns None``() =
        let addonWithoutReset = { testCommandSet with ResetCommand = None }
        let result = resetCommand addonWithoutReset
        Assert.Equal(None, result)

    // ─── AWSSunflowerCommands.commandSet ───

    [<Fact>]
    let ``AWSSunflowerCommands commandSet translates "1" to Text "s"``() =
        let result = translate AWSSunflowerCommands.commandSet "Property.AWS_SunflowerState" "1"
        Assert.Equal(Some (SerialCommand.Text "s"), result)

    [<Fact>]
    let ``AWSSunflowerCommands commandSet translates "0" to Text "c"``() =
        let result = translate AWSSunflowerCommands.commandSet "Property.AWS_SunflowerState" "0"
        Assert.Equal(Some (SerialCommand.Text "c"), result)

    [<Fact>]
    let ``AWSSunflowerCommands commandSet translates unknown endpoint to None``() =
        let result = translate AWSSunflowerCommands.commandSet "Property.Unknown" "1"
        Assert.Equal(None, result)

    [<Fact>]
    let ``AWSSunflowerCommands commandSet has reset command Text "c"``() =
        let result = resetCommand AWSSunflowerCommands.commandSet
        Assert.Equal(Some (SerialCommand.Text "c"), result)
