# Web UI Redesign Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace the vanilla HTML/CSS/JS web UI with a React + MUI + WebSocket SPA that allows full configuration management through the browser — no JSON file editing.

**Architecture:** React 18/TypeScript/Vite frontend served by ASP.NET Core. WebSocket at `/ws/live` pushes real-time status, metrics, and preview frames. REST API for CRUD. New `FrameSourceManager` enables live source switching. API surface is complete so a future N.I.N.A. plugin can do everything the web UI can.

**Tech Stack:** React 18, TypeScript, Vite, MUI 6, ASP.NET Core 8 WebSocket, System.Text.Json

---

## Phase 1: Backend — FrameSourceManager & WebSocket

### Task 1: Create FrameSourceManager

The current `IFrameSource` is a singleton created at startup and injected directly. To support live source switching, wrap it in a manager service.

**Files:**
- Create: `src/SkyWatch.Capture/FrameSourceManager.cs`
- Test: `tests/SkyWatch.Tests/Capture/FrameSourceManagerTests.cs`

**Step 1: Write the failing tests**

```csharp
// tests/SkyWatch.Tests/Capture/FrameSourceManagerTests.cs
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SkyWatch.Capture;
using SkyWatch.Contracts.Enums;
using SkyWatch.Contracts.Interfaces;
using SkyWatch.Contracts.Models;
using SkyWatch.Core.Configuration;

namespace SkyWatch.Tests.Capture;

public class FrameSourceManagerTests
{
    private readonly ILoggerFactory loggerFactory = NullLoggerFactory.Instance;

    [Fact]
    public async Task GetLatestFrameAsync_DelegatesToCurrentSource()
    {
        var mockSource = new Mock<IFrameSource>();
        var frame = new FrameData
        {
            ImageBytes = new byte[] { 1, 2, 3 },
            Width = 640,
            Height = 480,
            Channels = 3,
            CapturedAt = DateTimeOffset.UtcNow,
            SourceType = FrameSourceType.UsbCamera,
        };
        mockSource.Setup(s => s.GetLatestFrameAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(frame);
        mockSource.Setup(s => s.GetStatus())
            .Returns(new CaptureStatus
            {
                IsConnected = true,
                SourceType = FrameSourceType.UsbCamera,
                SourceDescription = "test",
            });

        var manager = new FrameSourceManager(loggerFactory);
        await manager.InitializeAsync(
            new CaptureSettings { SourceType = FrameSourceType.Replay, SourceIdentifier = "." },
            CancellationToken.None);

        // Replace with mock
        await manager.SwitchSourceAsync(mockSource.Object, CancellationToken.None);

        var result = await manager.GetLatestFrameAsync(CancellationToken.None);
        Assert.Equal(frame, result);
    }

    [Fact]
    public async Task SwitchSourceAsync_StopsOldSourceAndStartsNew()
    {
        var oldSource = new Mock<IFrameSource>();
        var newSource = new Mock<IFrameSource>();
        newSource.Setup(s => s.GetStatus())
            .Returns(new CaptureStatus
            {
                IsConnected = true,
                SourceType = FrameSourceType.Rtsp,
                SourceDescription = "new",
            });

        var manager = new FrameSourceManager(loggerFactory);
        await manager.SwitchSourceAsync(oldSource.Object, CancellationToken.None);
        await manager.SwitchSourceAsync(newSource.Object, CancellationToken.None);

        oldSource.Verify(s => s.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
        newSource.Verify(s => s.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetStatus_WhenNoSource_ReturnsDisconnectedStatus()
    {
        var manager = new FrameSourceManager(loggerFactory);
        var status = manager.GetStatus();

        Assert.False(status.IsConnected);
        Assert.Equal("No source configured", status.SourceDescription);
    }

    [Fact]
    public async Task SwitchSourceAsync_WhenNewSourceThrows_DoesNotCrash()
    {
        var badSource = new Mock<IFrameSource>();
        badSource.Setup(s => s.StartAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Camera not found"));
        badSource.Setup(s => s.GetStatus())
            .Returns(new CaptureStatus
            {
                IsConnected = false,
                SourceType = FrameSourceType.UsbCamera,
                SourceDescription = "bad",
                ErrorMessage = "Camera not found",
            });

        var manager = new FrameSourceManager(loggerFactory);

        // Should not throw
        await manager.SwitchSourceAsync(badSource.Object, CancellationToken.None);

        var status = manager.GetStatus();
        Assert.False(status.IsConnected);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/SkyWatch.Tests --filter "FullyQualifiedName~FrameSourceManagerTests" -v minimal`
Expected: FAIL — `FrameSourceManager` does not exist

**Step 3: Implement FrameSourceManager**

```csharp
// src/SkyWatch.Capture/FrameSourceManager.cs
using Microsoft.Extensions.Logging;
using SkyWatch.Contracts.Enums;
using SkyWatch.Contracts.Interfaces;
using SkyWatch.Contracts.Models;
using SkyWatch.Core.Configuration;

namespace SkyWatch.Capture;

/// <summary>
/// Manages the active frame source, supporting live switching between sources.
/// </summary>
public sealed partial class FrameSourceManager : IFrameSource
{
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger<FrameSourceManager> logger;
    private readonly SemaphoreSlim switchLock = new(1, 1);
    private IFrameSource? currentSource;

    public FrameSourceManager(ILoggerFactory loggerFactory)
    {
        this.loggerFactory = loggerFactory;
        logger = loggerFactory.CreateLogger<FrameSourceManager>();
    }

    public async Task InitializeAsync(CaptureSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            var source = FrameSourceFactory.Create(settings, loggerFactory);
            await SwitchSourceAsync(source, cancellationToken);
        }
        catch (Exception ex)
        {
            LogInitializationFailed(ex.Message);
        }
    }

    public async Task SwitchSourceAsync(IFrameSource newSource, CancellationToken cancellationToken)
    {
        await switchLock.WaitAsync(cancellationToken);
        try
        {
            if (currentSource is not null)
            {
                try
                {
                    await currentSource.StopAsync(cancellationToken);
                    await currentSource.DisposeAsync();
                }
                catch (Exception ex)
                {
                    LogStopFailed(ex.Message);
                }
            }

            currentSource = newSource;

            try
            {
                await currentSource.StartAsync(cancellationToken);
                LogSourceSwitched(currentSource.GetStatus().SourceDescription);
            }
            catch (Exception ex)
            {
                LogStartFailed(ex.Message);
            }
        }
        finally
        {
            switchLock.Release();
        }
    }

    public async Task SwitchSourceAsync(CaptureSettings settings, CancellationToken cancellationToken)
    {
        var newSource = FrameSourceFactory.Create(settings, loggerFactory);
        await SwitchSourceAsync(newSource, cancellationToken);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Already started during SwitchSourceAsync
        return Task.CompletedTask;
    }

    public async Task<FrameData?> GetLatestFrameAsync(CancellationToken cancellationToken)
    {
        var source = currentSource;
        if (source is null)
        {
            return null;
        }

        return await source.GetLatestFrameAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return currentSource?.StopAsync(cancellationToken) ?? Task.CompletedTask;
    }

    public CaptureStatus GetStatus()
    {
        return currentSource?.GetStatus() ?? new CaptureStatus
        {
            IsConnected = false,
            SourceType = FrameSourceType.Replay,
            SourceDescription = "No source configured",
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (currentSource is not null)
        {
            await currentSource.StopAsync(CancellationToken.None);
            await currentSource.DisposeAsync();
            currentSource = null;
        }

        switchLock.Dispose();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Frame source switched to: {Description}")]
    private partial void LogSourceSwitched(string description);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to stop previous frame source: {ErrorMessage}")]
    private partial void LogStopFailed(string errorMessage);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to start new frame source: {ErrorMessage}")]
    private partial void LogStartFailed(string errorMessage);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to initialize frame source: {ErrorMessage}")]
    private partial void LogInitializationFailed(string errorMessage);
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/SkyWatch.Tests --filter "FullyQualifiedName~FrameSourceManagerTests" -v minimal`
Expected: PASS (all 4 tests)

