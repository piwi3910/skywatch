# SkyWatch Configuration Reference

All configuration is managed through JSON files. Application-level settings live in `appsettings.json`. Per-profile settings (thresholds, capture, timing) live in individual profile JSON files under the profiles directory.

## Application Settings (`appsettings.json`)

### SkyWatch Section

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `SkyWatch:ActiveProfile` | string | `"default"` | Name of the profile to load at startup. |
| `SkyWatch:ProfilesDirectory` | string | `"profiles"` | Directory containing profile JSON files. |
| `SkyWatch:LogsDirectory` | string | `"logs"` | Directory for rolling log files. |
| `SkyWatch:DebugFramesDirectory` | string | `"debug-frames"` | Directory where debug frames are saved on state changes. |
| `SkyWatch:SaveDebugFramesOnStateChange` | bool | `true` | Whether to save a frame image when the safety state changes. |

### Alpaca Section

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Alpaca:Port` | int | `11111` | HTTP port for the ASCOM Alpaca API. |
| `Alpaca:DiscoveryPort` | int | `32227` | UDP port for Alpaca device discovery (standard is 32227). |
| `Alpaca:EnableDiscovery` | bool | `true` | Whether to respond to Alpaca UDP discovery broadcasts. |
| `Alpaca:ServerName` | string | `"SkyWatch"` | Server name reported in Alpaca discovery responses. |
| `Alpaca:Manufacturer` | string | `"SkyWatch"` | Manufacturer name reported through the Alpaca API. |
| `Alpaca:DeviceName` | string | `"SkyWatch SafetyMonitor"` | Device name reported through the Alpaca API. |
| `Alpaca:UniqueId` | string | (auto-generated GUID) | Unique identifier for this device instance. |

### Other

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Urls` | string | `"http://0.0.0.0:5000"` | Kestrel listening URL for the web UI and health endpoint. |

## Profile Settings

Each profile is a JSON file stored in the profiles directory. A profile contains all settings that control capture, analysis thresholds, timing, masks, and manual overrides.

