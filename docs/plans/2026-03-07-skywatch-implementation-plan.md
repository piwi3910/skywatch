# SkyWatch Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build SkyWatch as a modular, cross-platform .NET 8 application that ingests camera frames, computes deterministic sky safety metrics, evaluates a fail-safe safety state, exposes it through ASCOM Alpaca SafetyMonitor, and provides a local web UI.

**Architecture:** Pipeline: Frame Source -> Preprocessing -> Vision Analysis -> Safety Engine -> Device API -> Clients/UI. Each layer is a separate .NET project with clean interface boundaries. Host project wires everything via DI and runs a background worker on a timer.

**Tech Stack:** .NET 8, ASP.NET Core, OpenCvSharp4, Serilog, System.Text.Json, xUnit, Moq. ASCOM official NuGet packages for Alpaca types.

---

## Phase 0: Foundation (Sequential, on main)

### Task 1: Solution Scaffold

**Files:**
- Create: `SkyWatch.sln`
- Create: `src/SkyWatch.Contracts/SkyWatch.Contracts.csproj`
- Create: `src/SkyWatch.Core/SkyWatch.Core.csproj`
- Create: `src/SkyWatch.Capture/SkyWatch.Capture.csproj`
- Create: `src/SkyWatch.Vision/SkyWatch.Vision.csproj`
- Create: `src/SkyWatch.Safety/SkyWatch.Safety.csproj`
- Create: `src/SkyWatch.Alpaca/SkyWatch.Alpaca.csproj`
- Create: `src/SkyWatch.Web/SkyWatch.Web.csproj`
- Create: `src/SkyWatch.Host/SkyWatch.Host.csproj`
- Create: `tests/SkyWatch.Tests/SkyWatch.Tests.csproj`
- Create: `tests/SkyWatch.IntegrationTests/SkyWatch.IntegrationTests.csproj`
- Create: `tools/SkyWatch.Tools.Replay/SkyWatch.Tools.Replay.csproj`
- Create: `Directory.Build.props`
- Create: `.editorconfig`
- Create: `.gitignore`

**Step 1: Create .gitignore for .NET**

```
bin/
obj/
*.user
*.suo
.vs/
*.DotSettings.user
TestResults/
```

**Step 2: Create Directory.Build.props**

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.NetAnalyzers" Version="9.0.0">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
    <PackageReference Include="StyleCop.Analyzers" Version="1.2.0-beta.556">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
  </ItemGroup>
</Project>
```

**Step 3: Create .editorconfig**

Standard C# editorconfig with:
- `indent_style = space`, `indent_size = 4`
- `dotnet_sort_system_directives_first = true`
- `csharp_style_namespace_declarations = file_scoped:warning`
- `csharp_style_var_for_built_in_types = true:suggestion`
- StyleCop rules: disable SA1101 (this prefix), SA1200 (using placement), SA1633 (file header)
- Enable all other StyleCop and CA rules

**Step 4: Create solution and all projects**

```bash
export PATH="/opt/homebrew/opt/dotnet@8/bin:$PATH"
export DOTNET_ROOT="/opt/homebrew/opt/dotnet@8/libexec"

dotnet new sln -n SkyWatch
mkdir -p src tests tools

# Library projects
dotnet new classlib -n SkyWatch.Contracts -o src/SkyWatch.Contracts
dotnet new classlib -n SkyWatch.Core -o src/SkyWatch.Core
dotnet new classlib -n SkyWatch.Capture -o src/SkyWatch.Capture
dotnet new classlib -n SkyWatch.Vision -o src/SkyWatch.Vision
dotnet new classlib -n SkyWatch.Safety -o src/SkyWatch.Safety
dotnet new classlib -n SkyWatch.Alpaca -o src/SkyWatch.Alpaca
dotnet new classlib -n SkyWatch.Web -o src/SkyWatch.Web
dotnet new web -n SkyWatch.Host -o src/SkyWatch.Host

# Test projects
dotnet new xunit -n SkyWatch.Tests -o tests/SkyWatch.Tests
dotnet new xunit -n SkyWatch.IntegrationTests -o tests/SkyWatch.IntegrationTests

# Tool project
dotnet new console -n SkyWatch.Tools.Replay -o tools/SkyWatch.Tools.Replay

# Add all to solution
dotnet sln add src/SkyWatch.Contracts
dotnet sln add src/SkyWatch.Core
dotnet sln add src/SkyWatch.Capture
dotnet sln add src/SkyWatch.Vision
dotnet sln add src/SkyWatch.Safety
dotnet sln add src/SkyWatch.Alpaca
dotnet sln add src/SkyWatch.Web
dotnet sln add src/SkyWatch.Host
dotnet sln add tests/SkyWatch.Tests
dotnet sln add tests/SkyWatch.IntegrationTests
dotnet sln add tools/SkyWatch.Tools.Replay
```

**Step 5: Add project references**

```bash
# Core depends on Contracts
dotnet add src/SkyWatch.Core reference src/SkyWatch.Contracts

# Capture depends on Contracts, Core
dotnet add src/SkyWatch.Capture reference src/SkyWatch.Contracts
dotnet add src/SkyWatch.Capture reference src/SkyWatch.Core

# Vision depends on Contracts, Core
dotnet add src/SkyWatch.Vision reference src/SkyWatch.Contracts
dotnet add src/SkyWatch.Vision reference src/SkyWatch.Core

# Safety depends on Contracts, Core
dotnet add src/SkyWatch.Safety reference src/SkyWatch.Contracts
dotnet add src/SkyWatch.Safety reference src/SkyWatch.Core

# Alpaca depends on Contracts, Core, Safety
dotnet add src/SkyWatch.Alpaca reference src/SkyWatch.Contracts
dotnet add src/SkyWatch.Alpaca reference src/SkyWatch.Core
dotnet add src/SkyWatch.Alpaca reference src/SkyWatch.Safety

# Web depends on Contracts, Core, Safety, Vision, Capture
dotnet add src/SkyWatch.Web reference src/SkyWatch.Contracts
dotnet add src/SkyWatch.Web reference src/SkyWatch.Core
dotnet add src/SkyWatch.Web reference src/SkyWatch.Safety
dotnet add src/SkyWatch.Web reference src/SkyWatch.Vision
dotnet add src/SkyWatch.Web reference src/SkyWatch.Capture

