# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**SkyWatch** is a cross-platform sky safety monitoring system for astrophotography and observatory automation. It uses a camera feed to determine whether imaging conditions are safe, then exposes that state to astronomy software through ASCOM Alpaca. See [sky_watch_implementation_plan.md](sky_watch_implementation_plan.md) for the full specification.

## Technology Stack

- **Language:** C# on .NET 8
- **API/Web:** ASP.NET Core (Razor Pages or Blazor Server for UI)
- **Image Processing:** OpenCvSharp
- **Logging:** Serilog (structured logging)
- **Serialization:** System.Text.Json
- **Configuration:** JSON files (per-profile)
- **Future AI:** ONNX Runtime

## Build & Run Commands

```bash
dotnet build SkyWatch.sln
dotnet run --project src/SkyWatch.Host
dotnet test SkyWatch.sln              # run all tests (119: 111 unit + 8 integration)
dotnet test tests/SkyWatch.Tests      # unit tests only
dotnet test tests/SkyWatch.IntegrationTests  # integration tests only
dotnet test --filter "FullyQualifiedName~ClassName.MethodName"  # single test
dotnet format SkyWatch.sln --verify-no-changes  # check formatting
dotnet publish src/SkyWatch.Host -c Release -r win-x64 --self-contained
dotnet publish src/SkyWatch.Host -c Release -r linux-arm64 --self-contained

# Replay tool -- run analysis against saved images
dotnet run --project tools/SkyWatch.Tools.Replay -- --source <dir> [--profile <name>] [--output <report.json>]
```

## Solution Architecture

```
Frame Source -> Preprocessing -> Vision Analysis -> Safety Engine -> Device API -> Clients/UI
```

### Projects and Responsibilities

| Project | Role |
|---------|------|
| **SkyWatch.Contracts** | Shared DTOs, interfaces, enums, state models. No dependencies on other projects. |
| **SkyWatch.Core** | Configuration models, utilities, validation, constants, profile logic. |
| **SkyWatch.Capture** | Frame source abstraction (`IFrameSource`) and implementations (USB, RTSP, Replay). |
| **SkyWatch.Vision** | Image analysis (`IFrameAnalyzer`): brightness, star count, cloudiness, entropy, edge density, obstruction. |
| **SkyWatch.Safety** | Rule engine (`ISafetyEvaluator`), hysteresis, debounce, state store. |
| **SkyWatch.Alpaca** | ASCOM Alpaca SafetyMonitor (and later ObservingConditions) endpoints. |
| **SkyWatch.Web** | Web UI: dashboard, preview streaming, settings, mask editor, threshold tuning. |
| **SkyWatch.Host** | Application bootstrap, DI wiring (`ServiceRegistration`), background worker (`SkyWatchWorker`). Entry point. |
| **SkyWatch.Tests** | Unit tests (111 tests). |
| **SkyWatch.IntegrationTests** | Integration and API tests (8 tests). |
| **SkyWatch.Tools.Replay** | Standalone CLI tool for replaying image directories against the analysis pipeline. |

### Key Interfaces

- `IFrameSource` — camera abstraction (start/stop/get frame)
- `IFrameAnalyzer` — produces `FrameAnalysisResult` from `FrameData`
- `ISafetyEvaluator` — converts analysis results into `SafetyEvaluation`
- `ISafetyStateStore` — holds and exposes current safety state

### Core Enums

- `SafetyState`: Safe, Unsafe, Unknown, Initializing, CameraError, Obstructed
- `UnsafeReason`: CloudCoverHigh, BrightnessTooHigh, StarCountTooLow, ObstructionDetected, LensCondensationDetected, RainSuspected, CameraDisconnected, AnalysisFailed, ManualUnsafe

## Architectural Rules

- Image analysis must be independent from Alpaca transport layer.
- UI must be independent from safety logic.
- Platform-specific code must be isolated.
- Support headless execution (no UI dependency).
- Replay testing is a first-class workflow.
- Every safety state transition must include explicit reason metadata.
- Safety logic must fail safe by default.
- No magic constants — all thresholds must be configurable.
- Prefer deterministic rules before AI.
- Nullable reference types enabled. Analyzers enabled.
- Async where appropriate.

## Coding Conventions

- No silent failures — all exceptions logged with context.
- No UI logic in the safety engine.
- Clear separation between analysis and transport/UI layers.

## Mandatory Implementation Rules

These rules are non-negotiable and apply to all work in this repository:

1. **Never disable linting or analyzers** — fix every warning/error at the source. No `#pragma warning disable`, no `[SuppressMessage]`, no `--no-verify`, no removing analyzer rules.
2. **No stubs or placeholders** — every method, class, and feature must be fully implemented when committed. No `throw new NotImplementedException()`, no `// TODO: implement later`.
3. **Fix issues immediately** — linting errors, test failures, build warnings get fixed in the same PR, never deferred to a follow-up.
4. **Verify before claiming done** — every PR must build, pass all tests, and be verified before marking complete. Run `dotnet build`, `dotnet test`, and `dotnet format --verify-no-changes` before declaring success.
5. **Keep all docs current** — README.md, ARCHITECTURE.md, CLAUDE.md, diagrams, and examples must be updated with every change that affects them. No stale documentation.
6. **No unilateral architecture decisions** — always ask the user before making choices about patterns, libraries, data flow, or structural changes.
7. **Research first** — look at how others have solved similar problems (ASCOM Alpaca implementations, OpenCvSharp patterns, sky monitoring tools) before writing code. Use web search to find reference implementations.
8. **Immediate remediation** — if any issue surfaces (lint, test, build), stop current work and fix it before continuing. Never push a broken build.
