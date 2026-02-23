# Objective: Build a library in F# that will make it easier to work with the Train Sim World 6 (TSW) API

## Phase 1: Working with GET requests only

The base url is http://localhost:31270 - this should be the default but overridable in case we are accessing TSW over a network rather than on the local computer. All requests will be relative to that

There is a header that must be sent with every request called `DTGCommKey` and the value of that is found in the file `"C:\Users\<USERNAME>\Documents\My Games\TrainSimWorld6\Saved\Config\CommAPIKey.txt"` - in general the TrainSimWorld6 directory will be the highest number of the TrainSimWorld directories in the `My Games` folder. On initialisation of the library we need to fetch that code and store it in memory so that we don't need to fetch it for every request.

### Some sample GET requests and responses:

/info =>

```json
{
  "Meta": {
    "Worker": "DTGCommWorkerRC",
    "GameName": "Train Sim World 6®",
    "GameBuildNumber": 749,
    "APIVersion": 1,
    "GameInstanceID": "A69D53564DFE46B7DE5AD7885CF0AA82"
  },
  "HttpRoutes": [
    {
      "Verb": "GET",
      "Path": "/info",
      "Description": "Get information about available commands."
    },
    {
      "Verb": "GET",
      "Path": "/list",
      "Description": "List all valid paths for commands."
    },
    {
      "Verb": "GET",
      "Path": "/get",
      "Description": "Get a value for a node's endpoint."
    },
    {
      "Verb": "PATCH",
      "Path": "/set",
      "Description": "Set a value on certain, writable endpoints."
    },
    {
      "Verb": "GET",
      "Path": "/subscription",
      "Description": "Read a subscription set. Parameters: Subscription (int) - set to read from. Returns an array of paths & values. Faster (less overhead) than asking for each individual variable."
    },
    {
      "Verb": "POST",
      "Path": "/subscription",
      "Description": "Create a subscription. Parameters; Subscription (int) - set to insert subscription into. Path: Path to a variable as per /get route"
    },
    {
      "Verb": "DELETE",
      "Path": "/subscription",
      "Description": "Unsubscribe from a particular path.."
    },
    {
      "Verb": "GET",
      "Path": "/listsubscriptions",
      "Description": "List all subscriptions currently in use, but doesn't retrieve their values."
    }
  ]
}
```