# Host depends on all src projects
dotnet add src/SkyWatch.Host reference src/SkyWatch.Contracts
dotnet add src/SkyWatch.Host reference src/SkyWatch.Core
dotnet add src/SkyWatch.Host reference src/SkyWatch.Capture
dotnet add src/SkyWatch.Host reference src/SkyWatch.Vision
dotnet add src/SkyWatch.Host reference src/SkyWatch.Safety
dotnet add src/SkyWatch.Host reference src/SkyWatch.Alpaca
dotnet add src/SkyWatch.Host reference src/SkyWatch.Web

# Tests
dotnet add tests/SkyWatch.Tests reference src/SkyWatch.Contracts
dotnet add tests/SkyWatch.Tests reference src/SkyWatch.Core
dotnet add tests/SkyWatch.Tests reference src/SkyWatch.Safety
dotnet add tests/SkyWatch.Tests reference src/SkyWatch.Vision
dotnet add tests/SkyWatch.Tests reference src/SkyWatch.Capture

# Integration tests
dotnet add tests/SkyWatch.IntegrationTests reference src/SkyWatch.Host
dotnet add tests/SkyWatch.IntegrationTests reference src/SkyWatch.Contracts
dotnet add tests/SkyWatch.IntegrationTests reference src/SkyWatch.Core

# Replay tool
dotnet add tools/SkyWatch.Tools.Replay reference src/SkyWatch.Capture
dotnet add tools/SkyWatch.Tools.Replay reference src/SkyWatch.Vision
dotnet add tools/SkyWatch.Tools.Replay reference src/SkyWatch.Core
dotnet add tools/SkyWatch.Tools.Replay reference src/SkyWatch.Contracts
```

**Step 6: Add NuGet packages**

```bash
# OpenCvSharp (Capture and Vision)
dotnet add src/SkyWatch.Capture package OpenCvSharp4
dotnet add src/SkyWatch.Vision package OpenCvSharp4

# Platform runtimes (conditionally referenced in .csproj)
# Windows: OpenCvSharp4.runtime.win
# Linux: OpenCvSharp4.official.runtime.linux-x64
# macOS: OpenCvSharp4.runtime.osx_arm64

# Serilog (Host)
dotnet add src/SkyWatch.Host package Serilog.AspNetCore

# Test packages (already added by xunit template, add Moq)
dotnet add tests/SkyWatch.Tests package Moq
dotnet add tests/SkyWatch.IntegrationTests package Moq
dotnet add tests/SkyWatch.IntegrationTests package Microsoft.AspNetCore.Mvc.Testing
```

After adding packages, manually edit `SkyWatch.Capture.csproj` and `SkyWatch.Vision.csproj` to use conditional runtime references:

```xml
<ItemGroup>
  <PackageReference Include="OpenCvSharp4" Version="4.13.0.20260302" />
  <PackageReference Include="OpenCvSharp4.runtime.win" Version="4.13.0.20260214"
                    Condition="$([MSBuild]::IsOSPlatform('Windows'))" />
  <PackageReference Include="OpenCvSharp4.official.runtime.linux-x64" Version="4.13.0.20260302"
                    Condition="$([MSBuild]::IsOSPlatform('Linux'))" />
  <PackageReference Include="OpenCvSharp4.runtime.osx_arm64" Version="4.8.1-rc"
                    Condition="$([MSBuild]::IsOSPlatform('OSX'))" />
</ItemGroup>
```

**Step 7: Build and verify**

```bash
dotnet build SkyWatch.sln
dotnet format --verify-no-changes
```
Expected: Build succeeds with zero warnings, format check passes.

**Step 8: Commit**

```bash
git add -A
git commit -m "feat: scaffold solution with all projects, analyzers, and editorconfig"
```

---

### Task 2: Contracts Project — Enums, Interfaces, DTOs

**Files:**
- Create: `src/SkyWatch.Contracts/Enums/SafetyState.cs`
- Create: `src/SkyWatch.Contracts/Enums/UnsafeReason.cs`
- Create: `src/SkyWatch.Contracts/Enums/FrameSourceType.cs`
- Create: `src/SkyWatch.Contracts/Enums/ManualOverrideMode.cs`
- Create: `src/SkyWatch.Contracts/Models/FrameData.cs`
- Create: `src/SkyWatch.Contracts/Models/FrameAnalysisResult.cs`
- Create: `src/SkyWatch.Contracts/Models/SafetyEvaluation.cs`
- Create: `src/SkyWatch.Contracts/Models/SkyMetrics.cs`
- Create: `src/SkyWatch.Contracts/Models/CaptureStatus.cs`
- Create: `src/SkyWatch.Contracts/Models/MaskDefinition.cs`
- Create: `src/SkyWatch.Contracts/Models/SystemHealthStatus.cs`
- Create: `src/SkyWatch.Contracts/Interfaces/IFrameSource.cs`
- Create: `src/SkyWatch.Contracts/Interfaces/IFrameAnalyzer.cs`
- Create: `src/SkyWatch.Contracts/Interfaces/ISafetyEvaluator.cs`
- Create: `src/SkyWatch.Contracts/Interfaces/ISafetyStateStore.cs`
- Delete: `src/SkyWatch.Contracts/Class1.cs`

**Step 1: Implement SafetyState enum**

```csharp
namespace SkyWatch.Contracts.Enums;

public enum SafetyState
{
    Initializing,
    Safe,
    Unsafe,
    Unknown,
    CameraError,
    Obstructed,
}
```

**Step 2: Implement UnsafeReason flags enum**

```csharp
namespace SkyWatch.Contracts.Enums;

[Flags]
public enum UnsafeReason
{
    None = 0,
    CloudCoverHigh = 1 << 0,
    BrightnessTooHigh = 1 << 1,
    StarCountTooLow = 1 << 2,
    ObstructionDetected = 1 << 3,
    LensCondensationDetected = 1 << 4,
    RainSuspected = 1 << 5,
    CameraDisconnected = 1 << 6,
    AnalysisFailed = 1 << 7,
    ManualUnsafe = 1 << 8,
}
```

**Step 3: Implement FrameSourceType and ManualOverrideMode**

```csharp
namespace SkyWatch.Contracts.Enums;

