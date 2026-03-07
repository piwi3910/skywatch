# SkyWatch — Full Implementation Plan

## 1. Project Overview

**SkyWatch** is a cross-platform sky safety monitoring system for astrophotography and observatory automation.

Its primary purpose is to use a locally connected camera or network camera to determine whether imaging conditions are safe, then expose that state to astronomy software through **ASCOM Alpaca**.

### Core goals
- Detect unsafe sky conditions from a camera feed.
- Expose a standards-compatible **SafetyMonitor** device for NINA and other clients.
- Optionally expose **ObservingConditions** values derived from image analysis.
- Run on **Windows, Linux, and Raspberry Pi-class systems**.
- Support **USB webcams**, **RTSP/IP cameras**, and later Pi-native camera sources.
- Provide a **local web UI** for configuration, tuning, diagnostics, and live preview.
- Be modular enough to add **AI-based classification** later without changing the public device interface.

### Primary deployment targets
1. **Local Windows mode**
   - Runs on the same Windows machine as NINA.
   - USB webcam connected directly to the imaging PC.

2. **Observatory appliance mode**
   - Runs on Raspberry Pi or Linux mini PC near the telescope.
   - Camera connected locally or via network.
   - NINA connects over LAN using Alpaca.

3. **Remote camera mode**
   - Uses RTSP/IP camera stream.
   - SkyWatch runs on any machine on the same network.

---

## 2. Product Scope

### v1 mandatory scope
- Cross-platform .NET 8 service/application.
- Camera capture abstraction.
- USB webcam support.
- RTSP stream support.
- Static image replay mode for testing/debugging.
- Frame processing pipeline.
- Safety state engine.
- ASCOM Alpaca **SafetyMonitor** support.
- Local web configuration UI.
- Logging, diagnostics, and debug image capture.
- Safe/unsafe rules based on deterministic image processing.
- User-defined masks and thresholds.

### v1.1 / near-term scope
- ASCOM Alpaca **ObservingConditions** support.
- Lens obstruction and condensation confidence.
- Historical charts and event timeline.
- Automatic profile switching by time or brightness.
- Watchdog / health monitoring.
- Optional Windows service and Linux systemd packaging.

### v2 scope
- AI-assisted classification.
- Cloud type classification.
- Rain / droplets / fog detection using ONNX models.
- Multi-camera support.
- Panoramic or all-sky camera support.
- Predictive safety trend estimation.
- Plugin/event actions and webhook integrations.

---

## 3. Non-Goals for v1

- No full weather station hardware integration in v1.
- No dependency on proprietary cloud services.
- No need for GPU acceleration in v1.
- No requirement to estimate true meteorological humidity, wind, or temperature from camera feed.
- No classic Windows COM ASCOM driver in v1 unless compatibility is proven necessary.
- No mobile app in v1.

---

## 4. Primary Use Cases

### UC-01 — NINA local observatory safety monitor
**Actor:** astrophotographer using NINA on Windows

**Scenario:**
- User runs SkyWatch on the same PC as NINA.
- USB webcam points at the sky or observatory opening.
- SkyWatch evaluates frames every few seconds.
- When conditions become unsafe, SkyWatch reports `IsSafe = false`.
- NINA pauses or aborts the sequence and performs configured safety actions.

**Acceptance criteria:**
- Stable Alpaca SafetyMonitor endpoint.
- IsSafe changes within configurable reaction time.
- User can see why a decision changed.

### UC-02 — Raspberry Pi observatory appliance
**Actor:** user with remote observatory or mini computer near telescope

**Scenario:**
- SkyWatch runs on Raspberry Pi/Linux.
- Camera is attached to Pi or delivered by RTSP.
- NINA on a separate Windows PC connects to SkyWatch via Alpaca over LAN.

**Acceptance criteria:**
- Runs headless.
- Recoverable on reboot.
- Configuration accessible from browser.

### UC-03 — Live diagnostics and threshold tuning
**Actor:** advanced user configuring cloud safety logic

**Scenario:**
- User opens local web UI.
- Views live frame preview, applied masks, star detections, brightness metrics, and current decision reasons.
- Adjusts thresholds.
- Saves profile.