/list =>
```json
{
  "Result": "Success",
  "NodePath": "Root",
  "NodeName": "Root",
  "Nodes": [
    {
      "NodePath": "Root/VirtualRailDriver",
      "NodeName": "VirtualRailDriver"
    },
    {
      "NodePath": "Root/Player",
      "NodeName": "Player",
      "Nodes": [
        {
          "NodePath": "Root/Player/TransformComponent0",
          "NodeName": "TransformComponent0"
        },
        {
          "NodePath": "Root/Player/PC_InputComponent0",
          "NodeName": "PC_InputComponent0"
        },
        {
          "NodePath": "Root/Player/GamepadCursorInput",
          "NodeName": "GamepadCursorInput"
        },
        {
          "NodePath": "Root/Player/PhotoModeInputComponent",
          "NodeName": "PhotoModeInputComponent"
        },
        {
          "NodePath": "Root/Player/DriverInputComponent_2147466219",
          "NodeName": "DriverInputComponent_2147466219"
        }
      ]
    },
    {
      "NodePath": "Root/CurrentDrivableActor",
      "NodeName": "CurrentDrivableActor",
      "Nodes": [
        {
          "NodePath": "Root/CurrentDrivableActor/ModelChildActorComponent0",
          "NodeName": "ModelChildActorComponent0"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/RailVehiclePhysicsComponent0",
          "NodeName": "RailVehiclePhysicsComponent0"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/GameplayTasksComponent0",
          "NodeName": "GameplayTasksComponent0"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/GameplayTagStatusComponent0",
          "NodeName": "GameplayTagStatusComponent0"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/SeatManager",
          "NodeName": "SeatManager"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/MainResPipe_B",
          "NodeName": "MainResPipe_B"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/Electric%28PushButton%29",
          "NodeName": "Electric(PushButton)"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/EngineStop%28PushButton%29",
          "NodeName": "EngineStop(PushButton)"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/EmergencyEngineStop%28PushButton%29",
          "NodeName": "EmergencyEngineStop(PushButton)"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/PantographUp%28PushButton%29",
          "NodeName": "PantographUp(PushButton)"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/PantographDown%28PushButton%29",
          "NodeName": "PantographDown(PushButton)"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/VigilancePedal%28ReversiblePushButton%29",
          "NodeName": "VigilancePedal(ReversiblePushButton)"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/DoorBuzzer_L%28PushButton%29",
          "NodeName": "DoorBuzzer_L(PushButton)"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/DoorRelease_Left%28PushButton%29",
          "NodeName": "DoorRelease_Left(PushButton)"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/DoorRelease_Right%28PushButton%29",
          "NodeName": "DoorRelease_Right(PushButton)"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/DoorCloseInterlock_L%28PushButton%29",
          "NodeName": "DoorCloseInterlock_L(PushButton)"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/TPWS_SPAD%28PushButton%29",
          "NodeName": "TPWS_SPAD(PushButton)"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/TPWS_TrainstopOverride%28PushButton%29",
          "NodeName": "TPWS_TrainstopOverride(PushButton)"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/BrakeRelease%28PushButton%29",
          "NodeName": "BrakeRelease(PushButton)"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/DRA%28PushButton%29",
          "NodeName": "DRA(PushButton)"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/AWS_Acknowledge%28PushButton%29",
          "NodeName": "AWS_Acknowledge(PushButton)"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/ETCS_Acknowledge%28PushButton%29",
          "NodeName": "ETCS_Acknowledge(PushButton)"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/Sander%28PushButton%29",
          "NodeName": "Sander(PushButton)"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/DoorCloseInterlock_R%28PushButton%29",
          "NodeName": "DoorCloseInterlock_R(PushButton)"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/TrainDoorControl_L%28PushButton%29",
          "NodeName": "TrainDoorControl_L(PushButton)"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/TrainDoorControl_R%28PushButton%29",
          "NodeName": "TrainDoorControl_R(PushButton)"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/DoorBuzzer_R%28PushButton%29",
          "NodeName": "DoorBuzzer_R(PushButton)"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/DriverInteractionEnvironment",
          "NodeName": "DriverInteractionEnvironment"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/SecondmanEnvironment",
          "NodeName": "SecondmanEnvironment"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/CouplerFlaps",
          "NodeName": "CouplerFlaps"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/TractionLockManager",
          "NodeName": "TractionLockManager"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/EmergencyConditionManager",
          "NodeName": "EmergencyConditionManager"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/MCB_GSMR",
          "NodeName": "MCB_GSMR"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/MCB_AWS_TPWS",
          "NodeName": "MCB_AWS_TPWS"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/MCB_DSD",
          "NodeName": "MCB_DSD"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/MCB_Sanding",
          "NodeName": "MCB_Sanding"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/MCB_FrontHatch",
          "NodeName": "MCB_FrontHatch"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/MCB_DeskLamp",
          "NodeName": "MCB_DeskLamp"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/MCB_TractionMotor1",
          "NodeName": "MCB_TractionMotor1"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/MCB_TractionMotor2",
          "NodeName": "MCB_TractionMotor2"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/MCB_CabCeilingLight",
          "NodeName": "MCB_CabCeilingLight"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/MCB_ExtLights_L",
          "NodeName": "MCB_ExtLights_L"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/MCB_ExtLights_R",
          "NodeName": "MCB_ExtLights_R"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/GSMR_Service",
          "NodeName": "GSMR_Service"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/GSMR_IncreaseBrightness",
          "NodeName": "GSMR_IncreaseBrightness"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/GSMR_DecreaseBrightness",
          "NodeName": "GSMR_DecreaseBrightness"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/GSMR_Cancel",
          "NodeName": "GSMR_Cancel"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/GSMR_Confirm",
          "NodeName": "GSMR_Confirm"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/GSMR_IncreaseVolume",
          "NodeName": "GSMR_IncreaseVolume"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/GSMR_DecreaseVolume",
          "NodeName": "GSMR_DecreaseVolume"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/GSMR_Test",
          "NodeName": "GSMR_Test"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/GSMR_Registration",
          "NodeName": "GSMR_Registration"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/CabDoor_L",
          "NodeName": "CabDoor_L"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/CabDoor_R",
          "NodeName": "CabDoor_R"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/ReadingLightSwitch_R%28IrregularLever%29",
          "NodeName": "ReadingLightSwitch_R(IrregularLever)"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/SecondManDesk",
          "NodeName": "SecondManDesk"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/Blind",
          "NodeName": "Blind"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/PowerHandle_BrakeHold",
          "NodeName": "PowerHandle_BrakeHold"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/CabDoorLight_L",
          "NodeName": "CabDoorLight_L"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/CabDoorLight_L1",
          "NodeName": "CabDoorLight_L1"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/CabDoorLight_R",
          "NodeName": "CabDoorLight_R"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/CabDoorLight_R1",
          "NodeName": "CabDoorLight_R1"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/CabDoorControlPanel_Signal_L",
          "NodeName": "CabDoorControlPanel_Signal_L"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/CabDoorPanelBuzzerKey_L",
          "NodeName": "CabDoorPanelBuzzerKey_L"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/CabDoorControlPanel_Release_L",
          "NodeName": "CabDoorControlPanel_Release_L"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/CabDoorControlPanel_Close_L",
          "NodeName": "CabDoorControlPanel_Close_L"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/CabDoorControlPanel_Signal_R",
          "NodeName": "CabDoorControlPanel_Signal_R"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/CabDoorPanelBuzzerKey_R",
          "NodeName": "CabDoorPanelBuzzerKey_R"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/CabDoorControlPanel_Release_R",
          "NodeName": "CabDoorControlPanel_Release_R"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/CabDoorControlPanel_Close_R",
          "NodeName": "CabDoorControlPanel_Close_R"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/CabDoorControlPanel_LocalDoor_R",
          "NodeName": "CabDoorControlPanel_LocalDoor_R"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/CabDoorControlPanel_LocalDoor_L",
          "NodeName": "CabDoorControlPanel_LocalDoor_L"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/CabGangwayDoor",
          "NodeName": "CabGangwayDoor"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/BP_SimpleRadio",
          "NodeName": "BP_SimpleRadio"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/HVAC_FanSpeed",
          "NodeName": "HVAC_FanSpeed"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/HVAC_Mode",
          "NodeName": "HVAC_Mode"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/HVAC_Temperature",
          "NodeName": "HVAC_Temperature"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/FuseCabinetDoor",
          "NodeName": "FuseCabinetDoor"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/TrainDoorControl_Proxy",
          "NodeName": "TrainDoorControl_Proxy"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/MainCabLighting",
          "NodeName": "MainCabLighting"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/TPWS_TemporaryIsolation",
          "NodeName": "TPWS_TemporaryIsolation"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/WiperBlade",
          "NodeName": "WiperBlade"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/SnowBrake",
          "NodeName": "SnowBrake"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/TPWS_Overspeed%28PushButton%29",
          "NodeName": "TPWS_Overspeed(PushButton)"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/TPWS_AWS%28PushButton%29",
          "NodeName": "TPWS_AWS(PushButton)"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/AWS_TPWS_Service",
          "NodeName": "AWS_TPWS_Service"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/PIS_Manager",
          "NodeName": "PIS_Manager"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/ISManager",
          "NodeName": "ISManager"
        },
        {
          "NodePath": "Root/CurrentDrivableActor/DriverAssist",
          "NodeName": "DriverAssist"
        }
      ]
    },
    {
      "NodePath": "Root/CurrentFormation",
      "NodeName": "CurrentFormation",
      "Nodes": [
        {
          "NodePath": "Root/CurrentFormation/0",
          "NodeName": "0",
          "CollapsedChildren": 188
        },
        {
          "NodePath": "Root/CurrentFormation/1",
          "NodeName": "1",
          "CollapsedChildren": 72
        },
        {
          "NodePath": "Root/CurrentFormation/2",
          "NodeName": "2",
          "CollapsedChildren": 72
        },
        {
          "NodePath": "Root/CurrentFormation/3",
          "NodeName": "3",
          "CollapsedChildren": 72
        },
        {
          "NodePath": "Root/CurrentFormation/4",
          "NodeName": "4",
          "CollapsedChildren": 189
        },
        {
          "NodePath": "Root/CurrentFormation/5",
          "NodeName": "5",
          "CollapsedChildren": 188
        },
        {
          "NodePath": "Root/CurrentFormation/6",
          "NodeName": "6",
          "CollapsedChildren": 72
        },
        {
          "NodePath": "Root/CurrentFormation/7",
          "NodeName": "7",
          "CollapsedChildren": 72
        },
        {
          "NodePath": "Root/CurrentFormation/8",
          "NodeName": "8",
          "CollapsedChildren": 72
        },
        {
          "NodePath": "Root/CurrentFormation/9",
          "NodeName": "9",
          "CollapsedChildren": 189
        }
      ]
    },
    {
      "NodePath": "Root/WeatherManager",
      "NodeName": "WeatherManager"
    },
    {
      "NodePath": "Root/Timetable",
      "NodeName": "Timetable",
      "Nodes": [
        {
          "NodePath": "Root/Timetable/A0FD8D774A5428F9D1A698973A65F677",
          "NodeName": "A0FD8D774A5428F9D1A698973A65F677",
          "CollapsedChildren": 208
        },
        {
          "NodePath": "Root/Timetable/458143A347572809BFDE79A6CADC47BA",
          "NodeName": "458143A347572809BFDE79A6CADC47BA",
          "CollapsedChildren": 76
        },
        {
          "NodePath": "Root/Timetable/94A2E50B40082DAFF20B8688F54705A3",
          "NodeName": "94A2E50B40082DAFF20B8688F54705A3",
          "CollapsedChildren": 76
        },
        {
          "NodePath": "Root/Timetable/B949C9C240FEA1037A2F1FA69FE3C772",
          "NodeName": "B949C9C240FEA1037A2F1FA69FE3C772",
          "CollapsedChildren": 208
        },
        {
          "NodePath": "Root/Timetable/CA245B32496EA3447202939844FFAE87",
          "NodeName": "CA245B32496EA3447202939844FFAE87",
          "CollapsedChildren": 208
        },
        {
          "NodePath": "Root/Timetable/E1EA69354406B6500782E589464AE687",
          "NodeName": "E1EA69354406B6500782E589464AE687",
          "CollapsedChildren": 76
        }
      ]
    },
    {
      "NodePath": "Root/TimeOfDay",
      "NodeName": "TimeOfDay"
    },
    {
      "NodePath": "Root/DriverInput",
      "NodeName": "DriverInput",
      "Nodes": [
        {
          "NodePath": "Root/DriverInput/Reverser%28IrregularLever%29",
          "NodeName": "Reverser(IrregularLever)"
        },
        {
          "NodePath": "Root/DriverInput/MasterKey%28SimpleLever%29",
          "NodeName": "MasterKey(SimpleLever)"
        },
        {
          "NodePath": "Root/DriverInput/PowerHandle%28IrregularLever%29",
          "NodeName": "PowerHandle(IrregularLever)"
        },
        {
          "NodePath": "Root/DriverInput/EngineStart%28PushButton%29",
          "NodeName": "EngineStart(PushButton)"
        },
        {
          "NodePath": "Root/DriverInput/Diesel%28PushButton%29",
          "NodeName": "Diesel(PushButton)"
        },
        {
          "NodePath": "Root/DriverInput/Electric%28PushButton%29",
          "NodeName": "Electric(PushButton)"
        },
        {
          "NodePath": "Root/DriverInput/EngineStop%28PushButton%29",
          "NodeName": "EngineStop(PushButton)"
        },
        {
          "NodePath": "Root/DriverInput/EmergencyEngineStop%28PushButton%29",
          "NodeName": "EmergencyEngineStop(PushButton)"
        },
        {
          "NodePath": "Root/DriverInput/PantographUp%28PushButton%29",
          "NodeName": "PantographUp(PushButton)"
        },
        {
          "NodePath": "Root/DriverInput/PantographDown%28PushButton%29",
          "NodeName": "PantographDown(PushButton)"
        },
        {
          "NodePath": "Root/DriverInput/VigilancePedal%28ReversiblePushButton%29",
          "NodeName": "VigilancePedal(ReversiblePushButton)"
        },
        {
          "NodePath": "Root/DriverInput/DoorBuzzer_L%28PushButton%29",
          "NodeName": "DoorBuzzer_L(PushButton)"
        },
        {
          "NodePath": "Root/DriverInput/DoorRelease_Left%28PushButton%29",
          "NodeName": "DoorRelease_Left(PushButton)"
        },
        {
          "NodePath": "Root/DriverInput/DoorRelease_Right%28PushButton%29",
          "NodeName": "DoorRelease_Right(PushButton)"
        },
        {
          "NodePath": "Root/DriverInput/DoorCloseInterlock_L%28PushButton%29",
          "NodeName": "DoorCloseInterlock_L(PushButton)"
        },
        {
          "NodePath": "Root/DriverInput/TPWS_SPAD%28PushButton%29",
          "NodeName": "TPWS_SPAD(PushButton)"
        },
        {
          "NodePath": "Root/DriverInput/TPWS_TrainstopOverride%28PushButton%29",
          "NodeName": "TPWS_TrainstopOverride(PushButton)"
        },
        {
          "NodePath": "Root/DriverInput/BrakeRelease%28PushButton%29",
          "NodeName": "BrakeRelease(PushButton)"
        },
        {
          "NodePath": "Root/DriverInput/DRA%28PushButton%29",
          "NodeName": "DRA(PushButton)"
        },
        {
          "NodePath": "Root/DriverInput/AWS_Acknowledge%28PushButton%29",
          "NodeName": "AWS_Acknowledge(PushButton)"
        },
        {
          "NodePath": "Root/DriverInput/ETCS_Acknowledge%28PushButton%29",
          "NodeName": "ETCS_Acknowledge(PushButton)"
        },
        {
          "NodePath": "Root/DriverInput/Sander%28PushButton%29",
          "NodeName": "Sander(PushButton)"
        },
        {
          "NodePath": "Root/DriverInput/HeadLights%28IrregularLever%29",
          "NodeName": "HeadLights(IrregularLever)"
        },
        {
          "NodePath": "Root/DriverInput/HazardWarning%28PushButton%29",
          "NodeName": "HazardWarning(PushButton)"
        },
        {
          "NodePath": "Root/DriverInput/CabDownLight%28PushButton%29",
          "NodeName": "CabDownLight(PushButton)"
        },
        {
          "NodePath": "Root/DriverInput/CentralCabLight%28PushButton%29",
          "NodeName": "CentralCabLight(PushButton)"
        },
        {
          "NodePath": "Root/DriverInput/ReadingLightSwitch_L%28IrregularLever%29",
          "NodeName": "ReadingLightSwitch_L(IrregularLever)"
        },
        {
          "NodePath": "Root/DriverInput/DeskIlluminationSwitch%28IrregularLever%29",
          "NodeName": "DeskIlluminationSwitch(IrregularLever)"
        },
        {
          "NodePath": "Root/DriverInput/CouplePreparation_F%28PushButton%29",
          "NodeName": "CouplePreparation_F(PushButton)"
        },
        {
          "NodePath": "Root/DriverInput/CouplePreparation_R%28PushButton%29",
          "NodeName": "CouplePreparation_R(PushButton)"
        },
        {
          "NodePath": "Root/DriverInput/Couple%28PushButton%29",
          "NodeName": "Couple(PushButton)"
        },
        {
          "NodePath": "Root/DriverInput/Uncouple%28PushButton%29",
          "NodeName": "Uncouple(PushButton)"
        },
        {
          "NodePath": "Root/DriverInput/WindscreenWiperPosition%28IrregularLever%29",
          "NodeName": "WindscreenWiperPosition(IrregularLever)"
        },
        {
          "NodePath": "Root/DriverInput/WindscreenWiperSpeed%28IrregularLever%29",
          "NodeName": "WindscreenWiperSpeed(IrregularLever)"
        },
        {
          "NodePath": "Root/DriverInput/DSDOverride%28PushButton%29",
          "NodeName": "DSDOverride(PushButton)"
        },
        {
          "NodePath": "Root/DriverInput/Horn%28IrregularLever%29",
          "NodeName": "Horn(IrregularLever)"
        },
        {
          "NodePath": "Root/DriverInput/ETCSIsolation%28SimpleLever%29",
          "NodeName": "ETCSIsolation(SimpleLever)"
        },
        {
          "NodePath": "Root/DriverInput/DRAIsolation%28SimpleLever%29",
          "NodeName": "DRAIsolation(SimpleLever)"
        },
        {
          "NodePath": "Root/DriverInput/SDOIsolation%28SimpleLever%29",
          "NodeName": "SDOIsolation(SimpleLever)"
        },
        {
          "NodePath": "Root/DriverInput/AWSIsolation%28SimpleLever%29",
          "NodeName": "AWSIsolation(SimpleLever)"
        },
        {
          "NodePath": "Root/DriverInput/TPWS%2FAWSIsolation%28SimpleLever%29",
          "NodeName": "TPWS/AWSIsolation(SimpleLever)"
        },
        {
          "NodePath": "Root/DriverInput/VigilanceIsolationSwitch%28SimpleLever%29",
          "NodeName": "VigilanceIsolationSwitch(SimpleLever)"
        },
        {
          "NodePath": "Root/DriverInput/DSDIsolation%28SimpleLever%29",
          "NodeName": "DSDIsolation(SimpleLever)"
        },
        {
          "NodePath": "Root/DriverInput/PantoGraphSelection%28SimpleLever%29",
          "NodeName": "PantoGraphSelection(SimpleLever)"
        },
        {
          "NodePath": "Root/DriverInput/AuxOff%28PushButton%29",
          "NodeName": "AuxOff(PushButton)"
        },
        {
          "NodePath": "Root/DriverInput/AuxOn%28PushButton%29",
          "NodeName": "AuxOn(PushButton)"
        },
        {
          "NodePath": "Root/DriverInput/EmergencyBrake_R%28ToggleButton%29",
          "NodeName": "EmergencyBrake_R(ToggleButton)"
        },
        {
          "NodePath": "Root/DriverInput/EmergencyBrake_L%28ToggleButton%29",
          "NodeName": "EmergencyBrake_L(ToggleButton)"
        },
        {
          "NodePath": "Root/DriverInput/ContactSignaller",
          "NodeName": "ContactSignaller"
        },
        {
          "NodePath": "Root/DriverInput/DoorCloseInterlock_R%28PushButton%29",
          "NodeName": "DoorCloseInterlock_R(PushButton)"
        },
        {
          "NodePath": "Root/DriverInput/TrainDoorControl_L%28PushButton%29",
          "NodeName": "TrainDoorControl_L(PushButton)"
        },
        {
          "NodePath": "Root/DriverInput/TrainDoorControl_R%28PushButton%29",
          "NodeName": "TrainDoorControl_R(PushButton)"
        },
        {
          "NodePath": "Root/DriverInput/DoorBuzzer_R%28PushButton%29",
          "NodeName": "DoorBuzzer_R(PushButton)"
        },
        {
          "NodePath": "Root/DriverInput/GSMR_IncreaseBrightness",
          "NodeName": "GSMR_IncreaseBrightness"
        },
        {
          "NodePath": "Root/DriverInput/GSMR_DecreaseBrightness",
          "NodeName": "GSMR_DecreaseBrightness"
        },
        {
          "NodePath": "Root/DriverInput/GSMR_Cancel",
          "NodeName": "GSMR_Cancel"
        },
        {
          "NodePath": "Root/DriverInput/GSMR_Confirm",
          "NodeName": "GSMR_Confirm"
        },
        {
          "NodePath": "Root/DriverInput/GSMR_IncreaseVolume",
          "NodeName": "GSMR_IncreaseVolume"
        },
        {
          "NodePath": "Root/DriverInput/GSMR_DecreaseVolume",
          "NodeName": "GSMR_DecreaseVolume"
        },
        {
          "NodePath": "Root/DriverInput/GSMR_Test",
          "NodeName": "GSMR_Test"
        },
        {
          "NodePath": "Root/DriverInput/GSMR_Registration",
          "NodeName": "GSMR_Registration"
        },
        {
          "NodePath": "Root/DriverInput/ReadingLightSwitch_R%28IrregularLever%29",
          "NodeName": "ReadingLightSwitch_R(IrregularLever)"
        },
        {
          "NodePath": "Root/DriverInput/Blind",
          "NodeName": "Blind"
        },
        {
          "NodePath": "Root/DriverInput/PowerHandle_BrakeHold",
          "NodeName": "PowerHandle_BrakeHold"
        },
        {
          "NodePath": "Root/DriverInput/CabDoorLight_L",
          "NodeName": "CabDoorLight_L"
        },
        {
          "NodePath": "Root/DriverInput/CabDoorLight_L1",
          "NodeName": "CabDoorLight_L1"
        },
        {
          "NodePath": "Root/DriverInput/CabDoorLight_R",
          "NodeName": "CabDoorLight_R"
        },
        {
          "NodePath": "Root/DriverInput/CabDoorLight_R1",
          "NodeName": "CabDoorLight_R1"
        },
        {
          "NodePath": "Root/DriverInput/CabDoorControlPanel_Signal_L",
          "NodeName": "CabDoorControlPanel_Signal_L"
        },
        {
          "NodePath": "Root/DriverInput/CabDoorPanelBuzzerKey_L",
          "NodeName": "CabDoorPanelBuzzerKey_L"
        },
        {
          "NodePath": "Root/DriverInput/CabDoorControlPanel_Release_L",
          "NodeName": "CabDoorControlPanel_Release_L"
        },
        {
          "NodePath": "Root/DriverInput/CabDoorControlPanel_Close_L",
          "NodeName": "CabDoorControlPanel_Close_L"
        },
        {
          "NodePath": "Root/DriverInput/CabDoorControlPanel_Signal_R",
          "NodeName": "CabDoorControlPanel_Signal_R"
        },
        {
          "NodePath": "Root/DriverInput/CabDoorPanelBuzzerKey_R",
          "NodeName": "CabDoorPanelBuzzerKey_R"
        },
        {
          "NodePath": "Root/DriverInput/CabDoorControlPanel_Release_R",
          "NodeName": "CabDoorControlPanel_Release_R"
        },
        {
          "NodePath": "Root/DriverInput/CabDoorControlPanel_Close_R",
          "NodeName": "CabDoorControlPanel_Close_R"
        },
        {
          "NodePath": "Root/DriverInput/CabDoorControlPanel_LocalDoor_R",
          "NodeName": "CabDoorControlPanel_LocalDoor_R"
        },
        {
          "NodePath": "Root/DriverInput/CabDoorControlPanel_LocalDoor_L",
          "NodeName": "CabDoorControlPanel_LocalDoor_L"
        },
        {
          "NodePath": "Root/DriverInput/FuseCabinetDoor",
          "NodeName": "FuseCabinetDoor"
        },
        {
          "NodePath": "Root/DriverInput/TrainDoorControl_Proxy",
          "NodeName": "TrainDoorControl_Proxy"
        },
        {
          "NodePath": "Root/DriverInput/MainCabLighting",
          "NodeName": "MainCabLighting"
        },
        {
          "NodePath": "Root/DriverInput/TPWS_TemporaryIsolation",
          "NodeName": "TPWS_TemporaryIsolation"
        },
        {
          "NodePath": "Root/DriverInput/SnowBrake",
          "NodeName": "SnowBrake"
        },
        {
          "NodePath": "Root/DriverInput/TPWS_Overspeed%28PushButton%29",
          "NodeName": "TPWS_Overspeed(PushButton)"
        },
        {
          "NodePath": "Root/DriverInput/TPWS_AWS%28PushButton%29",
          "NodeName": "TPWS_AWS(PushButton)"
        }
      ]
    },
    {
      "NodePath": "Root/DriverAid",
      "NodeName": "DriverAid"
    }
  ]
}
```

