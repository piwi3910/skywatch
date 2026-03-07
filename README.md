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
- **Web UI** -- live preview, threshold tuning, mask editing, configuration management
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
# Web UI: http://localhost:5000
# Health: http://localhost:5000/health
# Alpaca: http://localhost:11111/api/v1/safetymonitor/0/issafe
```

## Development

```bash
dotnet build SkyWatch.sln          # Build
dotnet test SkyWatch.sln           # Run all tests
dotnet format SkyWatch.sln         # Fix formatting
dotnet test --filter "FullyQualifiedName~ClassName.MethodName"  # Single test
```

## Project Structure

```
src/
  SkyWatch.Contracts   — Shared interfaces, DTOs, enums (no external dependencies)
  SkyWatch.Core        — Configuration, logging, common services
  SkyWatch.Capture     — Frame sources: webcam, RTSP, replay (OpenCvSharp)
  SkyWatch.Vision      — Frame analysis and sky metrics (OpenCvSharp)
  SkyWatch.Safety      — Safety evaluation engine, state management
  SkyWatch.Alpaca      — ASCOM Alpaca SafetyMonitor HTTP API
  SkyWatch.Web         — Web UI, REST API, SignalR hub

tests/
  SkyWatch.Tests              — Unit tests
  SkyWatch.IntegrationTests   — Integration tests

tools/
  SkyWatch.Tools.Replay       — Image replay utility for development
```

See [ARCHITECTURE.md](ARCHITECTURE.md) for detailed design and data flow.

## License

MIT (TBD)