**Acceptance criteria:**
- Live preview updates reliably.
- Threshold changes reflected quickly.
- Settings persistence works across restarts.

### UC-04 — Obstruction detection
**Actor:** user with cover, dome slit issue, insect, dew, or camera misalignment

**Scenario:**
- Camera becomes blocked or lens fogs up.
- Star count collapses or image entropy changes sharply.
- SkyWatch marks unsafe or sensor obstructed.

**Acceptance criteria:**
- Obstruction state identifiable in UI/logs.
- Configurable handling: immediate unsafe or grace period.

### UC-05 — Observing conditions telemetry
**Actor:** user or software wanting richer environmental metrics

**Scenario:**
- SkyWatch exposes derived telemetry such as cloudiness score and sky brightness.
- Clients read values through Alpaca ObservingConditions.

**Acceptance criteria:**
- Metrics available without affecting SafetyMonitor behavior.
- Values documented as estimated/image-derived.

### UC-06 — Replay-based testing
**Actor:** developer or QA engineer

**Scenario:**
- Run SkyWatch against saved image sets or short recorded sequences.
- Validate expected safety outcomes.

**Acceptance criteria:**
- Deterministic replay support.
- Output comparable against expected labels.

---

## 5. High-Level Architecture

```text
Frame Source -> Preprocessing -> Vision Analysis -> Safety Engine -> Device API -> Clients/UI
```

### Layer breakdown
1. **Frame Source Layer**
   - Retrieves frames from supported camera inputs.
2. **Preprocessing Layer**
   - Resizes, crops, masks, converts color spaces, normalizes exposure-dependent metrics.
3. **Vision Analysis Layer**
   - Produces measurements from frames.
4. **Safety Engine**
   - Converts measurements into final states and confidence.
5. **Device API Layer**
   - Exposes Alpaca SafetyMonitor and later ObservingConditions.
6. **Host/UI Layer**
   - Configuration, diagnostics, logs, preview, and service control.

---

## 6. Technology Stack

### Language and runtime
- **C#**
- **.NET 8**

### Core libraries
- **ASP.NET Core** — API hosting and local web UI backend
- **OpenCvSharp** — image capture and image processing
- **Serilog** — structured logging
- **System.Text.Json** — serialization
- **ONNX Runtime** — optional v2 AI inference

### Frontend
- Minimal **web UI**
- Recommended frontend approach:
  - simple Razor Pages or Blazor Server for v1
  - avoid heavy SPA complexity unless necessary

### Storage
- JSON configuration for v1
- Optional SQLite in v1.1+ for event/history storage

### Packaging
- Self-contained .NET publish for Windows/Linux/ARM64
- Windows installer later
- systemd unit for Linux/Pi later

---

## 7. Solution Structure

```text
SkyWatch.sln
│
├── SkyWatch.Core
├── SkyWatch.Capture
├── SkyWatch.Vision
├── SkyWatch.Safety
├── SkyWatch.Alpaca
├── SkyWatch.Web
├── SkyWatch.Host
├── SkyWatch.Contracts
├── SkyWatch.Tests
├── SkyWatch.IntegrationTests
└── SkyWatch.Tools.Replay
```

### Project responsibilities

#### SkyWatch.Contracts
Shared DTOs, interfaces, enums, and state models.

#### SkyWatch.Core
Configuration models, shared utilities, validation, constants, profile logic.

#### SkyWatch.Capture
Frame source abstraction and concrete implementations.

#### SkyWatch.Vision
All image analysis logic.

#### SkyWatch.Safety
Rule engine and state decision logic.

#### SkyWatch.Alpaca
SafetyMonitor and later ObservingConditions endpoints/services.

#### SkyWatch.Web
Web UI, local status dashboard, preview streaming, settings endpoints.

#### SkyWatch.Host
Application bootstrap, dependency injection, background workers, platform startup.

#### SkyWatch.Tests
Unit tests.

#### SkyWatch.IntegrationTests
End-to-end tests, replay tests, API tests.

#### SkyWatch.Tools.Replay
Standalone tool to replay images/videos for tuning and validation.

---

## 8. Core Domain Model

### Core enums
- `SafetyState`
  - Safe
  - Unsafe
  - Unknown
  - Initializing
  - CameraError
  - Obstructed

