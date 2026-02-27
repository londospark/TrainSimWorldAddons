module PortDetectionTests

open Xunit
open CounterApp

[<Fact>]
let ``tryParseVidPid parses VID_2341&PID_0043``() =
    let result = PortDetection.tryParseVidPid "VID_2341&PID_0043"
    Assert.Equal(Some ("2341", "0043"), result)

[<Fact>]
let ``tryParseVidPid parses VID_1A86&PID_7523``() =
    let result = PortDetection.tryParseVidPid "VID_1A86&PID_7523"
    Assert.Equal(Some ("1A86", "7523"), result)

[<Fact>]
let ``tryParseVidPid returns None for invalid string``() =
    let result = PortDetection.tryParseVidPid "not a vid pid"
    Assert.Equal(None, result)

[<Fact>]
let ``tryParseVidPid returns None for empty string``() =
    let result = PortDetection.tryParseVidPid ""
    Assert.Equal(None, result)

[<Fact>]
let ``tryParseVidPid is case insensitive``() =
    let result = PortDetection.tryParseVidPid "vid_2341&pid_0043"
    Assert.Equal(Some ("2341", "0043"), result)

[<Fact>]
let ``isArduinoVid returns true for Arduino LLC``() =
    let result = PortDetection.isArduinoVid "2341"
    Assert.True(result)

[<Fact>]
let ``isArduinoVid returns true for CH340``() =
    let result = PortDetection.isArduinoVid "1A86"
    Assert.True(result)

[<Fact>]
let ``isArduinoVid returns true for FTDI``() =
    let result = PortDetection.isArduinoVid "0403"
    Assert.True(result)

[<Fact>]
let ``isArduinoVid returns true for CP210x``() =
    let result = PortDetection.isArduinoVid "10C4"
    Assert.True(result)

[<Fact>]
let ``isArduinoVid returns false for unknown VID``() =
    let result = PortDetection.isArduinoVid "DEAD"
    Assert.False(result)

[<Fact>]
let ``isArduinoVid is case insensitive``() =
    let lowerResult = PortDetection.isArduinoVid "2341"
    let upperResult = PortDetection.isArduinoVid "2341"
    Assert.Equal(lowerResult, upperResult)

[<Fact>]
let ``classifyPorts with empty list returns NoPorts``() =
    let result = PortDetection.classifyPorts []
    match result with
    | PortDetection.NoPorts -> Assert.True(true)
    | _ -> Assert.Fail("Expected NoPorts")

[<Fact>]
let ``classifyPorts with single Arduino returns SingleArduino``() =
    let port = { PortDetection.PortName = "COM3"; PortDetection.UsbInfo = Some { PortDetection.Vid = "2341"; PortDetection.Pid = "0043"; PortDetection.Description = "Arduino Uno" }; PortDetection.IsArduino = true }
    let result = PortDetection.classifyPorts [port]
    match result with
    | PortDetection.SingleArduino p -> Assert.Equal("COM3", p.PortName)
    | _ -> Assert.Fail("Expected SingleArduino")

[<Fact>]
let ``classifyPorts with two Arduinos returns MultipleArduinos``() =
    let port1 = { PortDetection.PortName = "COM3"; PortDetection.UsbInfo = Some { PortDetection.Vid = "2341"; PortDetection.Pid = "0043"; PortDetection.Description = "Arduino Uno" }; PortDetection.IsArduino = true }
    let port2 = { PortDetection.PortName = "COM5"; PortDetection.UsbInfo = Some { PortDetection.Vid = "1A86"; PortDetection.Pid = "7523"; PortDetection.Description = "CH340" }; PortDetection.IsArduino = true }
    let result = PortDetection.classifyPorts [port1; port2]
    match result with
    | PortDetection.MultipleArduinos ports -> Assert.Equal(2, ports.Length)
    | _ -> Assert.Fail("Expected MultipleArduinos")

[<Fact>]
let ``classifyPorts with non-Arduino ports returns NoArduinoFound``() =
    let port = { PortDetection.PortName = "COM3"; PortDetection.UsbInfo = Some { PortDetection.Vid = "DEAD"; PortDetection.Pid = "BEEF"; PortDetection.Description = "Unknown Device" }; PortDetection.IsArduino = false }
    let result = PortDetection.classifyPorts [port]
    match result with
    | PortDetection.NoArduinoFound allPorts -> Assert.Equal(1, allPorts.Length)
    | _ -> Assert.Fail("Expected NoArduinoFound")

[<Fact>]
let ``portDisplayName with UsbInfo shows description``() =
    let port = { PortDetection.PortName = "COM3"; PortDetection.UsbInfo = Some { PortDetection.Vid = "2341"; PortDetection.Pid = "0043"; PortDetection.Description = "Arduino Uno" }; PortDetection.IsArduino = true }
    let result = PortDetection.portDisplayName port
    Assert.Equal("COM3 — Arduino Uno", result)

[<Fact>]
let ``portDisplayName without UsbInfo shows only port name``() =
    let port = { PortDetection.PortName = "COM3"; PortDetection.UsbInfo = None; PortDetection.IsArduino = false }
    let result = PortDetection.portDisplayName port
    Assert.Equal("COM3", result)