**Step 5: Commit**

```bash
git add src/SkyWatch.Capture/FrameSourceManager.cs tests/SkyWatch.Tests/Capture/FrameSourceManagerTests.cs
git commit -m "feat: add FrameSourceManager for live source switching"
```

---

### Task 2: Update DI wiring to use FrameSourceManager

Replace the direct `IFrameSource` singleton registration with `FrameSourceManager` so that all consumers (worker, controllers) share the same manager. Register `FrameSourceManager` as both itself and `IFrameSource`.

**Files:**
- Modify: `src/SkyWatch.Host/Extensions/ServiceRegistration.cs`
- Modify: `src/SkyWatch.Host/Workers/SkyWatchWorker.cs`

**Step 1: Update ServiceRegistration**

In `src/SkyWatch.Host/Extensions/ServiceRegistration.cs`, replace the `IFrameSource` factory registration:

```csharp
// Replace this block:
//   services.AddSingleton<IFrameSource>(sp => { ... FrameSourceFactory.Create ... });
// With:
services.AddSingleton<FrameSourceManager>(sp =>
{
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var manager = new FrameSourceManager(loggerFactory);
    var profile = sp.GetRequiredService<ProfileSettings>();
    manager.InitializeAsync(profile.Capture, CancellationToken.None).GetAwaiter().GetResult();
    return manager;
});
services.AddSingleton<IFrameSource>(sp => sp.GetRequiredService<FrameSourceManager>());
```

Add `using SkyWatch.Capture;` to the top of the file.

**Step 2: SkyWatchWorker already takes IFrameSource — no changes needed**

The worker injects `IFrameSource` which now resolves to `FrameSourceManager`. No code changes required in the worker.

**Step 3: Build and run existing tests**

Run: `dotnet build SkyWatch.sln && dotnet test SkyWatch.sln -v minimal`
Expected: Build succeeds, all 119 tests pass

**Step 4: Commit**

```bash
git add src/SkyWatch.Host/Extensions/ServiceRegistration.cs
git commit -m "refactor: wire FrameSourceManager into DI as IFrameSource"
```

---

### Task 3: Add new REST API endpoints for capture, thresholds, timing, and override

Extend `ConfigurationApiController` with dedicated endpoints for each settings section. These endpoints update the profile and (where applicable) apply changes immediately.

**Files:**
- Modify: `src/SkyWatch.Web/Controllers/ConfigurationApiController.cs`
- Test: `tests/SkyWatch.Tests/Web/ConfigurationApiControllerTests.cs` (create)

**Step 1: Write failing tests**

```csharp
// tests/SkyWatch.Tests/Web/ConfigurationApiControllerTests.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SkyWatch.Capture;
using SkyWatch.Contracts.Enums;
using SkyWatch.Core.Configuration;
using SkyWatch.Web.Controllers;

namespace SkyWatch.Tests.Web;

public class ConfigurationApiControllerTests
{
    private readonly Mock<ProfileManager> profileManagerMock;
    private readonly Mock<FrameSourceManager> frameSourceManagerMock;
    private readonly ConfigurationApiController controller;

    public ConfigurationApiControllerTests()
    {
        // ProfileManager needs IOptions<SkyWatchOptions> and ILogger
        var optionsMock = new Mock<IOptions<SkyWatchOptions>>();
        optionsMock.Setup(o => o.Value).Returns(new SkyWatchOptions { ProfilesDirectory = "test-profiles" });

        profileManagerMock = new Mock<ProfileManager>(
            optionsMock.Object,
            NullLogger<ProfileManager>.Instance) { CallBase = false };

        frameSourceManagerMock = new Mock<FrameSourceManager>(
            NullLoggerFactory.Instance) { CallBase = false };

        var optionsMonitor = new Mock<IOptionsMonitor<SkyWatchOptions>>();
        optionsMonitor.Setup(o => o.CurrentValue).Returns(new SkyWatchOptions { ActiveProfile = "default" });

        controller = new ConfigurationApiController(
            profileManagerMock.Object,
            optionsMonitor.Object,
            NullLogger<ConfigurationApiController>.Instance,
            frameSourceManagerMock.Object);
    }

    [Fact]
    public async Task GetCaptureSettings_ReturnsCurrentCaptureSettings()
    {
        var profile = new ProfileSettings
        {
            Name = "default",
            Capture = new CaptureSettings { SourceType = FrameSourceType.UsbCamera, DeviceIndex = 0 },
        };
        profileManagerMock.Setup(p => p.LoadAsync("default", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        var result = await controller.GetCaptureSettings(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var capture = Assert.IsType<CaptureSettings>(ok.Value);
        Assert.Equal(FrameSourceType.UsbCamera, capture.SourceType);
    }

    [Fact]
    public async Task UpdateCaptureSettings_SwitchesSourceAndSaves()
    {
        var profile = new ProfileSettings { Name = "default" };
        profileManagerMock.Setup(p => p.LoadAsync("default", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        profileManagerMock.Setup(p => p.SaveAsync(It.IsAny<ProfileSettings>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var newCapture = new CaptureSettings
        {
            SourceType = FrameSourceType.UsbCamera,
            DeviceIndex = 1,
            TargetWidth = 1280,
            TargetHeight = 720,
        };

        var result = await controller.UpdateCaptureSettings(newCapture, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        frameSourceManagerMock.Verify(
            m => m.SwitchSourceAsync(It.IsAny<CaptureSettings>(), It.IsAny<CancellationToken>()),
            Times.Once);
        profileManagerMock.Verify(
            p => p.SaveAsync(It.Is<ProfileSettings>(ps => ps.Capture.DeviceIndex == 1), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetOverride_ReturnsCurrentOverrideMode()
    {
        var profile = new ProfileSettings
        {
            Name = "default",
            ManualOverride = ManualOverrideMode.ForceSafe,
        };
        profileManagerMock.Setup(p => p.LoadAsync("default", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        var result = await controller.GetOverride(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        // Result is anonymous type with mode property
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("ForceSafe", json);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/SkyWatch.Tests --filter "FullyQualifiedName~ConfigurationApiControllerTests" -v minimal`
Expected: FAIL — new methods do not exist