- `UnsafeReason`
  - CloudCoverHigh
  - BrightnessTooHigh
  - StarCountTooLow
  - ObstructionDetected
  - LensCondensationDetected
  - RainSuspected
  - CameraDisconnected
  - AnalysisFailed
  - ManualUnsafe

### Core models
- `FrameData`
- `FrameAnalysisResult`
- `SafetyEvaluation`
- `SkyMetrics`
- `CaptureStatus`
- `MaskDefinition`
- `ProfileSettings`
- `SystemHealthStatus`

### Key interfaces

```csharp
public interface IFrameSource
{
    Task StartAsync(CancellationToken ct);
    Task<FrameData?> GetLatestFrameAsync(CancellationToken ct);
    Task StopAsync(CancellationToken ct);
}
```

```csharp
public interface IFrameAnalyzer
{
    Task<FrameAnalysisResult> AnalyzeAsync(FrameData frame, CancellationToken ct);
}
```

```csharp
public interface ISafetyEvaluator
{
    SafetyEvaluation Evaluate(FrameAnalysisResult analysis, DateTimeOffset now);
}
```

```csharp
public interface ISafetyStateStore
{
    SafetyEvaluation Current { get; }
    void Update(SafetyEvaluation evaluation);
}
```

---

## 9. Camera Input Support

### v1 supported sources
1. **USB webcam**
2. **RTSP stream**
3. **Replay source** (folder of images or test video)

### v1.1+ supported sources
4. Pi-native CSI camera if needed
5. HTTP snapshot camera
6. Multi-source aggregation

### Frame source implementations
- `UsbCameraFrameSource`
- `RtspFrameSource`
- `ReplayFrameSource`
- `PiCameraFrameSource` (later)

### Required features
- configurable resolution
- configurable frame rate / polling interval
- reconnect logic
- timeout handling
- source health state
- last successful frame timestamp

### Configuration parameters
- source type
- source identifier / device index / URL
- frame interval
- target resolution
- crop region
- rotation / flip
- exposure guidance metadata

---

## 10. Vision Features and Algorithms

### v1 deterministic metrics

#### 10.1 Average brightness
Use mean luminance of masked sky region.

**Purpose:**
- detect daytime/twilight
- detect sudden brightening from local lights
- contextualize star visibility

#### 10.2 Star count estimation
Detect local bright point sources in dark conditions.

**Purpose:**
- infer sky clarity
- detect clouds or obstruction

**Approach:**
- grayscale conversion
- noise reduction
- adaptive thresholding / blob detection
- filter blobs by size/shape/contrast

#### 10.3 Cloudiness score
Estimate cloud cover from combination of:
- star count drop relative to baseline
- local contrast reduction
- sky texture flattening
- brightness scatter / patchiness

#### 10.4 Image entropy / texture score
Measure scene complexity.

**Purpose:**
- detect blocked lens
- detect heavy fog/dew/cover
- identify unusual static obstruction

#### 10.5 Edge density
Detect amount of visible structure or sharpness.

**Purpose:**
- identify blur from condensation
- detect extreme haze or out-of-focus conditions

#### 10.6 Frame delta / temporal consistency
Compare current metrics against recent history.

**Purpose:**
- reduce false positives
- detect rapid degradation
- allow grace periods/hysteresis

#### 10.7 Obstruction detection
Use combination of:
- sudden entropy drop
- large dark/bright uniform regions
- persistent low star count with atypical texture
- mask-specific region occlusion

#### 10.8 Rain / droplet suspicion heuristic
Basic v1 heuristic only.

**Potential indicators:**
- bright streaks
- circular blur artifacts
- repeated lens highlights
- abrupt contrast collapse

This remains low-confidence in v1 and should be configurable.

### v1 optional derived metrics
- sky brightness score
- confidence score
- clarity trend
- recovery trend

### v2 AI-assisted metrics
- cloud class
- fog class
- droplets on lens class
- birds/insects/foreign object class
- twilight vs cloud confusion resolution

---

## 11. Safety Decision Engine

The safety engine must be deterministic, explainable, and tunable.

### Inputs
- brightness
- star count
- cloudiness score
- entropy
- edge density
- obstruction score
- time history
- camera health
- manual override state