/list/CurrentDrivableActor/AWS_TPWS_Service =>
```json
{
  "Result": "Success",
  "NodePath": "CurrentDrivableActor/AWS_TPWS_Service",
  "NodeName": "AWS_TPWS_Service",
  "Nodes": [],
  "Endpoints": [
    {
      "Name": "Property.bIsAWS_CutIn",
      "Writable": false
    },
    {
      "Name": "Property.bIsAWS_Enabled",
      "Writable": false
    },
    {
      "Name": "Property.bIsAWS_Active",
      "Writable": false
    },
    {
      "Name": "Property.IsAWS_PassiveStateActive",
      "Writable": false
    },
    {
      "Name": "Property.AWS_SunflowerState",
      "Writable": false
    },
    {
      "Name": "Property.HasAWSPassiveState",
      "Writable": false
    },
    {
      "Name": "Property.AWS_AcknowledgeTime",
      "Writable": false
    },
    {
      "Name": "Property.AWS_MagnetDelayTime",
      "Writable": false
    },
    {
      "Name": "Property.bIsTPWS_CutIn",
      "Writable": false
    },
    {
      "Name": "Property.bIsTPWS_Enabled",
      "Writable": false
    },
    {
      "Name": "Property.bIsTPWS_Active",
      "Writable": false
    },
    {
      "Name": "Property.IsTPWS_PassiveStateActive",
      "Writable": false
    },
    {
      "Name": "Property.bIsTPWS_TemporaryIsolation",
      "Writable": false
    },
    {
      "Name": "Property.bIsTPWS_TrainStopOverride",
      "Writable": false
    },
    {
      "Name": "Property.HasTPWSPassiveState",
      "Writable": false
    },
    {
      "Name": "Property.TPWS_MagnetDelayTime",
      "Writable": false
    },
    {
      "Name": "Property.TPWS_TrainStopOverrideTime",
      "Writable": false
    },
    {
      "Name": "Property.bIsPowered",
      "Writable": false
    },
    {
      "Name": "Property.bActiveState",
      "Writable": false
    },
    {
      "Name": "Property.BrakeDemandDuration",
      "Writable": false
    },
    {
      "Name": "Property.SelfTestState",
      "Writable": false
    },
    {
      "Name": "Property.bIsBrakeDemand",
      "Writable": false
    },
    {
      "Name": "Property.BrakeDemandStartTime",
      "Writable": false
    },
    {
      "Name": "Property.ArmedTSS_66-25",
      "Writable": false
    },
    {
      "Name": "Property.ArmedOSS_64-25",
      "Writable": false
    },
    {
      "Name": "Property.ArmedTSS_66-75",
      "Writable": false
    },
    {
      "Name": "Property.ArmedOSS_64-75",
      "Writable": false
    },
    {
      "Name": "Property.TPWS_OverspeedBrakeDemandState",
      "Writable": false
    },
    {
      "Name": "Property.TPWS_TrainStopBrakeDemandState",
      "Writable": false
    },
    {
      "Name": "Property.AWS_WarningState",
      "Writable": false
    },
    {
      "Name": "Property.AWS_BrakeDemandState",
      "Writable": false
    },
    {
      "Name": "Property.SelfTestDelay",
      "Writable": false
    },
    {
      "Name": "Property.CorrectUseScore",
      "Writable": false
    },
    {
      "Name": "Property.bReplicates",
      "Writable": false
    },
    {
      "Name": "Property.bAutoActivate",
      "Writable": false
    },
    {
      "Name": "Property.bIsEditorOnly",
      "Writable": false
    },
    {
      "Name": "Function.SafetySystemIsUsedCorrectly",
      "Writable": false
    },
    {
      "Name": "Function.GetSelfTestBrakeDemand",
      "Writable": false
    },
    {
      "Name": "Function.GetAWS_AcknowledgeRequired",
      "Writable": false
    },
    {
      "Name": "Function.GetSelfTestAcknowledgeRequired",
      "Writable": false
    },
    {
      "Name": "Function.AWS_TPWS_GetAlerter",
      "Writable": false
    },
    {
      "Name": "Function.GetAWS_Indicator",
      "Writable": false
    },
    {
      "Name": "Function.GetAWS_ReleaseRequired",
      "Writable": false
    },
    {
      "Name": "Function.GetTPWS_TrainStopButtonRequired",
      "Writable": false
    },
    {
      "Name": "Function.GetTPWS_OverspeedButtonRequired",
      "Writable": false
    },
    {
      "Name": "Function.GetTPWS_TrainStopIndicator",
      "Writable": false
    },
    {
      "Name": "Function.GetTPWS_OverspeedIndicator",
      "Writable": false
    },
    {
      "Name": "Function.GetAcknowledgeButtonRequired",
      "Writable": false
    },
    {
      "Name": "Function.TPWS_TrainStopAcknowledgeOrRelease",
      "Writable": false
    },
    {
      "Name": "Function.TPWS_OverspeedAcknowledgeOrRelease",
      "Writable": false
    },
    {
      "Name": "Function.TPWS_TrainStopBrakeRelease",
      "Writable": false
    },
    {
      "Name": "Function.TPWS_OverspeedBrakeRelease",
      "Writable": false
    },
    {
      "Name": "Function.AWS_BrakeRelease",
      "Writable": false
    },
    {
      "Name": "Function.TPWS_TrainStopAcknowledge",
      "Writable": false
    },
    {
      "Name": "Function.TPWS_OverspeedAcknowledge",
      "Writable": false
    },
    {
      "Name": "Function.GetSelfTestAudio",
      "Writable": false
    },
    {
      "Name": "Function.GetTPWS_TrainStopAudio",
      "Writable": false
    },
    {
      "Name": "Function.GetTPWS_OverspeedAudio",
      "Writable": false
    },
    {
      "Name": "Function.GetAWS_ActiveAudio",
      "Writable": false
    },
    {
      "Name": "Function.GetAWS_AudioWarningHorn",
      "Writable": false
    },
    {
      "Name": "Function.GetTPWS_IsActive",
      "Writable": false
    },
    {
      "Name": "Function.GetAWS_IsActive",
      "Writable": false
    },
    {
      "Name": "Function.GetIsSelfTest",
      "Writable": false
    },
    {
      "Name": "Function.GetTPWS_BrakeDemandFromTrainStop",
      "Writable": false
    },
    {
      "Name": "Function.GetTPWS_PassiveState",
      "Writable": false
    },
    {
      "Name": "Function.GetAWS_PassiveState",
      "Writable": false
    },
    {
      "Name": "Function.GetIsPowered",
      "Writable": false
    },
    {
      "Name": "Function.GetAWS_SunflowerState",
      "Writable": false
    },
    {
      "Name": "Function.GetWarningIndicator",
      "Writable": false
    },
    {
      "Name": "Function.GetIsActive",
      "Writable": false
    },
    {
      "Name": "Function.GetBrakeDemandActive",
      "Writable": false
    },
    {
      "Name": "Function.GetAWS_EnabledState",
      "Writable": false
    },
    {
      "Name": "Function.GetTPWS_EnabledState",
      "Writable": false
    },
    {
      "Name": "Function.GetAWS_BrakeDemand",
      "Writable": false
    },
    {
      "Name": "Function.GetTPWS_BrakeDemandFromOverspeed",
      "Writable": false
    },
    {
      "Name": "Function.GetIsAcknowledged",
      "Writable": false
    },
    {
      "Name": "Function.GetTPWS_TemporatyIsolation",
      "Writable": false
    },
    {
      "Name": "Function.SetTPWS_TrainStopOverride",
      "Writable": false
    },
    {
      "Name": "Function.GetTPWS_TrainStopOverride",
      "Writable": false
    },
    {
      "Name": "Function.IsSelfTestBrakeDemand",
      "Writable": false
    },
    {
      "Name": "Function.HasBrakeDemandTimerElapsed",
      "Writable": false
    },
    {
      "Name": "Function.GetIsAcknowledgeOrReleaseRequired",
      "Writable": false
    },
    {
      "Name": "Function.Get_RemainingBrakeDemand_Seconds",
      "Writable": false
    },
    {
      "Name": "Function.CanClearBrakeDemand",
      "Writable": false
    },
    {
      "Name": "Function.GetIsTPWS_PassiveState",
      "Writable": false
    },
    {
      "Name": "Function.GetIsAWS_PassiveState",
      "Writable": false
    },
    {
      "Name": "Function.GetIsAWS_Active",
      "Writable": false
    },
    {
      "Name": "Function.GetIsTPWS_Active",
      "Writable": false
    },
    {
      "Name": "Function.IsComponentTickEnabled",
      "Writable": false
    },
    {
      "Name": "Function.IsBeingDestroyed",
      "Writable": false
    },
    {
      "Name": "Function.IsActive",
      "Writable": false
    },
    {
      "Name": "Function.GetComponentTickInterval",
      "Writable": false
    },
    {
      "Name": "Function.GetSelfTestBrakeDemand",
      "Writable": false
    },
    {
      "Name": "Function.GetAWS_AcknowledgeRequired",
      "Writable": false
    },
    {
      "Name": "Function.AWS_TPWS_GetAlerter",
      "Writable": false
    },
    {
      "Name": "Function.GetSelfTestAcknowledgeRequired",
      "Writable": false
    },
    {
      "Name": "Function.TPWS_TrainStopAcknowledgeOrRelease",
      "Writable": false
    },
    {
      "Name": "Function.TPWS_TrainStopBrakeRelease",
      "Writable": false
    },
    {
      "Name": "Function.GetAWS_Indicator",
      "Writable": false
    },
    {
      "Name": "Function.GetAWS_ReleaseRequired",
      "Writable": false
    },
    {
      "Name": "Function.GetAcknowledgeButtonRequired",
      "Writable": false
    },
    {
      "Name": "Function.GetTPWS_TrainStopButtonRequired",
      "Writable": false
    },
    {
      "Name": "Function.GetTPWS_OverspeedButtonRequired",
      "Writable": false
    },
    {
      "Name": "Function.GetTPWS_TrainStopIndicator",
      "Writable": false
    },
    {
      "Name": "Function.GetTPWS_OverspeedIndicator",
      "Writable": false
    },
    {
      "Name": "Function.TPWS_TrainStopAcknowledge",
      "Writable": false
    },
    {
      "Name": "Function.TPWS_OverspeedAcknowledgeOrRelease",
      "Writable": false
    },
    {
      "Name": "Function.TPWS_OverspeedBrakeRelease",
      "Writable": false
    },
    {
      "Name": "Function.TPWS_OverspeedAcknowledge",
      "Writable": false
    },
    {
      "Name": "Function.AWS_BrakeRelease",
      "Writable": false
    },
    {
      "Name": "Function.GetSelfTestAudio",
      "Writable": false
    },
    {
      "Name": "Function.GetTPWS_TrainStopAudio",
      "Writable": false
    },
    {
      "Name": "Function.GetTPWS_OverspeedAudio",
      "Writable": false
    },
    {
      "Name": "Function.GetAWS_ActiveAudio",
      "Writable": false
    },
    {
      "Name": "Function.GetAWS_AudioWarningHorn",
      "Writable": false
    },
    {
      "Name": "Function.GetIsSelfTest",
      "Writable": false
    },
    {
      "Name": "Function.GetTPWS_BrakeDemandFromTrainStop",
      "Writable": false
    },
    {
      "Name": "Function.GetTPWS_PassiveState",
      "Writable": false
    },
    {
      "Name": "Function.GetAWS_PassiveState",
      "Writable": false
    },
    {
      "Name": "Function.GetIsPowered",
      "Writable": false
    },
    {
      "Name": "Function.GetAWS_SunflowerState",
      "Writable": false
    },
    {
      "Name": "Function.GetWarningIndicator",
      "Writable": false
    },
    {
      "Name": "Function.GetIsActive",
      "Writable": false
    },
    {
      "Name": "Function.GetBrakeDemandActive",
      "Writable": false
    },
    {
      "Name": "Function.GetTPWS_TrainStopOverride",
      "Writable": false
    },
    {
      "Name": "Function.SetTPWS_TrainStopOverride",
      "Writable": false
    },
    {
      "Name": "Function.GetTPWS_TemporatyIsolation",
      "Writable": false
    },
    {
      "Name": "Function.GetIsAcknowledged",
      "Writable": false
    },
    {
      "Name": "Function.GetTPWS_BrakeDemandFromOverspeed",
      "Writable": false
    },
    {
      "Name": "Function.GetAWS_BrakeDemand",
      "Writable": false
    },
    {
      "Name": "Function.GetTPWS_EnabledState",
      "Writable": false
    },
    {
      "Name": "Function.GetTPWS_IsActive",
      "Writable": false
    },
    {
      "Name": "Function.GetAWS_EnabledState",
      "Writable": false
    },
    {
      "Name": "Function.GetAWS_IsActive",
      "Writable": false
    },
    {
      "Name": "Function.SafetySystemIsUsedCorrectly",
      "Writable": false
    },
    {
      "Name": "ObjectName",
      "Writable": false
    },
    {
      "Name": "ObjectClass",
      "Writable": false
    }
  ]
}
```


/get/CurrentDrivableActor/AWS_TPWS_Service.Property.AWS_SunflowerState =>
```json
{
  "Result": "Success",
  "Values": {
    "Value": 1
  }
}
```

From this you should be able to work out how to navigate the API. Please either write a type provider if you think that would help or some other way that we can navigate that tree. There might be a case where we would like to drill into the tree from the UI if that helps with the design of the library. Subscriptions and POST/PATCH requests will come later so make sure you're ready to work with that