**Step 3: Add new endpoints to ConfigurationApiController**

Add `FrameSourceManager` to the constructor and add the new endpoints. The constructor signature becomes:

```csharp
public ConfigurationApiController(
    ProfileManager profileManager,
    IOptionsMonitor<SkyWatchOptions> options,
    ILogger<ConfigurationApiController> logger,
    FrameSourceManager frameSourceManager)
```

New endpoints to add:

```csharp
[HttpGet("capture")]
public async Task<IActionResult> GetCaptureSettings(CancellationToken cancellationToken)
{
    var profile = await LoadActiveProfileAsync(cancellationToken);
    return Ok(profile.Capture);
}

[HttpPost("capture")]
public async Task<IActionResult> UpdateCaptureSettings([FromBody] CaptureSettings capture, CancellationToken cancellationToken)
{
    var profile = await LoadActiveProfileAsync(cancellationToken);
    var updated = new ProfileSettings
    {
        Name = profile.Name,
        Description = profile.Description,
        Capture = capture,
        Thresholds = profile.Thresholds,
        Timing = profile.Timing,
        Masks = profile.Masks,
        ManualOverride = profile.ManualOverride,
    };

    var errors = SettingsValidator.Validate(updated);
    if (errors.Count > 0)
    {
        return BadRequest(new { errors });
    }

    await frameSourceManager.SwitchSourceAsync(capture, cancellationToken);
    await profileManager.SaveAsync(updated, cancellationToken);
    return Ok(capture);
}

[HttpGet("thresholds")]
public async Task<IActionResult> GetThresholds(CancellationToken cancellationToken)
{
    var profile = await LoadActiveProfileAsync(cancellationToken);
    return Ok(profile.Thresholds);
}

[HttpPut("thresholds")]
public async Task<IActionResult> UpdateThresholds([FromBody] ThresholdSettings thresholds, CancellationToken cancellationToken)
{
    var profile = await LoadActiveProfileAsync(cancellationToken);
    var updated = new ProfileSettings
    {
        Name = profile.Name,
        Description = profile.Description,
        Capture = profile.Capture,
        Thresholds = thresholds,
        Timing = profile.Timing,
        Masks = profile.Masks,
        ManualOverride = profile.ManualOverride,
    };

    var errors = SettingsValidator.Validate(updated);
    if (errors.Count > 0)
    {
        return BadRequest(new { errors });
    }

    await profileManager.SaveAsync(updated, cancellationToken);
    return Ok(thresholds);
}

[HttpGet("timing")]
public async Task<IActionResult> GetTiming(CancellationToken cancellationToken)
{
    var profile = await LoadActiveProfileAsync(cancellationToken);
    return Ok(profile.Timing);
}

[HttpPut("timing")]
public async Task<IActionResult> UpdateTiming([FromBody] TimingSettings timing, CancellationToken cancellationToken)
{
    var profile = await LoadActiveProfileAsync(cancellationToken);
    var updated = new ProfileSettings
    {
        Name = profile.Name,
        Description = profile.Description,
        Capture = profile.Capture,
        Thresholds = profile.Thresholds,
        Timing = timing,
        Masks = profile.Masks,
        ManualOverride = profile.ManualOverride,
    };

    var errors = SettingsValidator.Validate(updated);
    if (errors.Count > 0)
    {
        return BadRequest(new { errors });
    }

    await profileManager.SaveAsync(updated, cancellationToken);
    return Ok(timing);
}

[HttpGet("override")]
public async Task<IActionResult> GetOverride(CancellationToken cancellationToken)
{
    var profile = await LoadActiveProfileAsync(cancellationToken);
    return Ok(new { mode = profile.ManualOverride.ToString() });
}

[HttpPut("override")]
public async Task<IActionResult> UpdateOverride([FromBody] ManualOverrideRequest request, CancellationToken cancellationToken)
{
    var profile = await LoadActiveProfileAsync(cancellationToken);
    var updated = new ProfileSettings
    {
        Name = profile.Name,
        Description = profile.Description,
        Capture = profile.Capture,
        Thresholds = profile.Thresholds,
        Timing = profile.Timing,
        Masks = profile.Masks,
        ManualOverride = request.Mode,
    };

    await profileManager.SaveAsync(updated, cancellationToken);
    return Ok(new { mode = updated.ManualOverride.ToString() });
}
```

Add a small request DTO at the bottom of the file or in a separate file:

```csharp
public sealed class ManualOverrideRequest
{
    public ManualOverrideMode Mode { get; set; }
}
```

**Step 4: Run tests**

Run: `dotnet build SkyWatch.sln && dotnet test SkyWatch.sln -v minimal`
Expected: All tests pass (existing + new)

**Step 5: Commit**

```bash
git add src/SkyWatch.Web/Controllers/ConfigurationApiController.cs tests/SkyWatch.Tests/Web/ConfigurationApiControllerTests.cs
git commit -m "feat: add REST endpoints for capture, thresholds, timing, override settings"
```

---

### Task 4: Add WebSocket endpoint for real-time updates

Create a WebSocket middleware that pushes status, metrics, and preview frames to connected clients on every analysis cycle.

**Files:**
- Create: `src/SkyWatch.Web/WebSocket/SkyWatchWebSocketHandler.cs`
- Create: `src/SkyWatch.Web/WebSocket/WebSocketConnectionManager.cs`
- Modify: `src/SkyWatch.Host/Program.cs` (add WebSocket middleware)
- Modify: `src/SkyWatch.Web/Extensions/WebServiceExtensions.cs`

**Step 1: Create WebSocketConnectionManager**