### Outputs
- final `SafetyState`
- boolean `IsSafe`
- reason codes
- confidence level
- human-readable explanation
- last state change timestamp

### Decision rules

#### Example logic
- If camera disconnected -> Unsafe or Unknown (configurable)
- If manual override unsafe -> Unsafe
- If obstruction score exceeds threshold -> Unsafe
- If brightness above day threshold -> Unsafe or Daylight state
- If star count below minimum for sustained period and cloudiness high -> Unsafe
- If transient degradation lasts less than grace period -> remain previous state
- If metrics recover beyond recovery threshold for sustained period -> Safe

### Required safety controls
- hysteresis
- debounce duration
- warm-up period after startup
- minimum unsafe hold time
- minimum safe hold time
- confidence threshold
- fallback policy when no recent frame exists

### Manual controls
- manual force safe
- manual force unsafe
- automatic mode

Manual force safe should be disabled by default or require explicit advanced-user acknowledgement.

---

## 12. Alpaca Device Support

### v1
Implement **SafetyMonitor** only.

Required behavior:
- expose connection status
- expose `IsSafe`
- expose device identity metadata
- stable HTTP endpoints
- support polling by clients

### v1.1
Implement **ObservingConditions** with estimated/image-derived values.

Potential exposed values:
- cloud cover estimate
- sky brightness estimate
- sky quality index
- image clarity score
- sensor obstruction score

Values must be clearly documented as **camera-derived estimates**, not calibrated meteorological measurements.

### Device metadata
- device name
- version
- platform
- camera source description
- current profile

---

## 13. Web UI Requirements

### Main dashboard
- current safety state
- IsSafe indicator
- reason/explanation
- current frame preview
- recent metrics
- camera health
- last state change

### Configuration pages
1. **Camera source settings**
2. **Mask editor**
3. **Thresholds and rules**
4. **Profiles**
5. **Alpaca status**
6. **Logs and diagnostics**
7. **Replay/test tools**

### Preview/diagnostic overlays
- masked region
- detected stars
- obstruction heatmap (if available)
- brightness histogram
- metric summary overlay

### Profiles
Support profiles such as:
- all-sky camera
- observatory slit camera
- narrow sky window camera
- urban sky webcam
- highly sensitive mode
- conservative safety mode

---

## 14. Logging and Diagnostics

### Logging requirements
- structured logs
- configurable log level
- rolling file logs
- important events clearly tagged

### Must-log events
- application start/stop
- camera connect/disconnect
- configuration load/save
- state change safe/unsafe
- analysis errors
- source timeout/reconnect
- manual override usage

### Diagnostics artifacts
- optional saved debug frame on state change
- optional periodic snapshot archive
- metrics history for last 24h / 7d
- exportable diagnostics bundle

---

## 15. Configuration Model

### Categories
- capture settings
- preprocessing settings
- masks
- thresholds
- timing/hysteresis
- fallback behavior
- logging
- Alpaca settings
- UI settings

### Example configurable thresholds
- maximum average brightness
- minimum star count
- maximum cloudiness score
- obstruction threshold
- unsafe debounce seconds
- safe recovery debounce seconds
- stale frame timeout
- reconnect interval

### Persistence
- JSON file per profile
- current active profile marker
- schema version for migration

---

## 16. Failure Modes and Handling

### Failure cases
1. camera disconnected
2. camera returns corrupted frames
3. RTSP stream unavailable
4. frame analysis throws exception
5. no frames within timeout
6. local clock skew or stale data issue
7. UI unavailable but host still running
8. low disk space due to logs/debug frames

### Required behavior
- fail safe by default
- expose degraded health state
- provide clear operator message
- automatic reconnect attempts
- avoid crash loops
- cap debug artifact retention

### Failure policy options
- `Unknown treated as Unsafe`
- `Unknown treated as Safe` (advanced only)

Default should be fail-safe.

---

## 17. Security and Networking

### v1 assumptions
- primarily local LAN use
- local web UI only
- no public internet exposure required

### Required controls
- bind host/port configuration
- optional localhost-only mode
- optional LAN mode
- basic API key or local auth for config pages in v1.1 if needed
- CORS locked down by default

### Not required in v1
- cloud account system
- remote relay service
- internet-facing device discovery