public enum FrameSourceType
{
    UsbCamera,
    Rtsp,
    Replay,
}
```

```csharp
namespace SkyWatch.Contracts.Enums;

public enum ManualOverrideMode
{
    Automatic,
    ForceSafe,
    ForceUnsafe,
}
```

**Step 4: Implement FrameData**

```csharp
namespace SkyWatch.Contracts.Models;

public sealed class FrameData
{
    public required byte[] ImageBytes { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required int Channels { get; init; }
    public required DateTimeOffset CapturedAt { get; init; }
    public required FrameSourceType SourceType { get; init; }
}
```

Note: `FrameData` uses `byte[]` rather than `Mat` to keep Contracts free of OpenCvSharp dependency. Capture implementations convert `Mat` -> `byte[]` at the boundary.

**Step 5: Implement SkyMetrics**

```csharp
namespace SkyWatch.Contracts.Models;

public sealed class SkyMetrics
{
    public double AverageBrightness { get; init; }
    public int StarCount { get; init; }
    public double CloudinessScore { get; init; }
    public double Entropy { get; init; }
    public double EdgeDensity { get; init; }
    public double ObstructionScore { get; init; }
    public double Confidence { get; init; }
}
```

**Step 6: Implement FrameAnalysisResult**

```csharp
namespace SkyWatch.Contracts.Models;

public sealed class FrameAnalysisResult
{
    public required SkyMetrics Metrics { get; init; }
    public required DateTimeOffset AnalyzedAt { get; init; }
    public required DateTimeOffset FrameCapturedAt { get; init; }
    public required bool AnalysisSucceeded { get; init; }
    public string? ErrorMessage { get; init; }
}
```

**Step 7: Implement SafetyEvaluation**

```csharp
namespace SkyWatch.Contracts.Models;

public sealed class SafetyEvaluation
{
    public required SafetyState State { get; init; }
    public required bool IsSafe { get; init; }
    public required UnsafeReason Reasons { get; init; }
    public required double Confidence { get; init; }
    public required string Explanation { get; init; }
    public required DateTimeOffset EvaluatedAt { get; init; }
    public DateTimeOffset? LastStateChange { get; init; }
}
```

**Step 8: Implement CaptureStatus**

```csharp
namespace SkyWatch.Contracts.Models;

public sealed class CaptureStatus
{
    public required bool IsConnected { get; init; }
    public required FrameSourceType SourceType { get; init; }
    public required string SourceDescription { get; init; }
    public DateTimeOffset? LastFrameAt { get; init; }
    public int FramesCaptured { get; init; }
    public string? ErrorMessage { get; init; }
}
```

**Step 9: Implement MaskDefinition**

```csharp
namespace SkyWatch.Contracts.Models;

public sealed class MaskDefinition
{
    public required string Name { get; init; }
    public required MaskShape Shape { get; init; }
    public required int X { get; init; }
    public required int Y { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public bool IsExclusion { get; init; } = true;
}

public enum MaskShape
{
    Rectangle,
    Ellipse,
}
```

**Step 10: Implement SystemHealthStatus**

```csharp
namespace SkyWatch.Contracts.Models;

public sealed class SystemHealthStatus
{
    public required bool IsHealthy { get; init; }
    public required CaptureStatus CaptureStatus { get; init; }
    public required SafetyEvaluation CurrentSafety { get; init; }
    public required DateTimeOffset Uptime { get; init; }
    public required string Version { get; init; }
}
```

**Step 11: Implement interfaces**

`IFrameSource`:
```csharp
namespace SkyWatch.Contracts.Interfaces;

public interface IFrameSource : IAsyncDisposable
{
    Task StartAsync(CancellationToken cancellationToken);
    Task<FrameData?> GetLatestFrameAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
    CaptureStatus GetStatus();
}
```

`IFrameAnalyzer`:
```csharp
namespace SkyWatch.Contracts.Interfaces;

public interface IFrameAnalyzer
{
    Task<FrameAnalysisResult> AnalyzeAsync(FrameData frame, CancellationToken cancellationToken);
}
```

`ISafetyEvaluator`:
```csharp
namespace SkyWatch.Contracts.Interfaces;

public interface ISafetyEvaluator
{
    SafetyEvaluation Evaluate(FrameAnalysisResult analysis, DateTimeOffset now);
}
```

`ISafetyStateStore`:
```csharp
namespace SkyWatch.Contracts.Interfaces;

public interface ISafetyStateStore
{
    SafetyEvaluation Current { get; }
    void Update(SafetyEvaluation evaluation);
    event EventHandler<SafetyEvaluation>? StateChanged;
}
```

**Step 12: Build and verify**

```bash
dotnet build SkyWatch.sln
dotnet format --verify-no-changes
```

**Step 13: Commit**

```bash
git add -A
git commit -m "feat: add all contracts - enums, models, and interfaces"
```

---

### Task 3: Core Project — Configuration, Profiles, Utilities

**Files:**
- Create: `src/SkyWatch.Core/Configuration/ProfileSettings.cs`
- Create: `src/SkyWatch.Core/Configuration/CaptureSettings.cs`
- Create: `src/SkyWatch.Core/Configuration/ThresholdSettings.cs`
- Create: `src/SkyWatch.Core/Configuration/TimingSettings.cs`
- Create: `src/SkyWatch.Core/Configuration/AlpacaSettings.cs`
- Create: `src/SkyWatch.Core/Configuration/SkyWatchOptions.cs`
- Create: `src/SkyWatch.Core/Configuration/ProfileManager.cs`
- Create: `src/SkyWatch.Core/Validation/SettingsValidator.cs`
- Delete: `src/SkyWatch.Core/Class1.cs`

**Step 1: Implement SkyWatchOptions (root config)**

```csharp
namespace SkyWatch.Core.Configuration;

public sealed class SkyWatchOptions
{
    public const string SectionName = "SkyWatch";
    public string ActiveProfile { get; set; } = "default";
    public string ProfilesDirectory { get; set; } = "profiles";
    public string LogsDirectory { get; set; } = "logs";
    public string DebugFramesDirectory { get; set; } = "debug-frames";
    public bool SaveDebugFramesOnStateChange { get; set; } = true;
}
```

**Step 2: Implement CaptureSettings**

```csharp
namespace SkyWatch.Core.Configuration;

public sealed class CaptureSettings
{
    public FrameSourceType SourceType { get; set; } = FrameSourceType.Replay;
    public string SourceIdentifier { get; set; } = string.Empty;
    public int DeviceIndex { get; set; }
    public int FrameIntervalSeconds { get; set; } = 3;
    public int TargetWidth { get; set; } = 1280;
    public int TargetHeight { get; set; } = 720;
    public int ReconnectIntervalSeconds { get; set; } = 5;
    public int StaleFrameTimeoutSeconds { get; set; } = 15;
    public int ConnectionTimeoutSeconds { get; set; } = 10;
    public bool FlipHorizontal { get; set; }
    public bool FlipVertical { get; set; }
    public int RotationDegrees { get; set; }
}
```

**Step 3: Implement ThresholdSettings**

Based on research, use these defaults from existing sky monitoring tools:

```csharp
namespace SkyWatch.Core.Configuration;

public sealed class ThresholdSettings
{
    public double MaxAverageBrightness { get; set; } = 200.0;
    public int MinStarCount { get; set; } = 10;
    public double MaxCloudinessScore { get; set; } = 0.7;
    public double MinEntropy { get; set; } = 2.0;
    public double MinEdgeDensity { get; set; } = 0.005;
    public double ObstructionThreshold { get; set; } = 0.8;
    public double StarDetectionThreshold { get; set; } = 0.6;
    public double ConfidenceThreshold { get; set; } = 0.5;
}
```

**Step 4: Implement TimingSettings**

Based on research — asymmetric hysteresis: unsafe fast, safe slow.

```csharp
namespace SkyWatch.Core.Configuration;

public sealed class TimingSettings
{
    public int WarmupSeconds { get; set; } = 30;
    public int UnsafeDebounceSeconds { get; set; } = 10;
    public int SafeRecoveryDebounceSeconds { get; set; } = 300;
    public int MinimumUnsafeHoldSeconds { get; set; } = 60;
    public bool FailSafeOnCameraLoss { get; set; } = true;
    public bool FailSafeOnStaleFrame { get; set; } = true;
}
```

**Step 5: Implement AlpacaSettings**

```csharp
namespace SkyWatch.Core.Configuration;

public sealed class AlpacaSettings
{
    public int Port { get; set; } = 11111;
    public int DiscoveryPort { get; set; } = 32227;
    public bool EnableDiscovery { get; set; } = true;
    public string ServerName { get; set; } = "SkyWatch";
    public string Manufacturer { get; set; } = "SkyWatch";
    public string DeviceName { get; set; } = "SkyWatch SafetyMonitor";
    public Guid UniqueId { get; set; } = Guid.NewGuid();
}
```

**Step 6: Implement ProfileSettings (aggregates all settings)**

```csharp
namespace SkyWatch.Core.Configuration;

public sealed class ProfileSettings
{
    public string Name { get; set; } = "default";
    public string Description { get; set; } = "Default profile";
    public CaptureSettings Capture { get; set; } = new();
    public ThresholdSettings Thresholds { get; set; } = new();
    public TimingSettings Timing { get; set; } = new();
    public List<MaskDefinition> Masks { get; set; } = new();
    public ManualOverrideMode ManualOverride { get; set; } = ManualOverrideMode.Automatic;
}
```

**Step 7: Implement ProfileManager**

Handles loading/saving JSON profiles from disk.

```csharp
namespace SkyWatch.Core.Configuration;

public sealed class ProfileManager
{
    private readonly string _profilesDirectory;
    private readonly ILogger<ProfileManager> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public ProfileManager(IOptions<SkyWatchOptions> options, ILogger<ProfileManager> logger) { ... }
    public async Task<ProfileSettings> LoadAsync(string profileName, CancellationToken ct) { ... }
    public async Task SaveAsync(ProfileSettings profile, CancellationToken ct) { ... }
    public IReadOnlyList<string> ListProfiles() { ... }
    public async Task<ProfileSettings> LoadOrCreateDefaultAsync(CancellationToken ct) { ... }
}
```

Full implementation: read/write `{profilesDirectory}/{name}.json`, create default if not exists, enumerate `.json` files for listing.

**Step 8: Implement SettingsValidator**

```csharp
namespace SkyWatch.Core.Validation;

public static class SettingsValidator
{
    public static IReadOnlyList<string> Validate(ProfileSettings settings) { ... }
}
```

Validates: frame interval > 0, target dimensions > 0, thresholds in valid ranges, debounce values >= 0, etc. Returns list of error messages (empty = valid).

**Step 9: Build, format, test, commit**

```bash
dotnet build SkyWatch.sln
dotnet format --verify-no-changes
git add -A
git commit -m "feat: add Core configuration models, profile manager, and validation"
```

---

### Task 4: Host Project — Bootstrap, DI, Serilog

**Files:**
- Modify: `src/SkyWatch.Host/Program.cs`
- Create: `src/SkyWatch.Host/appsettings.json`
- Create: `src/SkyWatch.Host/appsettings.Development.json`

**Step 1: Configure Program.cs**

Minimal ASP.NET Core host with Serilog, configuration binding, and placeholder service registrations for each layer:

```csharp
using Serilog;
using SkyWatch.Core.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine("logs", "skywatch-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30));

builder.Services.Configure<SkyWatchOptions>(
    builder.Configuration.GetSection(SkyWatchOptions.SectionName));
builder.Services.AddSingleton<ProfileManager>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy" }));

app.Run();
```

**Step 2: Configure appsettings.json**

```json
{
  "SkyWatch": {
    "ActiveProfile": "default",
    "ProfilesDirectory": "profiles",
    "LogsDirectory": "logs",
    "DebugFramesDirectory": "debug-frames",
    "SaveDebugFramesOnStateChange": true
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning"
      }
    }
  },
  "AllowedHosts": "*",
  "Urls": "http://0.0.0.0:5000"
}
```

**Step 3: Build, run, verify health endpoint, commit**

```bash
dotnet build SkyWatch.sln
dotnet run --project src/SkyWatch.Host &
curl http://localhost:5000/health
# Expected: {"status":"Healthy"}
kill %1
git add -A
git commit -m "feat: add Host bootstrap with Serilog, config binding, and health endpoint"
```

---

### Task 5: GitHub Actions CI

**Files:**
- Create: `.github/workflows/ci.yml`

**Step 1: Create CI workflow**

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build:
    runs-on: ${{ matrix.os }}
    strategy:
      matrix:
        os: [ubuntu-latest, windows-latest]

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET 8
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x

      - name: Restore
        run: dotnet restore SkyWatch.sln

      - name: Build
        run: dotnet build SkyWatch.sln --no-restore -c Release

      - name: Format check
        run: dotnet format SkyWatch.sln --verify-no-changes --no-restore

      - name: Test
        run: dotnet test SkyWatch.sln --no-build -c Release --logger "trx;LogFileName=test-results.trx"

      - name: Upload test results
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: test-results-${{ matrix.os }}
          path: '**/TestResults/*.trx'
```

**Step 2: Commit and push**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: add GitHub Actions workflow for build, format, and test"
git push origin main
```

**Step 3: Verify CI passes**

```bash
gh run watch
```

---

### Task 6: Git Pre-commit Hook

**Files:**
- Create: `.githooks/pre-commit`
- Create: `setup-hooks.sh`

**Step 1: Create pre-commit hook**

```bash
#!/usr/bin/env bash
set -e

# Only check if there are staged .cs files
STAGED_CS=$(git diff --cached --name-only --diff-filter=ACM | grep '\.cs$' || true)
if [ -z "$STAGED_CS" ]; then
    exit 0
fi

echo "Running dotnet format check..."
dotnet format SkyWatch.sln --verify-no-changes --verbosity quiet
if [ $? -ne 0 ]; then
    echo ""
    echo "ERROR: Code formatting issues detected."
    echo "Run 'dotnet format SkyWatch.sln' to fix them, then re-stage and commit."
    exit 1
fi
```

**Step 2: Create setup script**

```bash
#!/usr/bin/env bash
set -e
git config core.hooksPath .githooks
chmod +x .githooks/pre-commit
echo "Git hooks configured. Pre-commit hook will run dotnet format on staged .cs files."
```

**Step 3: Make executable, commit**

```bash
chmod +x .githooks/pre-commit setup-hooks.sh
./setup-hooks.sh
git add .githooks/ setup-hooks.sh
git commit -m "feat: add pre-commit hook for dotnet format verification"
```

---

### Task 7: Documentation — README, ARCHITECTURE.md

**Files:**
- Create: `README.md`
- Create: `ARCHITECTURE.md`

**Step 1: Write README.md**

Cover: what SkyWatch is, supported platforms, quick start (build, run, configure), project structure overview, development setup (clone, run setup-hooks.sh, dotnet build), license.

**Step 2: Write ARCHITECTURE.md**

Cover: pipeline diagram, project dependency graph, data flow (FrameData -> FrameAnalysisResult -> SafetyEvaluation), key interfaces, configuration model, extension points.

**Step 3: Commit**

```bash
git add README.md ARCHITECTURE.md
git commit -m "docs: add README and ARCHITECTURE documentation"
git push origin main
```

---

## Phase 1: Core Modules (Parallel Worktrees)

After Phase 0 is merged to main, launch 4 parallel agents in isolated worktrees.

### Task 8: SkyWatch.Capture (Branch: feature/capture)

**Files:**
- Create: `src/SkyWatch.Capture/FrameSourceFactory.cs`
- Create: `src/SkyWatch.Capture/Sources/ReplayFrameSource.cs`
- Create: `src/SkyWatch.Capture/Sources/UsbCameraFrameSource.cs`
- Create: `src/SkyWatch.Capture/Sources/RtspFrameSource.cs`
- Create: `src/SkyWatch.Capture/Helpers/MatConverter.cs`
- Delete: `src/SkyWatch.Capture/Class1.cs`
- Test: `tests/SkyWatch.Tests/Capture/ReplayFrameSourceTests.cs`
- Test: `tests/SkyWatch.Tests/Capture/FrameSourceFactoryTests.cs`

**Implementation details:**

`MatConverter` — converts OpenCvSharp `Mat` to `FrameData.ImageBytes` (PNG encoding via `Cv2.ImEncode`) and back (`Cv2.ImDecode`). Centralizes the OpenCvSharp boundary.

`ReplayFrameSource` — reads images from a directory, cycles through them at configured interval. Uses `Directory.GetFiles("*.png", "*.jpg")`, sorts by name, returns sequentially. Must implement `IFrameSource` fully including `GetStatus()`.

`UsbCameraFrameSource` — wraps `VideoCapture(deviceIndex)`. On Windows uses `VideoCaptureAPIs.DSHOW`. Implements reconnect logic: if `capture.Read(frame)` returns empty, attempts reconnect after `ReconnectIntervalSeconds`. Reuses single `Mat` per `VideoCapture.Read()` call to avoid allocation per frame.

`RtspFrameSource` — wraps `VideoCapture(rtspUrl, VideoCaptureAPIs.FFMPEG)`. Sets `OpenTimeoutMsec`. Implements same reconnect pattern as USB. Handles timeout via `StaleFrameTimeoutSeconds`.

`FrameSourceFactory` — given `CaptureSettings`, creates the appropriate `IFrameSource` implementation.

All sources must:
- Track `CaptureStatus` (connected, frame count, last frame time, errors)
- Use `ResourcesTracker` or explicit `using` for all `Mat` objects
- Log connection/disconnection/reconnection events via `ILogger`
- Support `CancellationToken` throughout

**Tests:**
- `ReplayFrameSourceTests`: create temp directory with test images, verify frames returned in order, verify cycling, verify status tracking.
- `FrameSourceFactoryTests`: verify correct source type created for each `FrameSourceType`.

**PR:** Create PR `feature/capture -> main`, verify CI passes, merge.

---

### Task 9: SkyWatch.Vision (Branch: feature/vision)

**Files:**
- Create: `src/SkyWatch.Vision/Analyzers/BrightnessAnalyzer.cs`
- Create: `src/SkyWatch.Vision/Analyzers/StarDetector.cs`
- Create: `src/SkyWatch.Vision/Analyzers/CloudinessAnalyzer.cs`
- Create: `src/SkyWatch.Vision/Analyzers/EntropyCalculator.cs`
- Create: `src/SkyWatch.Vision/Analyzers/EdgeDensityAnalyzer.cs`
- Create: `src/SkyWatch.Vision/Analyzers/ObstructionDetector.cs`
- Create: `src/SkyWatch.Vision/FrameAnalyzerPipeline.cs`
- Create: `src/SkyWatch.Vision/Preprocessing/FramePreprocessor.cs`
- Delete: `src/SkyWatch.Vision/Class1.cs`
- Test: `tests/SkyWatch.Tests/Vision/BrightnessAnalyzerTests.cs`
- Test: `tests/SkyWatch.Tests/Vision/StarDetectorTests.cs`
- Test: `tests/SkyWatch.Tests/Vision/EntropyCalculatorTests.cs`
- Test: `tests/SkyWatch.Tests/Vision/EdgeDensityAnalyzerTests.cs`
- Test: `tests/SkyWatch.Tests/Vision/ObstructionDetectorTests.cs`
- Test: `tests/SkyWatch.Tests/Vision/CloudinessAnalyzerTests.cs`

**Implementation details:**

`FramePreprocessor`:
- Converts `FrameData.ImageBytes` -> `Mat` via `Cv2.ImDecode`
- Resizes if needed
- Converts to grayscale
- Applies masks (fills exclusion regions with black)
- Returns preprocessed `Mat`

`BrightnessAnalyzer`:
- Compute mean luminance of masked grayscale image: `Cv2.Mean(gray).Val0`
- Returns `double` (0-255)

`StarDetector` (based on indi-allsky template matching approach):
- Create 15x15 synthetic star template (white circle radius 3, Gaussian blur 2x2)
- Run `Cv2.MatchTemplate` with `TemplateMatchModes.CCoeffNormed`
- Threshold matches above `StarDetectionThreshold` (default 0.6)
- Deduplicate within 10px distance
- Return count

`CloudinessAnalyzer`:
- Combine: star count relative to baseline, local contrast (stddev of brightness in grid cells), brightness scatter
- Formula: `cloudiness = 1.0 - (starCount / baselineStarCount) * contrastFactor`
- Clamp to [0, 1]

`EntropyCalculator`:
- Shannon entropy via histogram: `Cv2.CalcHist`, iterate 256 bins, `entropy -= p * log2(p)`
- Returns 0.0 to 8.0

`EdgeDensityAnalyzer`:
- `Cv2.GaussianBlur` then `Cv2.Canny(50, 200)`
- `edgeDensity = Cv2.CountNonZero(edges) / totalPixels`

`ObstructionDetector`:
- Combines: entropy drop (< MinEntropy), large uniform regions (check stddev of quadrants), persistent low star count with low edge density
- Returns obstruction score 0.0 to 1.0

`FrameAnalyzerPipeline` (implements `IFrameAnalyzer`):
- Orchestrates all analyzers
- Preprocess -> run all analyzers -> assemble `FrameAnalysisResult`
- Wraps in try/catch, returns failed result on exception
- Uses `ResourcesTracker` for all intermediate `Mat` objects

**Tests:**
- Create test images programmatically (black image, white image, image with dots for stars, uniform gray for obstruction)
- Verify brightness returns expected values for known images
- Verify star detector finds expected count on synthetic star field
- Verify entropy is ~0 for uniform image, higher for noisy image
- Verify edge density is ~0 for uniform, higher for edges
- Verify obstruction score high for uniform dark image

**PR:** Create PR `feature/vision -> main`, verify CI, merge.

---

### Task 10: SkyWatch.Safety (Branch: feature/safety)

**Files:**
- Create: `src/SkyWatch.Safety/SafetyEvaluatorEngine.cs`
- Create: `src/SkyWatch.Safety/SafetyStateStoreImpl.cs`
- Create: `src/SkyWatch.Safety/Rules/BrightnessRule.cs`
- Create: `src/SkyWatch.Safety/Rules/StarCountRule.cs`
- Create: `src/SkyWatch.Safety/Rules/CloudinessRule.cs`
- Create: `src/SkyWatch.Safety/Rules/ObstructionRule.cs`
- Create: `src/SkyWatch.Safety/Rules/ISafetyRule.cs`
- Create: `src/SkyWatch.Safety/HysteresisTracker.cs`
- Delete: `src/SkyWatch.Safety/Class1.cs`
- Test: `tests/SkyWatch.Tests/Safety/SafetyEvaluatorEngineTests.cs`
- Test: `tests/SkyWatch.Tests/Safety/HysteresisTrackerTests.cs`
- Test: `tests/SkyWatch.Tests/Safety/SafetyStateStoreTests.cs`
- Test: `tests/SkyWatch.Tests/Safety/Rules/BrightnessRuleTests.cs`
- Test: `tests/SkyWatch.Tests/Safety/Rules/StarCountRuleTests.cs`

**Implementation details:**

`ISafetyRule` (internal interface):
```csharp
internal interface ISafetyRule
{
    UnsafeReason Evaluate(SkyMetrics metrics, ThresholdSettings thresholds);
}
```
Each rule returns `UnsafeReason.None` if safe, or the specific reason flag if unsafe.

`HysteresisTracker`:
- Tracks when a state was first observed
- `ShouldTransition(SafetyState proposed, DateTimeOffset now)` -> bool
- Enforces `UnsafeDebounceSeconds` for safe->unsafe transitions
- Enforces `SafeRecoveryDebounceSeconds` for unsafe->safe transitions
- Enforces `MinimumUnsafeHoldSeconds`
- Tracks warmup period after startup

`SafetyEvaluatorEngine` (implements `ISafetyEvaluator`):
- Takes `IOptions<ProfileSettings>` (or direct injection)
- Runs all rules against metrics
- Combines reason flags
- Applies manual override (ForceUnsafe/ForceSafe)
- Applies hysteresis
- Generates human-readable explanation from reason flags
- Handles camera disconnect (FailSafeOnCameraLoss)
- Handles stale frames (FailSafeOnStaleFrame)

`SafetyStateStoreImpl` (implements `ISafetyStateStore`):
- Thread-safe singleton (uses `lock`)
- Fires `StateChanged` event only when `SafetyState` actually changes
- Initializes with `SafetyState.Initializing`

**Tests:**
- `BrightnessRuleTests`: verify returns `BrightnessTooHigh` when above threshold, `None` when below
- `StarCountRuleTests`: verify returns `StarCountTooLow` when below minimum
- `HysteresisTrackerTests`: verify debounce timing, verify warmup period, verify asymmetric transition delays
- `SafetyEvaluatorEngineTests`: verify full evaluation with various metric combinations, verify manual override, verify explanation generation
- `SafetyStateStoreTests`: verify thread safety, verify event firing on state change, verify no event on same state

**PR:** Create PR `feature/safety -> main`, verify CI, merge.

---

### Task 11: Unit Tests Phase 1 (Branch: feature/tests-phase1)

**Files:**
- Test: `tests/SkyWatch.Tests/Core/ProfileManagerTests.cs`
- Test: `tests/SkyWatch.Tests/Core/SettingsValidatorTests.cs`
- Test: `tests/SkyWatch.Tests/Core/ProfileSettingsTests.cs`

**Implementation details:**

`ProfileManagerTests`:
- Create temp directory, save profile, load profile, verify roundtrip
- Verify `ListProfiles()` returns saved profiles
- Verify `LoadOrCreateDefaultAsync` creates default when none exists

`SettingsValidatorTests`:
- Valid settings returns empty error list
- Zero frame interval returns error
- Negative thresholds return error
- All boundary conditions tested

`ProfileSettingsTests`:
- Verify default values are set correctly
- Verify JSON serialization roundtrip

**PR:** Create PR `feature/tests-phase1 -> main`, verify CI, merge.

---

## Phase 2: API & UI (Parallel Worktrees, after Phase 1 merges)

### Task 12: SkyWatch.Alpaca (Branch: feature/alpaca)

**Files:**
- Create: `src/SkyWatch.Alpaca/Models/AlpacaResponse.cs`
- Create: `src/SkyWatch.Alpaca/Models/AlpacaErrorCode.cs`
- Create: `src/SkyWatch.Alpaca/Controllers/SafetyMonitorController.cs`
- Create: `src/SkyWatch.Alpaca/Controllers/ManagementController.cs`
- Create: `src/SkyWatch.Alpaca/Discovery/AlpacaDiscoveryService.cs`
- Create: `src/SkyWatch.Alpaca/Extensions/AlpacaServiceExtensions.cs`
- Delete: `src/SkyWatch.Alpaca/Class1.cs`
- Test: `tests/SkyWatch.Tests/Alpaca/SafetyMonitorControllerTests.cs`
- Test: `tests/SkyWatch.Tests/Alpaca/ManagementControllerTests.cs`

**Implementation details:**

Based on ASCOM Alpaca API research:

`AlpacaResponse` models:
```csharp
public class AlpacaResponse
{
    public int ErrorNumber { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public uint ClientTransactionID { get; set; }
    public uint ServerTransactionID { get; set; }
}
public class AlpacaResponse<T> : AlpacaResponse
{
    public T? Value { get; set; }
}
```

`SafetyMonitorController`:
- All endpoints under `/api/v1/safetymonitor/{deviceNumber}/`
- GET `issafe` -> reads from `ISafetyStateStore.Current.IsSafe`
- GET `connected` -> returns connection state
- PUT `connect` / `disconnect` -> manages connection
- GET `name`, `description`, `driverinfo`, `driverversion`, `interfaceversion`, `supportedactions`, `devicestate`
- PUT `action`, `commandblind`, `commandbool`, `commandstring`
- Tracks `ServerTransactionID` as incrementing counter
- Returns 200 with error codes in body per Alpaca spec

`ManagementController`:
- GET `/management/apiversions` -> `[1]`
- GET `/management/v1/description` -> server name, manufacturer, version
- GET `/management/v1/configureddevices` -> list with SafetyMonitor device

`AlpacaDiscoveryService`:
- `BackgroundService` that listens on UDP port 32227
- Responds to `"alpacadiscovery1"` with `{"AlpacaPort": <port>}`
- Handles `SO_REUSEADDR` for cross-platform

`AlpacaServiceExtensions`:
- `IServiceCollection.AddAlpaca()` — registers controller, discovery service

**Tests:**
- Mock `ISafetyStateStore`, verify `issafe` returns correct value
- Verify response format matches Alpaca spec
- Verify management endpoints return correct metadata
- Verify unknown device number returns error

**PR:** Create PR, CI, merge.

---

### Task 13: SkyWatch.Web (Branch: feature/web)

**Files:**
- Create: `src/SkyWatch.Web/Controllers/DashboardApiController.cs`
- Create: `src/SkyWatch.Web/Controllers/ConfigurationApiController.cs`
- Create: `src/SkyWatch.Web/Controllers/PreviewApiController.cs`
- Create: `src/SkyWatch.Web/wwwroot/index.html`
- Create: `src/SkyWatch.Web/wwwroot/css/styles.css`
- Create: `src/SkyWatch.Web/wwwroot/js/dashboard.js`
- Create: `src/SkyWatch.Web/wwwroot/js/config.js`
- Create: `src/SkyWatch.Web/Extensions/WebServiceExtensions.cs`
- Delete: `src/SkyWatch.Web/Class1.cs`

**Implementation details:**

API Controllers (JSON REST endpoints consumed by frontend):

`DashboardApiController`:
- GET `/api/dashboard/status` -> current safety state, metrics, capture status, health
- GET `/api/dashboard/metrics/history` -> last N metrics readings
- GET `/api/dashboard/events` -> recent state change events

`ConfigurationApiController`:
- GET `/api/config/profile` -> current profile settings
- PUT `/api/config/profile` -> update profile settings (validates first)
- GET `/api/config/profiles` -> list available profiles
- POST `/api/config/profiles/{name}/activate` -> switch active profile
- GET `/api/config/masks` -> current masks
- PUT `/api/config/masks` -> update masks

`PreviewApiController`:
- GET `/api/preview/frame` -> latest frame as JPEG (for img polling)
- GET `/api/preview/frame/annotated` -> frame with overlay (stars marked, mask regions, metrics)

Frontend (vanilla HTML/CSS/JS — no framework, keeps it simple):

`index.html`:
- Single page with sections: status banner, live preview, metrics panel, recent events
- Navigation tabs: Dashboard, Configuration, Masks, Logs
- Auto-refreshes status every 2 seconds via fetch()

`styles.css`:
- Clean dashboard layout, status indicator (green/red/yellow), responsive grid

`dashboard.js`:
- Polls `/api/dashboard/status`, updates DOM
- Polls `/api/preview/frame` for live preview
- Shows metrics charts (simple canvas or inline SVG bars)

`config.js`:
- Loads/saves configuration via API
- Threshold sliders
- Mask editor (click to add rectangle/ellipse regions on preview image)

`WebServiceExtensions`:
- `IServiceCollection.AddSkyWatchWeb()` — registers controllers
- `IApplicationBuilder.UseSkyWatchWeb()` — maps static files, controllers

**PR:** Create PR, CI, merge.

---

### Task 14: Tests Phase 2 (Branch: feature/tests-phase2)

**Files:**
- Test: `tests/SkyWatch.Tests/Vision/FrameAnalyzerPipelineTests.cs`
- Test: `tests/SkyWatch.Tests/Capture/MatConverterTests.cs`
- Test: `tests/SkyWatch.Tests/Alpaca/AlpacaDiscoveryServiceTests.cs`

**Tests for the full pipeline, converter, and discovery protocol.**

**PR:** Create PR, CI, merge.

---

## Phase 3: Integration & Final Wiring

### Task 15: Host Wiring — Background Worker (Branch: feature/host-wiring)

**Files:**
- Create: `src/SkyWatch.Host/Workers/SkyWatchWorker.cs`
- Create: `src/SkyWatch.Host/Extensions/ServiceRegistration.cs`
- Modify: `src/SkyWatch.Host/Program.cs`

**Implementation details:**

`SkyWatchWorker` (BackgroundService):
- On startup: load active profile, create frame source via factory, start source
- Main loop (every `FrameIntervalSeconds`):
  1. `GetLatestFrameAsync()` from source
  2. If frame is null and stale timeout exceeded, evaluate as camera error
  3. `AnalyzeAsync(frame)` via `IFrameAnalyzer`
  4. `Evaluate(analysis, now)` via `ISafetyEvaluator`
  5. `Update(evaluation)` on `ISafetyStateStore`
  6. If state changed and `SaveDebugFramesOnStateChange`, save frame to debug directory
- On shutdown: stop source, log final state

`ServiceRegistration`:
- Registers all services: `IFrameSource` (via factory), `IFrameAnalyzer`, `ISafetyEvaluator`, `ISafetyStateStore`
- Registers `SkyWatchWorker` as hosted service
- Registers Alpaca services and Web services

Update `Program.cs` to call `ServiceRegistration`.

**PR:** Create PR, CI, merge.

---

### Task 16: Integration Tests (Branch: feature/integration-tests)

**Files:**
- Test: `tests/SkyWatch.IntegrationTests/AlpacaEndpointTests.cs`
- Test: `tests/SkyWatch.IntegrationTests/PipelineIntegrationTests.cs`
- Test: `tests/SkyWatch.IntegrationTests/ReplayScenarioTests.cs`
- Create: `tests/SkyWatch.IntegrationTests/TestAssets/clear_sky.png`
- Create: `tests/SkyWatch.IntegrationTests/TestAssets/cloudy.png`
- Create: `tests/SkyWatch.IntegrationTests/TestAssets/blocked_lens.png`

**Implementation details:**

Use `WebApplicationFactory<Program>` to spin up the full host.

`AlpacaEndpointTests`:
- GET `/api/v1/safetymonitor/0/issafe` returns valid Alpaca response
- GET `/management/apiversions` returns `[1]`
- GET `/management/v1/configureddevices` lists SafetyMonitor

`PipelineIntegrationTests`:
- Configure replay source with test images
- Wait for worker to process frames
- Verify safety state transitions

`ReplayScenarioTests`:
- Clear sky images -> Safe
- Cloudy images -> Unsafe (CloudCoverHigh)
- Blocked lens (uniform dark) -> Unsafe (ObstructionDetected)
- Verify hysteresis timing

Test images: create programmatically in test setup (black, white, star field, uniform gray).

**PR:** Create PR, CI, merge.

---

### Task 17: SkyWatch.Tools.Replay (Branch: feature/replay-tool)

**Files:**
- Modify: `tools/SkyWatch.Tools.Replay/Program.cs`
- Create: `tools/SkyWatch.Tools.Replay/ReplayRunner.cs`

**Implementation:**

CLI tool that:
- Takes arguments: `--source <dir>` `--profile <name>` `--output <report.json>`
- Loads profile, creates ReplayFrameSource, runs pipeline
- Outputs per-frame metrics and safety decisions to JSON report
- Prints summary: total frames, safe/unsafe counts, state transitions

**PR:** Create PR, CI, merge.

---

### Task 18: Final Documentation Pass (Branch: feature/docs-final)

**Files:**
- Update: `README.md` — add screenshots/examples, NINA connection instructions
- Update: `ARCHITECTURE.md` — finalize with actual implementation details
- Update: `CLAUDE.md` — update any changed commands or structure
- Create: `docs/CONFIGURATION.md` — all settings documented
- Create: `docs/ALGORITHMS.md` — brightness, star detection, cloudiness, obstruction algorithms
- Create: `docs/DEPLOYMENT.md` — Windows, Linux, Pi setup instructions

**PR:** Create PR, CI, merge.

---

## Execution Summary

| Phase | Tasks | Parallelizable | Dependencies |
|-------|-------|---------------|--------------|
| 0 | 1-7 | No (sequential) | None |
| 1 | 8-11 | Yes (4 worktrees) | Phase 0 complete |
| 2 | 12-14 | Yes (3 worktrees) | Phase 1 merged |
| 3 | 15-18 | Partially (15 first, then 16-18 parallel) | Phase 2 merged |

Total: 18 tasks across 4 phases.
