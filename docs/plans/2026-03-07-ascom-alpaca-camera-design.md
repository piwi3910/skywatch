# ASCOM & Alpaca Camera Support — Design Document

## Overview

Add two new camera frame sources to SkyWatch: ASCOM Alpaca (cross-platform HTTP REST) and ASCOM COM (Windows-only native driver). Extend `FrameData` to preserve full image quality at native bit depth. Add auto-exposure capability.

## New Frame Sources

### AlpacaCameraFrameSource

Cross-platform. Connects to any ASCOM Alpaca camera server via HTTP REST.

- **Connection:** host, port, device number
- **Exposure workflow:** `PUT startexposure` → poll `GET imageready` → `GET imagebytes` (binary, fastest transfer)
- **Image data:** Raw pixel array at native bit depth (8/12/16-bit, mono or color)
- **Auto-exposure:** Optional feedback loop adjusting exposure duration based on frame brightness
- **Reconnect:** Same pattern as existing RTSP/USB sources — retry on failure with configurable interval

### AscomComCameraFrameSource

Windows only. Connects to native ASCOM COM camera drivers via `ASCOM.DriverAccess.Camera`.

- **Connection:** ASCOM ProgID string (e.g., `"ASCOM.Simulator.Camera"`)
- **Exposure workflow:** Same as Alpaca but via COM interop
- **Runtime guard:** `RuntimeInformation.IsOSPlatform(OSPlatform.Windows)` — throws `PlatformNotSupportedException` on other platforms
- **Conditional dependency:** `ASCOM.DriverAccess` NuGet package referenced with `Condition="$([MSBuild]::IsOSPlatform('Windows'))"`
- Same image handling and auto-exposure as Alpaca source

## FrameData Changes

Current `FrameData` stores 8-bit only. Updated to preserve full camera quality:

```
FrameData
  byte[] ImageBytes          (raw pixels at native bit depth)
  int Width
  int Height
  int Channels
  int BitsPerChannel         (NEW: 8, 12, 16)
  string PixelFormat         (NEW: "Mono8", "Mono16", "RGB24", "BayerRGGB16", etc.)
  FrameSourceType SourceType
  DateTimeOffset CapturedAt
```

### Downstream handling

- **Vision pipeline:** `MatConverter` creates appropriate `Mat` type (`CV_8UC1`, `CV_16UC1`, etc.). `FramePreprocessor` converts to grayscale and scales to working depth for analysis.
- **Preview (WebSocket/API):** Auto-stretches 16-bit to 8-bit, encodes to JPEG for display.
- **USB/RTSP sources:** Report `BitsPerChannel = 8`, `PixelFormat = "BGR24"`.
- **Alpaca/ASCOM sources:** Report actual camera bit depth and format.

## Auto-Exposure Algorithm

Feedback loop running inside the camera frame source after each capture:

1. Capture frame at current exposure duration
2. Calculate average brightness (quick mean, scaled to 0-255)
3. Compare to `autoExposureTarget`
4. Adjust: `newExposure = currentExposure * (target / actualBrightness)`
5. Damping: `adjusted = current + (new - current) * 0.5` (prevents oscillation)
6. Clamp between `exposureMin` and `exposureMax`

Auto-exposure state (current exposure, enabled flag) included in `CaptureStatus` for UI display.

## Configuration

### New CaptureSettings fields

```
// Alpaca connection
string AlpacaHost
int AlpacaPort = 11111
int AlpacaDeviceNumber = 0

// ASCOM COM
string AscomProgId

// Exposure (shared by Alpaca + ASCOM)
double ExposureDuration = 2.0
bool AutoExposure = false
double AutoExposureTarget = 128.0    (target brightness 0-255)
double ExposureMin = 0.1
double ExposureMax = 30.0
```

### FrameSourceType enum additions

```
AlpacaCamera
AscomCamera
```

### Sources page UI

Two new options in the source type dropdown:
- **Alpaca Camera:** fields for host, port, device number, exposure duration, auto-exposure toggle with target/min/max
- **ASCOM Camera (Windows):** ProgID field, same exposure settings

## Impact on Existing Code

| Component | Change |
|-----------|--------|
| `FrameData` | Add `BitsPerChannel` and `PixelFormat` required properties |
| `MatConverter` | Handle 16-bit Mat types (`CV_16UC1`, `CV_16UC3`) |
| `FramePreprocessor` | Handle 16-bit → 8-bit conversion with auto-stretch for analysis |
| `PreviewApiController` / WebSocket | Auto-stretch 16-bit to 8-bit before JPEG encoding |
| `FrameSourceFactory` | Add cases for `AlpacaCamera` and `AscomCamera` |
| `CaptureSettings` | Add Alpaca/ASCOM/exposure fields |
| `FrameSourceType` enum | Add two values |
| `SettingsValidator` | Validate new fields |
| All existing `FrameData` constructors | Add the two new required properties |
| All existing tests using `FrameData` | Add the two new required properties |
| Sources page (React) | Add two new source type options with conditional fields |
| `services/types.ts` | Add new fields to `CaptureSettings` TypeScript type |

## ASCOM Alpaca Camera API Reference

Key endpoints used (base: `http://{host}:{port}/api/v1/camera/{deviceNumber}/`):

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `connected` | PUT | Connect/disconnect |
| `startexposure` | PUT | Start exposure (duration, light params) |
| `imageready` | GET | Poll for completion |
| `imagebytes` | GET | Download binary image data |
| `camerastate` | GET | Current state (Idle/Exposing/etc.) |
| `cameraxsize` / `cameraysize` | GET | Sensor dimensions |
| `maxbinx` / `maxbiny` | GET | Binning capabilities |
| `binx` / `biny` | PUT | Set binning |
| `gain` / `gainmin` / `gainmax` | GET/PUT | Gain settings |
| `exposuremin` / `exposuremax` | GET | Exposure range |