---

## 18. Performance Targets

### v1 target behavior
- process 1 frame every 1–5 seconds by default
- UI preview at low frequency if resource constrained
- usable on modest Windows PC and Raspberry Pi-class hardware

### Constraints
- low CPU mode available
- configurable processing interval
- optional lower-resolution analysis mode
- avoid unnecessary memory allocations

### Success target
- safety decision latency under configured polling interval + debounce
- no unbounded memory growth

---

## 19. Testing Strategy

### Unit tests
- thresholds
- hysteresis behavior
- reason selection
- config validation
- fallback behavior

### Integration tests
- frame source startup/shutdown
- Alpaca endpoint behavior
- replay scenario validation
- profile persistence

### Replay dataset tests
Build a labeled dataset with scenarios:
- clear sky
- thin clouds
- heavy clouds
- bright twilight
- blocked lens
- dew/condensation
- rain droplets
- camera disconnected
- indoor accidental camera orientation

### Manual validation tests
- connect from NINA
- verify IsSafe transitions
- verify startup/shutdown stability
- verify Windows and Linux packaging

---

## 20. Developer Workflow

### Branching
- feature branches
- replay datasets committed separately or in test assets package
- strict PR review for safety logic changes

### Coding standards
- nullable enabled
- analyzers enabled
- async where appropriate
- clear separation between analysis and transport/UI
- no UI logic in safety engine

### CI goals
- build on Windows/Linux
- run unit/integration tests
- publish artifacts for x64 and ARM64

---

## 21. MVP Milestones

### Milestone 0 — Foundation
- solution scaffolding
- dependency injection
- logging
- config load/save
- basic web host

### Milestone 1 — Capture
- USB webcam input
- replay input
- basic preview frame endpoint
- reconnect logic

### Milestone 2 — Vision basics
- brightness metric
- entropy metric
- simple obstruction detection
- debug overlays

### Milestone 3 — Star/cloud analysis
- star count
- cloudiness estimation
- temporal smoothing

### Milestone 4 — Safety engine
- deterministic rules
- hysteresis
- manual override
- reason codes

### Milestone 5 — Alpaca SafetyMonitor
- endpoint implementation
- NINA compatibility test
- state polling validation

### Milestone 6 — Web UI and tuning
- dashboard
- threshold editor
- mask editor
- event log

### Milestone 7 — Packaging
- Windows self-contained package
- Linux self-contained package
- basic deployment docs

### Milestone 8 — RTSP support
- network camera source
- timeout/reconnect
- source switching

### Milestone 9 — ObservingConditions (optional near-term)
- expose image-derived metrics
- docs on semantics/limitations

---

## 22. Detailed Feature Backlog

### Core platform
- [ ] Cross-platform host
- [ ] Structured logging
- [ ] Config schema versioning
- [ ] Profile management
- [ ] Health/status model

### Capture
- [ ] USB camera enumeration
- [ ] Select source index
- [ ] RTSP connection
- [ ] Replay mode
- [ ] Rotation/flip
- [ ] Crop support
- [ ] FPS/poll interval control

### Vision
- [ ] Grayscale conversion
- [ ] Masked region support
- [ ] Brightness calculation
- [ ] Histogram calculation
- [ ] Star detection
- [ ] Cloudiness score
- [ ] Entropy score
- [ ] Edge density score
- [ ] Obstruction score
- [ ] Time-series smoothing

### Safety
- [ ] Rule engine
- [ ] Hysteresis
- [ ] Debounce timers
- [ ] Fallback policy
- [ ] Confidence score
- [ ] Human-readable explanations

### Alpaca
- [ ] SafetyMonitor endpoints
- [ ] Connection metadata
- [ ] State exposure
- [ ] ObservingConditions endpoints (later)

### UI
- [ ] Dashboard
- [ ] Live preview
- [ ] Overlay toggle
- [ ] Mask editor
- [ ] Threshold editor
- [ ] Logs page
- [ ] History chart
- [ ] Test/replay page

### Ops
- [ ] Windows packaging
- [ ] Linux/systemd packaging
- [ ] ARM64 publish
- [ ] Crash recovery strategy

