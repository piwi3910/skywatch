# SkyWatch Implementation Design

## Overview

Full implementation of SkyWatch — a cross-platform .NET 8 sky safety monitoring system for astrophotography. This design covers the phased build-out using parallel agents, worktrees, and PRs.

## Phasing Strategy

### Phase 0 — Foundation (sequential, on main)

Everything else depends on this. Built directly on `main`:

- Solution scaffold with all 11 `.csproj` files
- `Directory.Build.props` — nullable, implicit usings, TreatWarningsAsErrors, analyzer packages
- `.editorconfig` — C# style rules
- `SkyWatch.Contracts` — all interfaces, enums, DTOs, state models
- `SkyWatch.Core` — configuration models, profile logic, validation, constants
- `SkyWatch.Host` — minimal ASP.NET Core bootstrap with DI and Serilog
- GitHub Actions CI (`.github/workflows/ci.yml`) — build, format check, test on ubuntu + windows
- Git pre-commit hook (`.githooks/pre-commit`) — `dotnet format --verify-no-changes`
- `setup-hooks.sh` — one-time developer setup script

### Phase 1 — Core Modules (parallel worktrees)

Four independent branches, each in its own worktree:

| Branch | Project | Scope |
|--------|---------|-------|
| `feature/capture` | SkyWatch.Capture | IFrameSource, ReplayFrameSource, UsbCameraFrameSource, RtspFrameSource, reconnect logic |
| `feature/vision` | SkyWatch.Vision | IFrameAnalyzer, brightness, star count, cloudiness, entropy, edge density, obstruction, frame delta |
| `feature/safety` | SkyWatch.Safety | ISafetyEvaluator, rule engine, hysteresis, debounce, ISafetyStateStore, manual override |
| `feature/tests-phase1` | SkyWatch.Tests | Unit tests for Contracts, Core, Safety |

Each branch creates a PR, passes CI, then merges to main.

### Phase 2 — API & UI (parallel worktrees, after Phase 1 merges)

| Branch | Project | Scope |
|--------|---------|-------|
| `feature/alpaca` | SkyWatch.Alpaca | ASCOM Alpaca SafetyMonitor endpoints, device metadata, connection status |
| `feature/web` | SkyWatch.Web | Dashboard, live preview, threshold editor, mask editor, config pages, logs |
| `feature/tests-phase2` | SkyWatch.Tests | Unit tests for Vision, Capture, Alpaca |

### Phase 3 — Integration & Final Wiring (sequential)

- `feature/integration` — SkyWatch.IntegrationTests, SkyWatch.Tools.Replay
- `feature/host-wiring` — Background worker orchestrating capture→analyze→evaluate loop, full pipeline in Host
- Final documentation pass

## Technical Decisions

### CI Pipeline

- **Trigger**: push and PR to `main`
- **Matrix**: ubuntu-latest, windows-latest
- **Steps**: restore → build (warnings as errors) → format check → test

### Pre-commit Hook

- `.githooks/pre-commit` runs `dotnet format --verify-no-changes`
- Developers run `./setup-hooks.sh` once after cloning
- Hook script configured via `git config core.hooksPath .githooks`

### Analyzers

- `Microsoft.CodeAnalysis.NetAnalyzers` — .NET code quality
- `StyleCop.Analyzers` — style enforcement
- Both configured as errors in `Directory.Build.props`

### Project Dependencies

```
Contracts ← (none)
Core ← Contracts
Capture ← Contracts, Core, OpenCvSharp4
Vision ← Contracts, Core, OpenCvSharp4
Safety ← Contracts, Core
Alpaca ← Contracts, Core, Safety
Web ← Contracts, Core, Safety, Vision, Capture
Host ← All projects
Tests ← Core, Contracts, Safety, Vision, Capture
IntegrationTests ← Host
Tools.Replay ← Capture, Vision, Core
```

### Key NuGet Packages

- OpenCvSharp4 + platform runtimes
- Serilog.AspNetCore
- Microsoft.CodeAnalysis.NetAnalyzers
- StyleCop.Analyzers
- xunit, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk
- Moq

## Implementation Rules

1. Never disable linting — fix at the source
2. No stubs — fully implement everything
3. Fix issues immediately — same PR, never deferred
4. Verify before done — build + test + format check
5. Keep docs current — README, ARCHITECTURE.md, CLAUDE.md, diagrams
6. No unilateral architecture decisions — ask first
7. Research first — look at existing solutions before coding
8. Immediate remediation — stop and fix any issue before continuing