```csharp
// src/SkyWatch.Web/WebSocket/WebSocketConnectionManager.cs
using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace SkyWatch.Web.WebSocket;

/// <summary>
/// Tracks all active WebSocket connections and provides broadcast capability.
/// </summary>
public sealed class WebSocketConnectionManager
{
    private readonly ConcurrentDictionary<string, System.Net.WebSockets.WebSocket> connections = new();

    public string AddConnection(System.Net.WebSockets.WebSocket socket)
    {
        var id = Guid.NewGuid().ToString("N");
        connections.TryAdd(id, socket);
        return id;
    }

    public void RemoveConnection(string id)
    {
        connections.TryRemove(id, out _);
    }

    public async Task BroadcastTextAsync(string message, CancellationToken cancellationToken)
    {
        var buffer = System.Text.Encoding.UTF8.GetBytes(message);
        var segment = new ArraySegment<byte>(buffer);

        foreach (var (id, socket) in connections)
        {
            if (socket.State == WebSocketState.Open)
            {
                try
                {
                    await socket.SendAsync(segment, WebSocketMessageType.Text, true, cancellationToken);
                }
                catch
                {
                    RemoveConnection(id);
                }
            }
            else
            {
                RemoveConnection(id);
            }
        }
    }

    public async Task BroadcastBinaryAsync(byte[] data, CancellationToken cancellationToken)
    {
        var segment = new ArraySegment<byte>(data);

        foreach (var (id, socket) in connections)
        {
            if (socket.State == WebSocketState.Open)
            {
                try
                {
                    await socket.SendAsync(segment, WebSocketMessageType.Binary, true, cancellationToken);
                }
                catch
                {
                    RemoveConnection(id);
                }
            }
            else
            {
                RemoveConnection(id);
            }
        }
    }

    public int ConnectionCount => connections.Count;
}
```

**Step 2: Create SkyWatchWebSocketHandler**

```csharp
// src/SkyWatch.Web/WebSocket/SkyWatchWebSocketHandler.cs
using System.Net.WebSockets;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace SkyWatch.Web.WebSocket;

/// <summary>
/// Handles WebSocket upgrade requests and keeps connections alive.
/// </summary>
public sealed partial class SkyWatchWebSocketHandler
{
    private readonly WebSocketConnectionManager connectionManager;
    private readonly ILogger<SkyWatchWebSocketHandler> logger;

    public SkyWatchWebSocketHandler(
        WebSocketConnectionManager connectionManager,
        ILogger<SkyWatchWebSocketHandler> logger)
    {
        this.connectionManager = connectionManager;
        this.logger = logger;
    }

    public async Task HandleAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            return;
        }

        var socket = await context.WebSockets.AcceptWebSocketAsync();
        var connectionId = connectionManager.AddConnection(socket);
        LogClientConnected(connectionId, connectionManager.ConnectionCount);

        try
        {
            await ReceiveLoop(socket, connectionId, context.RequestAborted);
        }
        finally
        {
            connectionManager.RemoveConnection(connectionId);
            LogClientDisconnected(connectionId, connectionManager.ConnectionCount);

            if (socket.State == WebSocketState.Open)
            {
                await socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Closing",
                    CancellationToken.None);
            }
        }
    }

    private static async Task ReceiveLoop(
        System.Net.WebSockets.WebSocket socket,
        string connectionId,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[1024];
        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await socket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
            catch (WebSocketException)
            {
                break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "WebSocket client connected: {ConnectionId} (total: {Count})")]
    private partial void LogClientConnected(string connectionId, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "WebSocket client disconnected: {ConnectionId} (total: {Count})")]
    private partial void LogClientDisconnected(string connectionId, int count);
}
```

**Step 3: Update WebServiceExtensions to register WebSocket services**

```csharp
// In AddSkyWatchWeb:
services.AddSingleton<WebSocketConnectionManager>();
services.AddSingleton<SkyWatchWebSocketHandler>();

// In UseSkyWatchWeb:
app.UseWebSockets();
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/ws/live")
    {
        var handler = context.RequestServices.GetRequiredService<SkyWatchWebSocketHandler>();
        await handler.HandleAsync(context);
    }
    else
    {
        await next(context);
    }
});
```

**Step 4: Update SkyWatchWorker to broadcast via WebSocket**

Modify `src/SkyWatch.Host/Workers/SkyWatchWorker.cs` to inject `WebSocketConnectionManager` and broadcast status + frame data after each analysis cycle.

Add to constructor: `WebSocketConnectionManager webSocketManager`

After `stateStore.Update(evaluation)`, broadcast the status as JSON and the frame as binary:

```csharp
await BroadcastStatusAsync(evaluation, analysis, stoppingToken);
if (frame is not null)
{
    await BroadcastFrameAsync(frame, stoppingToken);
}
```

The `BroadcastStatusAsync` method serializes the evaluation + metrics to a JSON message matching the design spec format and calls `webSocketManager.BroadcastTextAsync`.

The `BroadcastFrameAsync` method sends `frame.ImageBytes` via `webSocketManager.BroadcastBinaryAsync`.

**Step 5: Build and test**

Run: `dotnet build SkyWatch.sln && dotnet test SkyWatch.sln -v minimal`
Expected: Build succeeds, all tests pass

**Step 6: Commit**

```bash
git add src/SkyWatch.Web/WebSocket/ src/SkyWatch.Web/Extensions/WebServiceExtensions.cs src/SkyWatch.Host/Workers/SkyWatchWorker.cs
git commit -m "feat: add WebSocket endpoint for real-time status and preview broadcast"
```

---

### Task 5: Fix static file serving for SPA

The React app's built files need to be served by ASP.NET Core. Configure the Web project to serve static files from the correct location and add SPA fallback routing.

**Files:**
- Modify: `src/SkyWatch.Web/SkyWatch.Web.csproj` (embed wwwroot as content)
- Modify: `src/SkyWatch.Web/Extensions/WebServiceExtensions.cs`
- Modify: `src/SkyWatch.Host/Program.cs`

**Step 1: Update SkyWatch.Web.csproj**

Add content item for wwwroot files to be copied to output:

```xml
<ItemGroup>
    <Content Include="wwwroot\**" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

Later (Task 13), this will be replaced with the React build output via an MSBuild target.

**Step 2: Update WebServiceExtensions.UseSkyWatchWeb**

Configure static file provider to serve from the Web assembly's wwwroot:

```csharp
public static IApplicationBuilder UseSkyWatchWeb(this IApplicationBuilder app)
{
    app.UseWebSockets();

    // WebSocket middleware (from Task 4)
    app.Use(async (context, next) => { ... });

    // Serve static files from SkyWatch.Web's wwwroot
    var webAssemblyPath = Path.GetDirectoryName(typeof(WebServiceExtensions).Assembly.Location)!;
    var webRoot = Path.Combine(webAssemblyPath, "wwwroot");
    if (Directory.Exists(webRoot))
    {
        var fileProvider = new PhysicalFileProvider(webRoot);
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
        app.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider });

        // SPA fallback: serve index.html for non-API, non-file routes
        app.Use(async (context, next) =>
        {
            await next();
            if (context.Response.StatusCode == 404
                && !context.Request.Path.StartsWithSegments("/api")
                && !context.Request.Path.StartsWithSegments("/ws")
                && !Path.HasExtension(context.Request.Path.Value))
            {
                context.Request.Path = "/index.html";
                context.Response.StatusCode = 200;
                await next();
            }
        });
    }

    return app;
}
```

**Step 3: Build and verify**

Run: `dotnet build SkyWatch.sln && dotnet test SkyWatch.sln -v minimal`
Expected: Build succeeds, all tests pass

**Step 4: Commit**

```bash
git add src/SkyWatch.Web/SkyWatch.Web.csproj src/SkyWatch.Web/Extensions/WebServiceExtensions.cs
git commit -m "fix: serve static files from SkyWatch.Web wwwroot with SPA fallback"
```

---

## Phase 2: React App Scaffold

### Task 6: Initialize React + Vite + TypeScript project

**Files:**
- Create: `src/SkyWatch.Web/ClientApp/` (entire React project)

**Step 1: Create the React project**

```bash
cd src/SkyWatch.Web
npm create vite@latest ClientApp -- --template react-ts
cd ClientApp
npm install
npm install @mui/material @mui/icons-material @emotion/react @emotion/styled
npm install react-router-dom
npm install @types/react-router-dom --save-dev
```

**Step 2: Configure Vite proxy**

Create `vite.config.ts`:

```typescript
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5050',
        changeOrigin: true,
      },
      '/ws': {
        target: 'ws://localhost:5050',
        ws: true,
      },
    },
  },
  build: {
    outDir: '../wwwroot',
    emptyOutDir: true,
  },
})
```

**Step 3: Verify dev server starts**

```bash
cd src/SkyWatch.Web/ClientApp
npm run dev
```

Expected: Vite dev server at http://localhost:5173

**Step 4: Commit**

```bash
git add src/SkyWatch.Web/ClientApp/
git commit -m "feat: initialize React + Vite + TypeScript + MUI project"
```

---

### Task 7: Create MUI dark theme and app layout

Set up the MUI dark theme, router, and sidebar navigation layout.

**Files:**
- Create: `src/SkyWatch.Web/ClientApp/src/theme/darkTheme.ts`
- Create: `src/SkyWatch.Web/ClientApp/src/components/Layout.tsx`
- Modify: `src/SkyWatch.Web/ClientApp/src/App.tsx`
- Modify: `src/SkyWatch.Web/ClientApp/src/main.tsx`

**Step 1: Create dark theme**

```typescript
// src/SkyWatch.Web/ClientApp/src/theme/darkTheme.ts
import { createTheme } from '@mui/material/styles';

const darkTheme = createTheme({
  palette: {
    mode: 'dark',
    primary: {
      main: '#58a6ff',
    },
    secondary: {
      main: '#3fb950',
    },
    error: {
      main: '#f85149',
    },
    warning: {
      main: '#d29922',
    },
    background: {
      default: '#0d1117',
      paper: '#161b22',
    },
    text: {
      primary: '#e6edf3',
      secondary: '#8b949e',
    },
  },
  typography: {
    fontFamily: '-apple-system, BlinkMacSystemFont, "Segoe UI", Helvetica, Arial, sans-serif',
  },
  components: {
    MuiCard: {
      styleOverrides: {
        root: {
          backgroundImage: 'none',
          border: '1px solid #30363d',
        },
      },
    },
  },
});

export default darkTheme;
```

**Step 2: Create Layout with sidebar**

```typescript
// src/SkyWatch.Web/ClientApp/src/components/Layout.tsx
import { useState } from 'react';
import { Outlet, useNavigate, useLocation } from 'react-router-dom';
import {
  Box, Drawer, List, ListItemButton, ListItemIcon, ListItemText,
  AppBar, Toolbar, Typography, IconButton, useMediaQuery, useTheme,
} from '@mui/material';
import {
  Dashboard as DashboardIcon,
  Videocam as VideocamIcon,
  Tune as TuneIcon,
  Timer as TimerIcon,
  ManageAccounts as ProfilesIcon,
  Settings as SystemIcon,
  Menu as MenuIcon,
} from '@mui/icons-material';

const DRAWER_WIDTH = 240;

const navItems = [
  { label: 'Dashboard', path: '/', icon: <DashboardIcon /> },
  { label: 'Sources', path: '/sources', icon: <VideocamIcon /> },
  { label: 'Thresholds', path: '/thresholds', icon: <TuneIcon /> },
  { label: 'Timing', path: '/timing', icon: <TimerIcon /> },
  { label: 'Profiles', path: '/profiles', icon: <ProfilesIcon /> },
  { label: 'System', path: '/system', icon: <SystemIcon /> },
];

export default function Layout() {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('md'));
  const [mobileOpen, setMobileOpen] = useState(false);
  const navigate = useNavigate();
  const location = useLocation();

  const drawer = (
    <Box sx={{ overflow: 'auto' }}>
      <Toolbar>
        <Typography variant="h6" noWrap>SkyWatch</Typography>
      </Toolbar>
      <List>
        {navItems.map((item) => (
          <ListItemButton
            key={item.path}
            selected={location.pathname === item.path}
            onClick={() => {
              navigate(item.path);
              if (isMobile) setMobileOpen(false);
            }}
          >
            <ListItemIcon>{item.icon}</ListItemIcon>
            <ListItemText primary={item.label} />
          </ListItemButton>
        ))}
      </List>
    </Box>
  );

  return (
    <Box sx={{ display: 'flex' }}>
      {isMobile && (
        <AppBar position="fixed" sx={{ zIndex: theme.zIndex.drawer + 1 }}>
          <Toolbar>
            <IconButton color="inherit" edge="start" onClick={() => setMobileOpen(!mobileOpen)}>
              <MenuIcon />
            </IconButton>
            <Typography variant="h6" noWrap>SkyWatch</Typography>
          </Toolbar>
        </AppBar>
      )}
      <Drawer
        variant={isMobile ? 'temporary' : 'permanent'}
        open={isMobile ? mobileOpen : true}
        onClose={() => setMobileOpen(false)}
        sx={{ width: DRAWER_WIDTH, '& .MuiDrawer-paper': { width: DRAWER_WIDTH } }}
      >
        {drawer}
      </Drawer>
      <Box component="main" sx={{ flexGrow: 1, p: 3, mt: isMobile ? 8 : 0 }}>
        <Outlet />
      </Box>
    </Box>
  );
}
```

**Step 3: Set up App.tsx with router and placeholder pages**

```typescript
// src/SkyWatch.Web/ClientApp/src/App.tsx
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { ThemeProvider, CssBaseline } from '@mui/material';
import darkTheme from './theme/darkTheme';
import Layout from './components/Layout';
import Dashboard from './pages/Dashboard';
import Sources from './pages/Sources';
import Thresholds from './pages/Thresholds';
import Timing from './pages/Timing';
import Profiles from './pages/Profiles';
import System from './pages/System';