### AI later
- [ ] ONNX abstraction
- [ ] Model runner
- [ ] Cloud classifier
- [ ] Lens droplet classifier
- [ ] Fog/haze classifier

---

## 23. Proposed Initial Configuration Defaults

### Conservative defaults
- startup warm-up: 30 seconds
- frame interval: 3 seconds
- stale frame timeout: 15 seconds
- unsafe debounce: 10 seconds
- safe recovery debounce: 30 seconds
- save debug frames on state change: enabled
- fail-safe on camera loss: enabled

These should be profile-dependent and adjustable.

---

## 24. Risks and Mitigations

### Risk: false unsafe due to urban light pollution
**Mitigation:**
- user masks
- environment-specific profiles
- combine brightness with star count and texture

### Risk: false safe during thin cloud
**Mitigation:**
- temporal analysis
- relative baseline tracking
- conservative thresholds

### Risk: webcam auto-exposure creates noisy metrics
**Mitigation:**
- normalize metrics
- document preferred camera settings
- support camera-specific tuning guidance

### Risk: RTSP stream instability
**Mitigation:**
- reconnect logic
- stale source timeout
- source health state

### Risk: users misunderstand estimated ObservingConditions values
**Mitigation:**
- label clearly as derived estimates
- document calibration limitations

---

## 25. Documentation Deliverables

The AI coder should also generate the following docs:

1. **README.md**
   - overview
   - quick start
   - supported platforms
   - screenshots

2. **ARCHITECTURE.md**
   - module boundaries
   - data flow
   - extension points

3. **CONFIGURATION.md**
   - all settings and profiles

4. **ALGORITHMS.md**
   - brightness
   - star detection
   - cloudiness
   - obstruction detection

5. **DEPLOYMENT.md**
   - Windows setup
   - Linux/Pi setup
   - NINA connection instructions

6. **TROUBLESHOOTING.md**
   - common errors and fixes

---

## 26. Explicit Build Instructions for the AI Coder

### Phase-by-phase implementation order
1. Scaffold solution and projects.
2. Implement configuration, logging, and host bootstrapping.
3. Implement frame source abstraction and replay source first.
4. Implement USB webcam source.
5. Implement basic frame preview in web UI.
6. Implement brightness, entropy, and histogram metrics.
7. Implement star detection.
8. Implement cloudiness and obstruction logic.
9. Implement safety rule engine with hysteresis.
10. Implement Alpaca SafetyMonitor endpoints.
11. Validate local NINA connection.
12. Add mask editor and threshold tuning UI.
13. Add RTSP support.
14. Add persistence/history.
15. Add ObservingConditions.
16. Add optional AI inference framework.

### Architectural rules
- Keep image analysis independent from Alpaca transport.
- Keep UI independent from safety logic.
- Make all platform-specific code isolated.
- Support headless execution.
- Make replay testing a first-class workflow.
- Every safety transition must include explicit reason metadata.

### Quality rules
- No silent failures.
- All exceptions must be logged with context.
- Safety logic must fail safe by default.
- Avoid magic constants; all thresholds must be configurable.
- Prefer deterministic rules before AI.

---

## 27. Final v1 Definition of Done

SkyWatch v1 is complete when:
- it runs on Windows and Linux with .NET 8,
- supports at least USB webcam and replay mode,
- exposes a working Alpaca SafetyMonitor,
- NINA can connect and respond to `IsSafe`,
- user can configure source, masks, and thresholds through the web UI,
- safety decisions are explainable and logged,
- camera disconnects and stale frames fail safe,
- replay tests cover core weather/obstruction scenarios,
- packaging and setup docs are available.

---

## 28. Stretch Goals

- multi-camera voting
- sky region segmentation
- dome/slit state awareness
- webhook notifications
- MQTT publishing
- Home Assistant integration
- AI-assisted predictive cloud arrival
- astronomy-specific sky quality scoring
- image archive for observatory review

---

## 29. One-Sentence Brief for the AI Coder

Build **SkyWatch** as a modular, cross-platform **.NET 8** application that ingests camera frames, computes deterministic sky safety metrics, evaluates a fail-safe safety state, exposes that state through **ASCOM Alpaca SafetyMonitor**, and provides a local web UI for configuration, tuning, diagnostics, and future expansion into ObservingConditions and AI-based classification.

