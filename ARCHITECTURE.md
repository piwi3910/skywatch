# SkyWatch Architecture

This document describes the internal architecture of SkyWatch. For setup and usage, see [README.md](README.md). For configuration details, see [docs/CONFIGURATION.md](docs/CONFIGURATION.md).

## Pipeline Overview

```
Frame Source -> FrameSourceManager -> Preprocessing -> Vision Analysis -> Safety Engine -> Device API -> Clients/UI
```

A camera frame is captured by `SkyWatchWorker` via `FrameSourceManager`, analyzed for sky metrics by `FrameAnalyzerPipeline`, evaluated against safety thresholds by `SafetyEvaluatorEngine`, and the resulting safe/unsafe state is exposed via the ASCOM Alpaca API and the web UI.

`FrameSourceManager` wraps `IFrameSource` and enables live source switching at runtime without restarting the application.

## Project Dependency Graph

```
SkyWatch.Contracts          (no dependencies)
  |
  +-- SkyWatch.Core
        |
        +-- SkyWatch.Capture   (+ OpenCvSharp)
        |
        +-- SkyWatch.Vision    (+ OpenCvSharp)
        |
        +-- SkyWatch.Safety
        |     |
        |     +-- SkyWatch.Alpaca
        |
        +-- SkyWatch.Web       (+ Safety, Vision, Capture; React + MUI + Vite SPA)

SkyWatch.Host               (all projects -- composition root)
```

`SkyWatch.Contracts` is the leaf dependency: every other project references it. `SkyWatch.Host` is the composition root that wires everything together via dependency injection in `ServiceRegistration.AddSkyWatchServices()`.

## Data Flow

The system processes frames through three stages, each producing a distinct data type:

```
FrameData  ->  FrameAnalysisResult (SkyMetrics)  ->  SafetyEvaluation
```

1. **Frame acquisition** -- `SkyWatchWorker` (`src/SkyWatch.Host/Workers/SkyWatchWorker.cs`) runs a `PeriodicTimer` at `FrameIntervalSeconds` intervals. It calls `IFrameSource.GetLatestFrameAsync()` to obtain a `FrameData` containing raw image bytes, timestamp, and source metadata.

2. **Vision analysis** -- `FrameAnalyzerPipeline` (`src/SkyWatch.Vision/FrameAnalyzerPipeline.cs`) receives `FrameData`, preprocesses it via `FramePreprocessor` (grayscale conversion, mask application), then runs six analyzers in sequence:
   - `BrightnessAnalyzer.Analyze()` -- average pixel intensity
   - `StarDetector.Detect()` -- template-matched star count
   - `EntropyCalculator.Calculate()` -- Shannon entropy
   - `EdgeDensityAnalyzer.Analyze()` -- Canny edge density
   - `CloudinessAnalyzer.Analyze()` -- weighted star ratio + contrast
   - `ObstructionDetector.Detect()` -- composite obstruction score

   Results are packaged as `SkyMetrics` inside a `FrameAnalysisResult`.

3. **Safety evaluation** -- `SafetyEvaluatorEngine` (`src/SkyWatch.Safety/SafetyEvaluatorEngine.cs`) checks for manual overrides, runs six `ISafetyRule` implementations against `SkyMetrics` and `ThresholdSettings`, then applies hysteresis via `HysteresisTracker` to produce a `SafetyEvaluation` with the final state, reasons, and confidence.

4. **State persistence** -- `SafetyStateStore` (`src/SkyWatch.Safety/SafetyStateStore.cs`) holds the current `SafetyEvaluation` and fires `StateChanged` events consumed by the Alpaca API and web UI.

## Key Interfaces

| Interface | Project | Implementation | Responsibility |
|-----------|---------|---------------|----------------|
| `IFrameSource` | Contracts | `UsbCameraSource`, `RtspFrameSource`, `ReplayFrameSource` | Produces camera frames (`FrameData`) |
| `FrameSourceManager` | Capture | (wraps `IFrameSource`) | Manages active frame source, enables live source switching |
| `IFrameAnalyzer` | Contracts | `FrameAnalyzerPipeline` | Analyzes frames into `FrameAnalysisResult` with `SkyMetrics` |
| `ISafetyEvaluator` | Contracts | `SafetyEvaluatorEngine` | Evaluates metrics against thresholds, applies hysteresis |
| `ISafetyStateStore` | Contracts | `SafetyStateStore` | Holds current safety state, fires change events |
| `ISafetyRule` | Safety | `BrightnessRule`, `StarCountRule`, `CloudinessRule`, `ObstructionRule`, `EntropyRule`, `EdgeDensityRule` | Individual threshold checks |

## Configuration Model

Configuration uses a profile-based system with JSON persistence:

```
SkyWatchOptions (appsettings.json)
  +-- ActiveProfile, ProfilesDirectory, LogsDirectory, DebugFramesDirectory

ProfileSettings (profiles/<name>.json)
  +-- CaptureSettings      (source type, URI, resolution, interval)
  +-- ThresholdSettings    (per-metric safe/unsafe thresholds)
  +-- TimingSettings       (debounce durations, hysteresis, warmup)
  +-- List<MaskDefinition> (areas to exclude from analysis)
  +-- ManualOverrideMode   (Automatic, ForceSafe, ForceUnsafe)
```