### Top-Level Profile Properties

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Name` | string | `"default"` | Profile name. |
| `Description` | string | `"Default profile"` | Human-readable description. |
| `ManualOverride` | enum | `Automatic` | Override mode: `Automatic`, `ForceSafe`, or `ForceUnsafe`. |

### Capture Settings (`Capture`)

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `SourceType` | enum | `Replay` | Frame source type: `UsbCamera`, `Rtsp`, or `Replay`. |
| `SourceIdentifier` | string | `""` | RTSP URL or replay directory path (depends on SourceType). |
| `DeviceIndex` | int | `0` | USB camera device index (used when SourceType is UsbCamera). |
| `FrameIntervalSeconds` | int | `3` | Seconds between frame captures. |
| `TargetWidth` | int | `1280` | Target image width in pixels after resize. |
| `TargetHeight` | int | `720` | Target image height in pixels after resize. |
| `ReconnectIntervalSeconds` | int | `5` | Seconds to wait before reconnecting after a camera disconnect. |
| `StaleFrameTimeoutSeconds` | int | `15` | Seconds after which a frame is considered stale. |
| `ConnectionTimeoutSeconds` | int | `10` | Seconds to wait for initial camera connection. |
| `FlipHorizontal` | bool | `false` | Mirror the image horizontally. |
| `FlipVertical` | bool | `false` | Mirror the image vertically. |
| `RotationDegrees` | int | `0` | Rotate the image by this many degrees (0, 90, 180, 270). |

### Threshold Settings (`Thresholds`)

| Setting | Type | Default | Valid Range | Description |
|---------|------|---------|-------------|-------------|
| `MaxAverageBrightness` | double | `200.0` | 0--255 | Frames with average brightness above this are marked unsafe. |
| `MinStarCount` | int | `10` | 0+ | Fewer stars than this triggers an unsafe condition. |
| `MaxCloudinessScore` | double | `0.7` | 0.0--1.0 | Cloudiness score above this threshold is unsafe. |
| `MinEntropy` | double | `2.0` | 0.0--8.0 | Entropy below this suggests a featureless (obstructed) image. |
| `MinEdgeDensity` | double | `0.005` | 0.0--1.0 | Edge density below this suggests a featureless image. |
| `ObstructionThreshold` | double | `0.8` | 0.0--1.0 | Obstruction score above this triggers an unsafe condition. |
| `StarDetectionThreshold` | double | `0.6` | 0.0--1.0 | Normalized correlation threshold for template matching in star detection. |
| `ConfidenceThreshold` | double | `0.5` | 0.0--1.0 | Minimum confidence score for a reliable analysis. |

### Timing Settings (`Timing`)

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `WarmupSeconds` | int | `30` | Seconds after startup during which the system stays in Initializing state. |
| `UnsafeDebounceSeconds` | int | `10` | Seconds an unsafe condition must persist before transitioning to Unsafe. |
| `SafeRecoveryDebounceSeconds` | int | `300` | Seconds a safe condition must persist before transitioning back to Safe. |
| `MinimumUnsafeHoldSeconds` | int | `60` | Minimum seconds the system must remain Unsafe before it can return to Safe. |
| `FailSafeOnCameraLoss` | bool | `true` | Treat camera disconnection as unsafe. |
| `FailSafeOnStaleFrame` | bool | `true` | Treat stale (timed-out) frames as unsafe. |

### Mask Definitions (`Masks`)

Masks are a list of rectangular or elliptical regions that are excluded from (or included in) analysis. Each entry has:

| Setting | Type | Description |
|---------|------|-------------|
| `Name` | string | Human-readable label for the mask region. |
| `Shape` | enum | `Rectangle` or `Ellipse`. |
| `X` | int | X coordinate of the top-left corner (pixels). |
| `Y` | int | Y coordinate of the top-left corner (pixels). |
| `Width` | int | Width of the mask region (pixels). |
| `Height` | int | Height of the mask region (pixels). |
| `IsExclusion` | bool | `true` to exclude the region from analysis; `false` to restrict analysis to this region only. Default: `true`. |

## Example Profile

```json
{
  "Name": "observatory",
  "Description": "Main observatory allsky camera",
  "ManualOverride": "Automatic",
  "Capture": {
    "SourceType": "Rtsp",
    "SourceIdentifier": "rtsp://192.168.1.100:554/stream",
    "DeviceIndex": 0,
    "FrameIntervalSeconds": 5,
    "TargetWidth": 1280,
    "TargetHeight": 720,
    "ReconnectIntervalSeconds": 5,
    "StaleFrameTimeoutSeconds": 15,
    "ConnectionTimeoutSeconds": 10,
    "FlipHorizontal": false,
    "FlipVertical": false,
    "RotationDegrees": 0
  },
  "Thresholds": {
    "MaxAverageBrightness": 180.0,
    "MinStarCount": 15,
    "MaxCloudinessScore": 0.6,
    "MinEntropy": 2.5,
    "MinEdgeDensity": 0.008,
    "ObstructionThreshold": 0.7,
    "StarDetectionThreshold": 0.55,
    "ConfidenceThreshold": 0.5
  },
  "Timing": {
    "WarmupSeconds": 60,
    "UnsafeDebounceSeconds": 10,
    "SafeRecoveryDebounceSeconds": 600,
    "MinimumUnsafeHoldSeconds": 120,
    "FailSafeOnCameraLoss": true,
    "FailSafeOnStaleFrame": true
  },
  "Masks": [
    {
      "Name": "Horizon treeline",
      "Shape": "Rectangle",
      "X": 0,
      "Y": 600,
      "Width": 1280,
      "Height": 120,
      "IsExclusion": true
    }
  ]
}
```

## Example `appsettings.json`

```json
{
  "SkyWatch": {
    "ActiveProfile": "observatory",
    "ProfilesDirectory": "profiles",
    "LogsDirectory": "logs",
    "DebugFramesDirectory": "debug-frames",
    "SaveDebugFramesOnStateChange": true
  },
  "Alpaca": {
    "Port": 11111,
    "DiscoveryPort": 32227,
    "EnableDiscovery": true,
    "ServerName": "SkyWatch",
    "Manufacturer": "SkyWatch",
    "DeviceName": "SkyWatch SafetyMonitor"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.Hosting.Lifetime": "Information"
      }
    }
  },
  "AllowedHosts": "*",
  "Urls": "http://0.0.0.0:5000"
}
```