export default function App() {
  return (
    <ThemeProvider theme={darkTheme}>
      <CssBaseline />
      <BrowserRouter>
        <Routes>
          <Route element={<Layout />}>
            <Route path="/" element={<Dashboard />} />
            <Route path="/sources" element={<Sources />} />
            <Route path="/thresholds" element={<Thresholds />} />
            <Route path="/timing" element={<Timing />} />
            <Route path="/profiles" element={<Profiles />} />
            <Route path="/system" element={<System />} />
          </Route>
        </Routes>
      </BrowserRouter>
    </ThemeProvider>
  );
}
```

Create minimal placeholder pages in `src/pages/` — each page is a simple component with a `<Typography>` heading. These will be fully implemented in Tasks 8-12.

**Step 4: Verify it renders**

```bash
cd src/SkyWatch.Web/ClientApp && npm run dev
```

Open http://localhost:5173 — should see dark-themed layout with sidebar navigation.

**Step 5: Commit**

```bash
git add src/SkyWatch.Web/ClientApp/
git commit -m "feat: add MUI dark theme, sidebar layout, and page routing"
```

---

### Task 8: Create WebSocket hook and context provider

**Files:**
- Create: `src/SkyWatch.Web/ClientApp/src/services/wsClient.ts`
- Create: `src/SkyWatch.Web/ClientApp/src/hooks/useWebSocket.ts`
- Create: `src/SkyWatch.Web/ClientApp/src/context/SkyWatchContext.tsx`

**Step 1: Define TypeScript types**

```typescript
// src/SkyWatch.Web/ClientApp/src/services/wsClient.ts
export interface SkyMetrics {
  brightness: number;
  starCount: number;
  cloudiness: number;
  entropy: number;
  edgeDensity: number;
  obstructionScore: number;
}

export interface CaptureStatus {
  isConnected: boolean;
  sourceType: string;
  sourceDescription: string;
  framesCaptured: number;
  errorMessage: string | null;
}

export interface StatusMessage {
  type: 'status';
  data: {
    safetyState: string;
    isSafe: boolean;
    reasons: string;
    confidence: number;
    explanation: string;
    metrics: SkyMetrics;
    capture: CaptureStatus;
  };
}

export type WsMessage = StatusMessage;
```

**Step 2: Create useWebSocket hook**

```typescript
// src/SkyWatch.Web/ClientApp/src/hooks/useWebSocket.ts
import { useEffect, useRef, useCallback, useState } from 'react';
import { StatusMessage } from '../services/wsClient';

const RECONNECT_INTERVAL = 3000;

export function useWebSocket(url: string) {
  const wsRef = useRef<WebSocket | null>(null);
  const [status, setStatus] = useState<StatusMessage['data'] | null>(null);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [connected, setConnected] = useState(false);
  const reconnectTimer = useRef<ReturnType<typeof setTimeout>>();

  const connect = useCallback(() => {
    const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
    const wsUrl = url.startsWith('ws') ? url : `${protocol}//${window.location.host}${url}`;
    const ws = new WebSocket(wsUrl);
    wsRef.current = ws;

    ws.onopen = () => setConnected(true);

    ws.onmessage = (event) => {
      if (typeof event.data === 'string') {
        try {
          const msg = JSON.parse(event.data);
          if (msg.type === 'status') {
            setStatus(msg.data);
          }
        } catch { /* ignore parse errors */ }
      } else if (event.data instanceof Blob) {
        const url = URL.createObjectURL(event.data);
        setPreviewUrl((prev) => {
          if (prev) URL.revokeObjectURL(prev);
          return url;
        });
      }
    };

    ws.onclose = () => {
      setConnected(false);
      reconnectTimer.current = setTimeout(connect, RECONNECT_INTERVAL);
    };

    ws.onerror = () => ws.close();
  }, [url]);

  useEffect(() => {
    connect();
    return () => {
      clearTimeout(reconnectTimer.current);
      wsRef.current?.close();
    };
  }, [connect]);

  return { status, previewUrl, connected };
}
```

**Step 3: Create context provider**

```typescript
// src/SkyWatch.Web/ClientApp/src/context/SkyWatchContext.tsx
import { createContext, useContext, ReactNode } from 'react';
import { useWebSocket } from '../hooks/useWebSocket';
import { StatusMessage } from '../services/wsClient';

interface SkyWatchContextValue {
  status: StatusMessage['data'] | null;
  previewUrl: string | null;
  connected: boolean;
}

const SkyWatchContext = createContext<SkyWatchContextValue>({
  status: null,
  previewUrl: null,
  connected: false,
});

export function SkyWatchProvider({ children }: { children: ReactNode }) {
  const value = useWebSocket('/ws/live');
  return (
    <SkyWatchContext.Provider value={value}>
      {children}
    </SkyWatchContext.Provider>
  );
}

export function useSkyWatch() {
  return useContext(SkyWatchContext);
}
```

Wrap `<BrowserRouter>` in `App.tsx` with `<SkyWatchProvider>`.

**Step 4: Commit**

```bash
git add src/SkyWatch.Web/ClientApp/src/
git commit -m "feat: add WebSocket hook, types, and SkyWatch context provider"
```

---

### Task 9: Create API client service

**Files:**
- Create: `src/SkyWatch.Web/ClientApp/src/services/api.ts`

**Step 1: Implement the API client**

```typescript
// src/SkyWatch.Web/ClientApp/src/services/api.ts
import type { CaptureSettings, ThresholdSettings, TimingSettings, ProfileSettings } from './types';

const BASE = '/api';

async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    headers: { 'Content-Type': 'application/json' },
    ...options,
  });
  if (!res.ok) {
    const body = await res.json().catch(() => ({}));
    throw new Error(body.error || body.errors?.join(', ') || `Request failed: ${res.status}`);
  }
  return res.json();
}