Managed by `ProfileManager` (`src/SkyWatch.Core/Configuration/ProfileManager.cs`). Each profile is stored as a separate JSON file. The active profile is loaded at startup.

See [docs/CONFIGURATION.md](docs/CONFIGURATION.md) for all settings with defaults and valid ranges.

## Dependency Injection Wiring

`ServiceRegistration.AddSkyWatchServices()` (`src/SkyWatch.Host/Extensions/ServiceRegistration.cs`) registers:

- `SkyWatchOptions` and `AlpacaSettings` from configuration sections
- `ProfileManager` and the active `ProfileSettings` (singleton)
- `IFrameSource` via `FrameSourceFactory.Create()` (singleton)
- `IFrameAnalyzer` as `FrameAnalyzerPipeline` (singleton)
- `ISafetyStateStore` as `SafetyStateStore` (singleton)
- `ISafetyEvaluator` as `SafetyEvaluatorEngine` (singleton)
- Alpaca controllers and discovery via `AddSkyWatchAlpaca()`
- Web UI via `AddSkyWatchWeb()`
- `SkyWatchWorker` as a hosted background service

## Safety Decision Engine

The decision engine is rule-based and deterministic (no ML in the default path).

**Rule evaluation** -- Six rules run in sequence, each returning `UnsafeReason` flags that are combined with bitwise OR. Any nonzero result means unsafe.

**Hysteresis** -- `HysteresisTracker` (`src/SkyWatch.Safety/HysteresisTracker.cs`) prevents oscillation near threshold boundaries:
- **Unsafe transition**: requires `UnsafeDebounceSeconds` (default 10s) of sustained unsafe readings. Fast response to protect equipment.
- **Safe transition**: requires `SafeRecoveryDebounceSeconds` (default 300s / 5 minutes) of sustained safe readings AND at least `MinimumUnsafeHoldSeconds` (default 60s) since the last unsafe transition. Slow recovery to confirm stability.

**Fail-safe default** -- On startup (warmup period), during errors, or when data is unavailable, the system reports unsafe. Equipment protection takes priority over imaging time.

See [docs/ALGORITHMS.md](docs/ALGORITHMS.md) for detailed algorithm descriptions.

## ASCOM Alpaca Integration

`SafetyMonitorController` (`src/SkyWatch.Alpaca/Controllers/SafetyMonitorController.cs`) implements the ASCOM Alpaca SafetyMonitor device type (interface version 3):

- **Web/API URL**: configurable via `Urls` in `appsettings.json` (default `http://0.0.0.0:5050`)
- **UDP discovery**: port 32227 (standard Alpaca discovery, configurable)
- **Device type**: SafetyMonitor, device number 0
- **Primary endpoint**: `GET /api/v1/safetymonitor/0/issafe`
- **Other endpoints**: `connected`, `connecting`, `connect`, `disconnect`, `description`, `devicestate`, `driverinfo`, `driverversion`, `interfaceversion`, `name`, `supportedactions`, `action`, `commandblind`, `commandbool`, `commandstring`

Any ASCOM Alpaca client (NINA, SGPro, Voyager, etc.) can discover and query SkyWatch as a standard SafetyMonitor device.

## Web UI -- React SPA

The web interface is a React + Material UI single-page application built with Vite. In production, the SPA is compiled to static files and served by the ASP.NET Core backend. In development, the Vite dev server (port 5173) proxies API requests to the backend (port 5050).

The SPA provides six pages: Dashboard, Sources, Thresholds, Timing, Profiles, and System. All configuration can be managed through the UI without editing JSON files.

The REST API is designed for N.I.N.A. plugin compatibility, exposing endpoints for capture settings, threshold management, timing configuration, and manual safety overrides.

**API endpoints** (beyond Alpaca):

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/capture` | GET/PUT | Camera source configuration |
| `/api/thresholds` | GET/PUT | Per-metric safe/unsafe thresholds |
| `/api/timing` | GET/PUT | Debounce, hysteresis, warmup settings |
| `/api/override` | GET/PUT | Manual safety override mode |
| `/api/profiles` | GET/POST/DELETE | Profile management |
| `/api/status` | GET | Current safety state and metrics |
| `/ws/live` | WebSocket | Real-time streaming (see below) |

## Real-time Communication

SkyWatch uses a WebSocket endpoint at `/ws/live` for real-time data streaming to the web UI. The WebSocket pushes:

- Current safety state and evaluation details
- Live sky metrics (brightness, star count, cloudiness, entropy, edge density, obstruction score)
- Camera frame preview (base64-encoded JPEG)
- State transition events

This provides low-latency updates without polling, enabling the dashboard to display a live camera preview and real-time metric charts.

## Extension Points

- **`IFrameSource`** -- Implement for new camera types (e.g., ZWO ASI, AllSky cameras). Register via `FrameSourceFactory`.
- **`ISafetyRule`** -- Add custom safety rules beyond the built-in six metric thresholds. Add to the list in `SafetyEvaluatorEngine.CreateRules()`.
- **Future: ONNX models** -- AI-based sky classification as an alternative or supplement to the deterministic metric analysis.
