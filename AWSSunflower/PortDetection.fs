namespace CounterApp

open System
open System.Text.RegularExpressions
open Microsoft.Win32

module PortDetection =

    /// Known USB Vendor IDs for Arduino-compatible boards.
    [<RequireQualifiedAccess>]
    module KnownVids =
        /// Arduino LLC (genuine boards: Uno, Mega, Leonardo, etc.)
        let [<Literal>] ArduinoLLC = "2341"
        /// WCH CH340 (common Chinese clone chip)
        let [<Literal>] CH340 = "1A86"
        /// FTDI (used in older Arduinos and many breakout boards)
        let [<Literal>] FTDI = "0403"
        /// Silicon Labs CP210x (used in some ESP32 dev boards)
        let [<Literal>] CP210x = "10C4"

        /// All VIDs we consider "Arduino-like".
        let all = [ ArduinoLLC; CH340; FTDI; CP210x ]

    /// USB identity information for a COM port device.
    type UsbDeviceInfo =
        { /// USB Vendor ID (e.g., "2341")
          Vid: string
          /// USB Product ID (e.g., "0043")
          Pid: string
          /// Human-readable device description from the registry (e.g., "Arduino Uno")
          Description: string }

    /// A detected COM port with optional USB identity.
    type DetectedPort =
        { /// COM port name (e.g., "COM3")
          PortName: string
          /// USB device info, if available. None for non-USB serial ports (built-in, Bluetooth, etc.)
          UsbInfo: UsbDeviceInfo option
          /// True if the VID matches a known Arduino-compatible vendor.
          IsArduino: bool }

    /// Result of scanning for Arduino devices.
    [<RequireQualifiedAccess>]
    type DetectionResult =
        /// Exactly one Arduino found — safe to auto-select.
        | SingleArduino of DetectedPort
        /// Multiple Arduinos found — user must choose.
        | MultipleArduinos of DetectedPort list
        /// No Arduinos found, but other ports exist.
        | NoArduinoFound of allPorts: DetectedPort list
        /// No COM ports at all.
        | NoPorts

    /// Parse VID and PID from a USB device ID string like "VID_2341&PID_0043".
    /// Returns Some (vid, pid) if successful, None otherwise.
    let tryParseVidPid (deviceId: string) : (string * string) option =
        let vidMatch = Regex.Match(deviceId, @"VID_([0-9A-Fa-f]{4})", RegexOptions.IgnoreCase)
        let pidMatch = Regex.Match(deviceId, @"PID_([0-9A-Fa-f]{4})", RegexOptions.IgnoreCase)
        if vidMatch.Success && pidMatch.Success then
            Some (vidMatch.Groups[1].Value.ToUpperInvariant(), pidMatch.Groups[1].Value.ToUpperInvariant())
        else
            None

    /// Check if a specific VID is in the known Arduino VID list (case-insensitive).
    let isArduinoVid (vid: string) : bool =
        KnownVids.all |> List.contains (vid.ToUpperInvariant())

    /// Registry path to USB device enumeration.
    let private usbEnumPath = @"SYSTEM\CurrentControlSet\Enum\USB"

    /// Try to extract a COM port mapping from a single device instance registry key.
    let private tryGetInstancePort (classKey: RegistryKey) (instance: string) (vid: string) (pid: string) : (string * UsbDeviceInfo) option =
        use instKey = classKey.OpenSubKey(instance, false)
        if isNull instKey then None
        else
            use paramsKey = instKey.OpenSubKey("Device Parameters", false)
            if isNull paramsKey then None
            else
                paramsKey.GetValue("PortName")
                |> Option.ofObj
                |> Option.map string
                |> Option.map (fun portName ->
                    let desc =
                        instKey.GetValue("FriendlyName")
                        |> Option.ofObj
                        |> Option.map string
                        |> Option.defaultWith (fun () ->
                            instKey.GetValue("DeviceDesc")
                            |> Option.ofObj
                            |> Option.map string
                            |> Option.defaultValue "Unknown USB Device")
                    portName, { Vid = vid; Pid = pid; Description = desc })

    /// Get all port mappings for a single USB device class (e.g., "VID_2341&PID_0043").
    let private tryGetPortMappings (usbKey: RegistryKey) (deviceClass: string) : (string * UsbDeviceInfo) seq =
        match tryParseVidPid deviceClass with
        | None -> Seq.empty
        | Some (vid, pid) ->
            use classKey = usbKey.OpenSubKey(deviceClass, false)
            if isNull classKey then Seq.empty
            else
                classKey.GetSubKeyNames()
                |> Seq.choose (fun instance -> tryGetInstancePort classKey instance vid pid)

    /// Get USB port mappings from Windows registry.
    /// Returns a map of COM port names to USB device info.
    let private getUsbPortMappings () : Map<string, UsbDeviceInfo> =
        try
            use usbKey = Registry.LocalMachine.OpenSubKey(usbEnumPath, false)
            if isNull usbKey then
                Map.empty
            else
                usbKey.GetSubKeyNames()
                |> Seq.collect (fun deviceClass -> tryGetPortMappings usbKey deviceClass)
                |> Map.ofSeq
        with
        | :? Security.SecurityException
        | :? UnauthorizedAccessException ->
            Map.empty  // Graceful degradation

    /// Detect all COM ports with USB device information.
    /// Uses Windows registry to match COM port names to VID/PID.
    /// Falls back to bare port listing if registry is inaccessible.
    let detectPorts () : DetectedPort list =
        let portNames = System.IO.Ports.SerialPort.GetPortNames() |> Array.toList
        let usbMap = getUsbPortMappings ()
        portNames |> List.map (fun name ->
            let usbInfo = Map.tryFind name usbMap
            { PortName = name
              UsbInfo = usbInfo
              IsArduino = usbInfo |> Option.map (fun u -> isArduinoVid u.Vid) |> Option.defaultValue false })

    /// Classify a list of detected ports into a DetectionResult.
    /// This is a pure function extracted for testability.
    let classifyPorts (ports: DetectedPort list) : DetectionResult =
        if ports.IsEmpty then
            DetectionResult.NoPorts
        else
            let arduinos = ports |> List.filter (fun p -> p.IsArduino)
            match arduinos with
            | [ single ] -> DetectionResult.SingleArduino single
            | [] -> DetectionResult.NoArduinoFound ports
            | multiple -> DetectionResult.MultipleArduinos multiple

    /// Scan for Arduino-compatible devices and return a detection result.
    let detectArduino () : DetectionResult =
        detectPorts () |> classifyPorts

    /// Format a port for display in the UI.
    /// Shows "COM3 — Arduino Uno" if USB info is available, otherwise just "COM3".
    let portDisplayName (port: DetectedPort) : string =
        port.UsbInfo
        |> Option.map (fun usb -> $"{port.PortName} — {usb.Description}")
        |> Option.defaultValue port.PortName