export const api = {
  // Dashboard
  getStatus: () => request('/dashboard/status'),

  // Capture
  getCaptureSettings: () => request<CaptureSettings>('/config/capture'),
  updateCaptureSettings: (settings: CaptureSettings) =>
    request<CaptureSettings>('/config/capture', { method: 'POST', body: JSON.stringify(settings) }),

  // Thresholds
  getThresholds: () => request<ThresholdSettings>('/config/thresholds'),
  updateThresholds: (settings: ThresholdSettings) =>
    request<ThresholdSettings>('/config/thresholds', { method: 'PUT', body: JSON.stringify(settings) }),

  // Timing
  getTiming: () => request<TimingSettings>('/config/timing'),
  updateTiming: (settings: TimingSettings) =>
    request<TimingSettings>('/config/timing', { method: 'PUT', body: JSON.stringify(settings) }),

  // Override
  getOverride: () => request<{ mode: string }>('/config/override'),
  updateOverride: (mode: string) =>
    request<{ mode: string }>('/config/override', { method: 'PUT', body: JSON.stringify({ mode }) }),

  // Profiles
  getProfile: () => request<ProfileSettings>('/config/profile'),
  updateProfile: (profile: ProfileSettings) =>
    request<ProfileSettings>('/config/profile', { method: 'PUT', body: JSON.stringify(profile) }),
  listProfiles: () => request<string[]>('/config/profiles'),
  activateProfile: (name: string) =>
    request<ProfileSettings>(`/config/profiles/${encodeURIComponent(name)}/activate`, { method: 'POST' }),

  // Preview (fallback for non-WebSocket)
  getPreviewFrameUrl: () => `/api/preview/frame?t=${Date.now()}`,
};
```

Create a shared types file:

```typescript
// src/SkyWatch.Web/ClientApp/src/services/types.ts
export interface CaptureSettings {
  sourceType: 'UsbCamera' | 'Rtsp' | 'Replay';
  sourceIdentifier: string;
  deviceIndex: number;
  frameIntervalSeconds: number;
  targetWidth: number;
  targetHeight: number;
  reconnectIntervalSeconds: number;
  staleFrameTimeoutSeconds: number;
  connectionTimeoutSeconds: number;
  flipHorizontal: boolean;
  flipVertical: boolean;
  rotationDegrees: number;
}

export interface ThresholdSettings {
  maxAverageBrightness: number;
  minStarCount: number;
  maxCloudinessScore: number;
  minEntropy: number;
  minEdgeDensity: number;
  obstructionThreshold: number;
  starDetectionThreshold: number;
  confidenceThreshold: number;
}

export interface TimingSettings {
  warmupSeconds: number;
  unsafeDebounceSeconds: number;
  safeRecoveryDebounceSeconds: number;
  minimumUnsafeHoldSeconds: number;
  failSafeOnCameraLoss: boolean;
  failSafeOnStaleFrame: boolean;
}

export interface ProfileSettings {
  name: string;
  description: string;
  capture: CaptureSettings;
  thresholds: ThresholdSettings;
  timing: TimingSettings;
  masks: unknown[];
  manualOverride: string;
}
```

**Step 2: Commit**

```bash
git add src/SkyWatch.Web/ClientApp/src/services/
git commit -m "feat: add REST API client and shared TypeScript types"
```

---

## Phase 3: React Pages

### Task 10: Dashboard page

The main monitoring page with live status, metrics, and camera preview.

**Files:**
- Create: `src/SkyWatch.Web/ClientApp/src/pages/Dashboard.tsx`
- Create: `src/SkyWatch.Web/ClientApp/src/components/StatusBanner.tsx`
- Create: `src/SkyWatch.Web/ClientApp/src/components/MetricsPanel.tsx`
- Create: `src/SkyWatch.Web/ClientApp/src/components/LivePreview.tsx`

**Implementation:**
- `StatusBanner`: Shows safety state with MUI `Alert` component — green for Safe, red for Unsafe, amber for Unknown/Initializing.
- `MetricsPanel`: MUI `Card` with a list of metric rows. Each row shows label, current value, and a small `LinearProgress` bar indicating how close to threshold.
- `LivePreview`: Displays the preview image from WebSocket binary messages (`previewUrl` from context). Falls back to polling `/api/preview/frame` if WebSocket not connected.
- `Dashboard`: Combines all three in a responsive MUI `Grid` layout.

All data comes from `useSkyWatch()` context — no direct API calls on this page.

**Commit:**
```bash
git add src/SkyWatch.Web/ClientApp/src/pages/Dashboard.tsx src/SkyWatch.Web/ClientApp/src/components/
git commit -m "feat: implement Dashboard page with live status, metrics, and preview"
```

---

### Task 11: Sources page

Capture source selection and configuration with live switching.

**Files:**
- Create: `src/SkyWatch.Web/ClientApp/src/pages/Sources.tsx`

**Implementation:**
- MUI `Select` dropdown for source type (USB Camera, RTSP, Replay)
- Conditional fields based on source type:
  - USB: `TextField` for device index, resolution width/height
  - RTSP: `TextField` for URL, connection timeout
  - Replay: `TextField` for directory path
- Common fields: frame interval, flip horizontal/vertical toggles (`Switch`), rotation
- "Connect" button calls `api.updateCaptureSettings()` — live switch
- Current connection status from `useSkyWatch()` context shown as a chip (Connected/Disconnected/Error)
- Error messages displayed inline via MUI `Alert`
- `Snackbar` for success/error feedback

**Commit:**
```bash
git add src/SkyWatch.Web/ClientApp/src/pages/Sources.tsx
git commit -m "feat: implement Sources page with live source switching"
```

---

### Task 12: Thresholds page

Edit all threshold values with sliders and numeric inputs.

**Files:**
- Create: `src/SkyWatch.Web/ClientApp/src/pages/Thresholds.tsx`

**Implementation:**
- Load current thresholds via `api.getThresholds()` on mount
- For each threshold: MUI `Slider` + `TextField` (numeric) side by side
- Current live metric value from `useSkyWatch()` shown as a marker on each slider so the user can see where the current reading falls relative to the threshold
- "Save" button calls `api.updateThresholds()`
- `Snackbar` for success/error
- Threshold fields: maxAverageBrightness, minStarCount, maxCloudinessScore, minEntropy, minEdgeDensity, obstructionThreshold, starDetectionThreshold, confidenceThreshold

**Commit:**
```bash
git add src/SkyWatch.Web/ClientApp/src/pages/Thresholds.tsx
git commit -m "feat: implement Thresholds page with sliders and live metric indicators"
```

---

### Task 13: Timing page

Edit hysteresis and timing settings.

**Files:**
- Create: `src/SkyWatch.Web/ClientApp/src/pages/Timing.tsx`

**Implementation:**
- Load via `api.getTiming()` on mount
- Fields with `TextField` (number type):
  - Warmup seconds
  - Unsafe debounce seconds
  - Safe recovery debounce seconds
  - Minimum unsafe hold seconds
- Toggle switches (`Switch`):
  - Fail safe on camera loss
  - Fail safe on stale frame
- "Save" button calls `api.updateTiming()`
- `Snackbar` for feedback

**Commit:**
```bash
git add src/SkyWatch.Web/ClientApp/src/pages/Timing.tsx
git commit -m "feat: implement Timing page for hysteresis settings"
```

---

### Task 14: Profiles page

Profile management — list, create, delete, switch, export/import.

**Files:**
- Create: `src/SkyWatch.Web/ClientApp/src/pages/Profiles.tsx`

**Implementation:**
- Load profile list via `api.listProfiles()` on mount
- MUI `List` showing all profiles, current active profile highlighted
- "Activate" button per profile — calls `api.activateProfile(name)`
- "Create New" button — opens MUI `Dialog` with name + description fields, creates by calling `api.updateProfile()` with new name
- "Export" button — downloads current profile as JSON file (via Blob + download link)
- "Import" button — file input that reads JSON, validates structure, calls `api.updateProfile()`
- `Snackbar` for feedback

**Commit:**
```bash
git add src/SkyWatch.Web/ClientApp/src/pages/Profiles.tsx
git commit -m "feat: implement Profiles page with create, activate, import/export"
```

---

### Task 15: System page

Manual override, Alpaca info, app status.

**Files:**
- Create: `src/SkyWatch.Web/ClientApp/src/pages/System.tsx`

**Implementation:**
- **Manual Override**: MUI `ToggleButtonGroup` with three options (Automatic, Force Safe, Force Unsafe). Calls `api.updateOverride()` on change.
- **System Info** card: version, uptime, connection count — from `useSkyWatch()` status data
- **Capture Status** card: source type, frames captured, error message — from context
- **Alpaca Info** card: display port, discovery port, server name (read-only from `/api/dashboard/status`)

**Commit:**
```bash
git add src/SkyWatch.Web/ClientApp/src/pages/System.tsx
git commit -m "feat: implement System page with manual override and status info"
```

---

## Phase 4: Production Build & Cleanup

### Task 16: Configure production build in MSBuild

**Files:**
- Modify: `src/SkyWatch.Web/SkyWatch.Web.csproj`
- Create: `src/SkyWatch.Web/ClientApp/.gitignore`

**Step 1: Add MSBuild targets**

Add to `SkyWatch.Web.csproj`:

```xml
<PropertyGroup>
    <SpaRoot>ClientApp</SpaRoot>
