# SkyWatch Architecture

This document describes the internal architecture of SkyWatch. For setup and usage, see [README.md](README.md).

## Pipeline Overview

```
Frame Source -> Preprocessing -> Vision Analysis -> Safety Engine -> Device API -> Clients/UI
```

A camera frame is captured, analyzed for sky metrics, evaluated against safety thresholds, and the resulting safe/unsafe state is exposed via ASCOM Alpaca and the web UI.

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
        +-- SkyWatch.Web       (+ Safety, Vision, Capture)

SkyWatch.Host               (all projects — composition root)
```

`SkyWatch.Contracts` is the leaf dependency: every other project references it. `SkyWatch.Host` is the composition root that wires everything together via dependency injection.

## Data Flow

The system processes frames through three stages, each producing a distinct data type:

```
FrameData  ->  FrameAnalysisResult (SkyMetrics)  ->  SafetyEvaluation
```

1. **Frame acquisition** -- An `IFrameSource` implementation (webcam, RTSP, or replay) produces `FrameData` containing the raw image bytes, timestamp, and source metadata.

2. **Vision analysis** -- `IFrameAnalyzer` receives `FrameData` and produces a `FrameAnalysisResult` containing `SkyMetrics`: brightness, star count, cloudiness, entropy, edge density, and obstruction flags. Analysis respects any configured mask regions.

3. **Safety evaluation** -- `ISafetyEvaluator` compares `SkyMetrics` against the configured `ThresholdSettings` and produces a `SafetyEvaluation` with the current state (safe/unsafe), a list of reasons for the decision, and a confidence score.

4. **State persistence** -- `ISafetyStateStore` holds the current `SafetyEvaluation`, applies debounce/hysteresis logic, and fires change events consumed by the Alpaca API and web UI.

## Key Interfaces

| Interface | Project | Responsibility |
|-----------|---------|---------------|
| `IFrameSource` | Contracts | Produces camera frames (`FrameData`) |
| `IFrameAnalyzer` | Contracts | Analyzes frames into `FrameAnalysisResult` with `SkyMetrics` |
| `ISafetyEvaluator` | Contracts | Evaluates metrics against thresholds, produces `SafetyEvaluation` |
| `ISafetyStateStore` | Contracts | Holds current safety state, applies debounce, fires change events |

## Configuration Model

Configuration uses a profile-based system with JSON persistence:

```
ProfileSettings
  +-- CaptureSettings      (source type, URI, resolution, interval)
  +-- ThresholdSettings    (per-metric safe/unsafe thresholds)
  +-- TimingSettings       (debounce durations, hysteresis)
  +-- Mask regions         (areas to exclude from analysis)
```

Each profile is stored as a separate JSON file. The active profile is loaded at startup and can be switched at runtime through the web UI.

## Safety Decision Engine

The decision engine is rule-based and deterministic (no ML in the default path).

**Hysteresis** -- Each metric has separate safe and unsafe thresholds to prevent oscillation near boundary values.

**Asymmetric debounce** -- State transitions use different timing depending on direction:
- **Unsafe transition**: fast (~10 seconds debounce). When conditions deteriorate, the system responds quickly to protect equipment.
- **Safe transition**: slow (~5 minutes debounce). When conditions improve, the system waits to confirm stability before resuming operations.

**Fail-safe default** -- On startup, during errors, or when data is unavailable, the system reports unsafe. Equipment protection takes priority over imaging time.

## ASCOM Alpaca Integration

SkyWatch implements the ASCOM Alpaca SafetyMonitor device type:

- **HTTP API**: port 11111 (configurable)
- **UDP discovery**: port 32227 (standard Alpaca discovery)
- **Device type**: SafetyMonitor
- **Primary endpoint**: `GET /api/v1/safetymonitor/0/issafe`

Any ASCOM Alpaca client (NINA, SGPro, Voyager, etc.) can discover and query SkyWatch as a standard SafetyMonitor device.

## Extension Points

- **`IFrameSource`** -- Implement for new camera types (e.g., ZWO ASI, AllSky cameras)
- **`ISafetyRule`** -- Add custom safety rules beyond the built-in metric thresholds
- **Future: ONNX models** -- AI-based sky classification as an alternative or supplement to the deterministic metric analysis
