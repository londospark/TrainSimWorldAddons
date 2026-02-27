namespace TSWApi.Tests

open Xunit
open CounterApp.CommandMapping

module CommandMappingTests =

    let private testMapping =
        { EndpointName = "Property.TestEndpoint"
          Interpreter = ValueInterpreter.Boolean
          ToCommand = function
              | Action.Activate -> Some (SerialCommand.Text "s")
              | Action.Deactivate -> Some (SerialCommand.Text "c")
              | _ -> None }

    let private testCommandSet =
        { Name = "Test Addon"
          Mappings = Map.ofList [("Property.TestEndpoint", testMapping)]
          ResetCommand = Some (SerialCommand.Text "c") }

    [<Fact>]
    let ``interpret Boolean - "1" returns Activate`` () =
        let result = interpret ValueInterpreter.Boolean "1"
        Assert.Equal(Some Action.Activate, result)

    [<Fact>]
    let ``interpret Boolean - "0" returns Deactivate`` () =
        let result = interpret ValueInterpreter.Boolean "0"
        Assert.Equal(Some Action.Deactivate, result)

    [<Fact>]
    let ``interpret Boolean - "10" returns None (bug fix)`` () =
        let result = interpret ValueInterpreter.Boolean "10"
        Assert.Equal(None, result)

    [<Fact>]
    let ``interpret Boolean - "21" returns None (bug fix)`` () =
        let result = interpret ValueInterpreter.Boolean "21"
        Assert.Equal(None, result)

    [<Fact>]
    let ``interpret Boolean - "" returns None`` () =
        let result = interpret ValueInterpreter.Boolean ""
        Assert.Equal(None, result)

    [<Fact>]
    let ``interpret Boolean - "True" returns Activate`` () =
        let result = interpret ValueInterpreter.Boolean "True"
        Assert.Equal(Some Action.Activate, result)

    [<Fact>]
    let ``interpret Boolean - "true" returns Activate`` () =
        let result = interpret ValueInterpreter.Boolean "true"
        Assert.Equal(Some Action.Activate, result)

    [<Fact>]
    let ``interpret Boolean - "False" returns Deactivate`` () =
        let result = interpret ValueInterpreter.Boolean "False"
        Assert.Equal(Some Action.Deactivate, result)

    [<Fact>]
    let ``interpret Boolean - "false" returns Deactivate`` () =
        let result = interpret ValueInterpreter.Boolean "false"
        Assert.Equal(Some Action.Deactivate, result)

    [<Fact>]
    let ``interpret Continuous - "0.5" returns SetValue 0.5`` () =
        let result = interpret ValueInterpreter.Continuous "0.5"
        Assert.Equal(Some (Action.SetValue 0.5), result)

    [<Fact>]
    let ``interpret Continuous - "0.0" returns SetValue 0.0`` () =
        let result = interpret ValueInterpreter.Continuous "0.0"
        Assert.Equal(Some (Action.SetValue 0.0), result)

    [<Fact>]
    let ``interpret Continuous - "1.0" returns SetValue 1.0`` () =
        let result = interpret ValueInterpreter.Continuous "1.0"
        Assert.Equal(Some (Action.SetValue 1.0), result)

    [<Fact>]
    let ``interpret Continuous - "abc" returns None`` () =
        let result = interpret ValueInterpreter.Continuous "abc"
        Assert.Equal(None, result)

    [<Fact>]
    let ``interpret Continuous - "" returns None`` () =
        let result = interpret ValueInterpreter.Continuous ""
        Assert.Equal(None, result)

    [<Fact>]
    let ``interpret Mapped - known key returns correct Action`` () =
        let mapping = Map.ofList [("on", Action.Activate); ("off", Action.Deactivate)]
        let result = interpret (ValueInterpreter.Mapped mapping) "on"
        Assert.Equal(Some Action.Activate, result)

    [<Fact>]
    let ``interpret Mapped - unknown key returns None`` () =
        let mapping = Map.ofList [("on", Action.Activate); ("off", Action.Deactivate)]
        let result = interpret (ValueInterpreter.Mapped mapping) "unknown"
        Assert.Equal(None, result)

    [<Fact>]
    let ``translate - known endpoint with valid value returns Some command`` () =
        let result = translate testCommandSet "Property.TestEndpoint" "1"
        Assert.Equal(Some (SerialCommand.Text "s"), result)

    [<Fact>]
    let ``translate - known endpoint with invalid value returns None`` () =
        let result = translate testCommandSet "Property.TestEndpoint" "invalid"
        Assert.Equal(None, result)

    [<Fact>]
    let ``translate - unknown endpoint returns None`` () =
        let result = translate testCommandSet "Property.UnknownEndpoint" "1"
        Assert.Equal(None, result)

    [<Fact>]
    let ``toWireString - Text command returns raw string`` () =
        let cmd = SerialCommand.Text "s"
        let result = toWireString cmd
        Assert.Equal("s", result)

    [<Fact>]
    let ``toWireString - Formatted command returns raw string`` () =
        let cmd = SerialCommand.Formatted "T:0.75"
        let result = toWireString cmd
        Assert.Equal("T:0.75", result)

    [<Fact>]
    let ``resetCommand - addon with reset returns Some`` () =
        let commandSet =
            { Name = "Test Addon"
              Mappings = Map.empty
              ResetCommand = Some (SerialCommand.Text "c") }
        
        let result = resetCommand commandSet
        Assert.Equal(Some (SerialCommand.Text "c"), result)

    [<Fact>]
    let ``resetCommand - addon without reset returns None`` () =
        let commandSet =
            { Name = "Test Addon"
              Mappings = Map.empty
              ResetCommand = None }
        
        let result = resetCommand commandSet
        Assert.Equal(None, result)

    // Integration tests with AWSSunflowerCommands
    [<Fact>]
    let ``AWSSunflowerCommands - "1" on SunflowerState returns "s"`` () =
        let result = translate AWSSunflowerCommands.commandSet "Property.AWS_SunflowerState" "1"
        match result with
        | Some cmd -> Assert.Equal("s", toWireString cmd)
        | None -> Assert.Fail("Expected Some command, got None")

    [<Fact>]
    let ``AWSSunflowerCommands - "0" on SunflowerState returns "c"`` () =
        let result = translate AWSSunflowerCommands.commandSet "Property.AWS_SunflowerState" "0"
        match result with
        | Some cmd -> Assert.Equal("c", toWireString cmd)
        | None -> Assert.Fail("Expected Some command, got None")

    [<Fact>]
    let ``AWSSunflowerCommands - "10" on SunflowerState returns None (bug fix verification)`` () =
        let result = translate AWSSunflowerCommands.commandSet "Property.AWS_SunflowerState" "10"
        Assert.Equal(None, result)

    [<Fact>]
    let ``AWSSunflowerCommands - reset returns "c"`` () =
        let result = resetCommand AWSSunflowerCommands.commandSet
        match result with
        | Some cmd -> Assert.Equal("c", toWireString cmd)
        | None -> Assert.Fail("Expected Some command, got None")

    [<Fact>]
    let ``AWSSunflowerCommands - "True" on SunflowerState returns "s"`` () =
        let result = translate AWSSunflowerCommands.commandSet "Property.AWS_SunflowerState" "True"
        match result with
        | Some cmd -> Assert.Equal("s", toWireString cmd)
        | None -> Assert.Fail("Expected Some command, got None")

    [<Fact>]
    let ``AWSSunflowerCommands - "false" on SunflowerState returns "c"`` () =
        let result = translate AWSSunflowerCommands.commandSet "Property.AWS_SunflowerState" "false"
        match result with
        | Some cmd -> Assert.Equal("c", toWireString cmd)
        | None -> Assert.Fail("Expected Some command, got None")
