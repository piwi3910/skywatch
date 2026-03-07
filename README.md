# SkyWatch

A cross-platform sky safety monitoring system for astrophotography and observatory automation. SkyWatch uses a camera feed to determine whether imaging conditions are safe, then exposes that state to astronomy software through an ASCOM Alpaca SafetyMonitor interface.

## Supported Platforms

| Platform | Architecture |
|----------|-------------|
| Windows  | x64         |
| Linux    | x64, ARM64  |
| macOS    | ARM64       |

Primary deployment targets: Windows PC running [NINA](https://nighttime-imaging.eu/), Raspberry Pi as an observatory appliance.

## Features

- **Camera support** -- USB webcams, RTSP/IP cameras, image replay mode for testing
- **Safety metrics** -- brightness, star count, cloudiness, entropy, edge density, obstruction detection
- **Deterministic safety decision engine** -- configurable hysteresis and debounce; asymmetric timing (fast unsafe, slow safe transitions)
- **ASCOM Alpaca SafetyMonitor** -- standard HTTP API for NINA and other ASCOM clients
- **Web UI** -- React + MUI dark-themed dashboard with live preview, real-time metrics via WebSocket, threshold tuning, and full configuration management (no JSON editing needed)
- **Structured logging** -- Serilog with console and file sinks
- **Profile-based configuration** -- JSON persistence, multiple profiles
- **Fail-safe by default** -- unknown or error states are treated as unsafe

## Quick Start

```bash
# Clone and setup
git clone https://github.com/piwi3910/skywatch.git
cd skywatch
./setup-hooks.sh

# Build and run
dotnet build SkyWatch.sln
dotnet run --project src/SkyWatch.Host

# Access
# Web UI: http://localhost:5050
# Health: http://localhost:5050/health
# Alpaca: http://localhost:11111/api/v1/safetymonitor/0/issafe
```

## Web UI

SkyWatch includes a React + Material UI dark-themed dashboard served by the ASP.NET Core backend on port 5050. All configuration is manageable through the UI -- no JSON editing required.

**Pages:**
- **Dashboard** -- live camera preview, real-time safety status and sky metrics via WebSocket
- **Sources** -- configure camera source (USB, RTSP, replay)
- **Thresholds** -- tune per-metric safe/unsafe thresholds
- **Timing** -- adjust debounce, hysteresis, and warmup durations
- **Profiles** -- create, switch, and manage configuration profiles
- **System** -- application info, logging, and diagnostics

The API is designed for N.I.N.A. plugin compatibility.

## Development

```bash
# Backend
dotnet build SkyWatch.sln
dotnet run --project src/SkyWatch.Host

# Frontend (dev mode with hot reload)
cd src/SkyWatch.Web/ClientApp
npm install
npm run dev
# Vite dev server runs at http://localhost:5173 with proxy to backend

# Tests
dotnet test SkyWatch.sln           # Run all tests (119: 111 unit + 8 integration)
dotnet format SkyWatch.sln         # Fix formatting
dotnet test --filter "FullyQualifiedName~ClassName.MethodName"  # Single test
```

## Replay Tool

The standalone replay tool runs the analysis pipeline against a directory of saved images for threshold tuning and regression testing.

```bash
dotnet run --project tools/SkyWatch.Tools.Replay -- \
  --source /path/to/images \
  --profile myprofile \
  --output report.json
```

See [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md) for full usage and options.

## Project Structure

11 projects across 3 directories:

```
src/
  SkyWatch.Contracts   -- Shared interfaces, DTOs, enums (no external dependencies)
  SkyWatch.Core        -- Configuration models, profile management, utilities
  SkyWatch.Capture     -- Frame sources: USB webcam, RTSP, replay (OpenCvSharp)
  SkyWatch.Vision      -- Frame analysis pipeline and sky metrics (OpenCvSharp)
  SkyWatch.Safety      -- Safety evaluation engine, hysteresis, state management
  SkyWatch.Alpaca      -- ASCOM Alpaca SafetyMonitor HTTP API and discovery
  SkyWatch.Web         -- React + MUI SPA, WebSocket live streaming, REST API
  SkyWatch.Host        -- Application bootstrap, DI wiring, background worker

tests/
  SkyWatch.Tests              -- Unit tests (111 tests)
  SkyWatch.IntegrationTests   -- Integration and API tests (8 tests)

tools/
  SkyWatch.Tools.Replay       -- Standalone image replay utility
```

## Documentation

- [ARCHITECTURE.md](ARCHITECTURE.md) -- internal design, data flow, extension points
- [docs/CONFIGURATION.md](docs/CONFIGURATION.md) -- complete settings reference with defaults
- [docs/ALGORITHMS.md](docs/ALGORITHMS.md) -- image analysis and safety decision algorithms
- [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md) -- Windows, Linux, and Raspberry Pi deployment

## License

MIT (TBD)