</PropertyGroup>

<Target Name="BuildClientApp" BeforeTargets="Build" Condition="'$(BuildingInsideVisualStudio)' != 'true' AND Exists('$(SpaRoot)/package.json')">
    <Exec Command="npm install" WorkingDirectory="$(SpaRoot)" />
    <Exec Command="npm run build" WorkingDirectory="$(SpaRoot)" />
</Target>

<Target Name="PublishClientApp" AfterTargets="ComputeFilesToPublish" Condition="Exists('$(SpaRoot)/package.json')">
    <Exec Command="npm install" WorkingDirectory="$(SpaRoot)" />
    <Exec Command="npm run build" WorkingDirectory="$(SpaRoot)" />
</Target>

<ItemGroup>
    <Content Remove="$(SpaRoot)\**" />
    <None Include="$(SpaRoot)\**" Exclude="$(SpaRoot)\node_modules\**;$(SpaRoot)\dist\**" />
    <Content Include="wwwroot\**" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

**Step 2: Add .gitignore for ClientApp**

```
node_modules/
dist/
```

**Step 3: Remove old vanilla HTML/CSS/JS files**

Delete the old files that are replaced by the React app:
- `src/SkyWatch.Web/wwwroot/index.html`
- `src/SkyWatch.Web/wwwroot/css/styles.css`
- `src/SkyWatch.Web/wwwroot/js/dashboard.js`
- `src/SkyWatch.Web/wwwroot/js/config.js`

**Step 4: Build and verify**

```bash
dotnet build SkyWatch.sln && dotnet test SkyWatch.sln -v minimal
```

**Step 5: Commit**

```bash
git add -A
git commit -m "build: configure MSBuild to build React app, remove old vanilla UI"
```

---

### Task 17: Update appsettings.json default port

Change default port from 5000 to 5050 to avoid macOS AirPlay conflict.

**Files:**
- Modify: `src/SkyWatch.Host/appsettings.json`

**Step 1: Change the port**

Change `"Urls": "http://0.0.0.0:5000"` to `"Urls": "http://0.0.0.0:5050"`

**Step 2: Commit**

```bash
git add src/SkyWatch.Host/appsettings.json
git commit -m "fix: change default port to 5050 to avoid macOS AirPlay conflict"
```

---

### Task 18: Update documentation

**Files:**
- Modify: `README.md`
- Modify: `ARCHITECTURE.md`
- Modify: `CLAUDE.md`

**Step 1: Update docs**

- README: Update "Web UI" section to describe React + MUI dashboard, list available pages, note ports (5050 for API, 5173 for dev)
- ARCHITECTURE.md: Update Web layer description, add WebSocket section, add FrameSourceManager to pipeline diagram
- CLAUDE.md: Update Build & Run Commands section with `npm` commands for ClientApp, note API-first architecture for N.I.N.A. compatibility

**Step 2: Commit**

```bash
git add README.md ARCHITECTURE.md CLAUDE.md
git commit -m "docs: update documentation for React web UI and WebSocket architecture"
```

---

### Task 19: End-to-end verification

**Step 1: Build everything**

```bash
dotnet build SkyWatch.sln
dotnet test SkyWatch.sln -v minimal
dotnet format SkyWatch.sln --verify-no-changes
cd src/SkyWatch.Web/ClientApp && npm run build && cd ../../..
```

**Step 2: Run the app and verify**

```bash
dotnet run --project src/SkyWatch.Host
```

Open http://localhost:5050 — verify:
- Dashboard loads with dark theme and sidebar
- Status shows (Initializing or similar)
- All 6 pages navigate correctly
- Sources page can switch source types
- Thresholds page loads and saves
- Timing page loads and saves
- Profiles page lists profiles
- System page shows override toggle

**Step 3: Final commit if any fixes needed**

---

## Task Summary

| Task | Description | Phase |
|------|-------------|-------|
| 1 | FrameSourceManager (TDD) | Backend |
| 2 | DI wiring update | Backend |
| 3 | New REST endpoints (TDD) | Backend |
| 4 | WebSocket endpoint | Backend |
| 5 | Static file serving fix | Backend |
| 6 | React project scaffold | React Scaffold |
| 7 | MUI theme + layout | React Scaffold |
| 8 | WebSocket hook + context | React Scaffold |
| 9 | API client service | React Scaffold |
| 10 | Dashboard page | React Pages |
| 11 | Sources page | React Pages |
| 12 | Thresholds page | React Pages |
| 13 | Timing page | React Pages |
| 14 | Profiles page | React Pages |
| 15 | System page | React Pages |
| 16 | Production build config | Build & Cleanup |
| 17 | Default port change | Build & Cleanup |
| 18 | Documentation update | Build & Cleanup |
| 19 | End-to-end verification | Build & Cleanup |
