# ASCOM & Alpaca Camera Support — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add ASCOM Alpaca (cross-platform HTTP REST) and ASCOM COM (Windows-only) camera frame sources, extend FrameData for full bit-depth capture, and implement auto-exposure.

**Architecture:** Two new `IFrameSource` implementations (Alpaca via HttpClient, COM via conditional ASCOM.DriverAccess). FrameData extended with `BitsPerChannel` and `PixelFormat`. MatConverter and FramePreprocessor updated for 16-bit support. Auto-exposure feedback loop inside camera sources. UI extended with two new source types.

**Tech Stack:** C# .NET 8, HttpClient, System.Text.Json, OpenCvSharp, ASCOM.DriverAccess (Windows-only conditional), React + MUI + TypeScript

---

### Task 1: Extend FrameData with BitsPerChannel and PixelFormat

**Files:**
- Modify: `src/SkyWatch.Contracts/Models/FrameData.cs`
- Modify: `src/SkyWatch.Capture/Helpers/MatConverter.cs`
- Modify: `src/SkyWatch.Capture/Sources/UsbCameraFrameSource.cs`
- Modify: `src/SkyWatch.Capture/Sources/RtspFrameSource.cs`
- Modify: `src/SkyWatch.Capture/Sources/ReplayFrameSource.cs`
- Modify: `tests/SkyWatch.Tests/Capture/MatConverterTests.cs`
- Modify: All other test files that construct `FrameData` directly

**Step 1: Add new required properties to FrameData**

Add to `src/SkyWatch.Contracts/Models/FrameData.cs`:

```csharp
public required int BitsPerChannel { get; init; }
public required string PixelFormat { get; init; }
```

**Step 2: Update MatConverter.MatToFrameData to set new properties**

In `src/SkyWatch.Capture/Helpers/MatConverter.cs`, update `MatToFrameData`:

```csharp
public static FrameData MatToFrameData(Mat mat, FrameSourceType sourceType)
{
    ArgumentNullException.ThrowIfNull(mat);

    Cv2.ImEncode(".png", mat, out var imageBytes);

    int bitsPerChannel = mat.Depth() switch
    {
        MatType.CV_8U => 8,
        MatType.CV_16U => 16,
        _ => 8,
    };

    string pixelFormat = (mat.Channels(), bitsPerChannel) switch
    {
        (1, 8) => "Mono8",
        (1, 16) => "Mono16",
        (3, 8) => "BGR24",
        (3, 16) => "BGR48",
        (4, 8) => "BGRA32",
        _ => $"Unknown{mat.Channels()}ch{bitsPerChannel}bit",
    };

    return new FrameData
    {
        ImageBytes = imageBytes,
        Width = mat.Width,
        Height = mat.Height,
        Channels = mat.Channels(),
        BitsPerChannel = bitsPerChannel,
        PixelFormat = pixelFormat,
        CapturedAt = DateTimeOffset.UtcNow,
        SourceType = sourceType,
    };
}
```

**Step 3: Fix all existing FrameData constructors across the codebase**

Search all `.cs` files for `new FrameData` and add:
- `BitsPerChannel = 8`
- `PixelFormat = "BGR24"` (or `"Mono8"` for single-channel)

This includes:
- `tests/SkyWatch.Tests/Capture/MatConverterTests.cs` — the `FrameDataToMatConvertsCorrectly` test constructs FrameData manually
- Any other test files constructing FrameData (search with `grep -rn "new FrameData" tests/`)
- Any source files constructing FrameData outside MatConverter (search with `grep -rn "new FrameData" src/`)

**Step 4: Run tests to verify nothing broke**

Run: `dotnet test SkyWatch.sln`
Expected: All 119 tests pass

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: extend FrameData with BitsPerChannel and PixelFormat"
```

---

### Task 2: Add AlpacaCamera and AscomCamera to FrameSourceType enum

**Files:**
- Modify: `src/SkyWatch.Contracts/Enums/FrameSourceType.cs`

**Step 1: Add two new enum values**

```csharp
public enum FrameSourceType
{
    UsbCamera,
    Rtsp,
    Replay,
    AlpacaCamera,
    AscomCamera,
}
```

**Step 2: Run build to verify**

Run: `dotnet build SkyWatch.sln`
Expected: Build succeeds (switch expressions in FrameSourceFactory will warn about unhandled cases — this is expected and will be fixed in Task 7)

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: add AlpacaCamera and AscomCamera to FrameSourceType enum"
```

---

### Task 3: Add Alpaca/ASCOM/exposure fields to CaptureSettings

**Files:**
- Modify: `src/SkyWatch.Core/Configuration/CaptureSettings.cs`
- Modify: `src/SkyWatch.Core/Validation/SettingsValidator.cs`
- Test: `tests/SkyWatch.Tests/Core/SettingsValidatorTests.cs`

**Step 1: Write failing tests for new validation rules**

Add to `tests/SkyWatch.Tests/Core/SettingsValidatorTests.cs`:

```csharp
[Fact]
public void AlpacaCameraWithEmptyHostReturnsError()
{
    var settings = new ProfileSettings
    {
        Capture = new CaptureSettings
        {
            SourceType = FrameSourceType.AlpacaCamera,
            AlpacaHost = string.Empty,
        },
    };

    var errors = SettingsValidator.Validate(settings);

    Assert.Contains(errors, e => e.Contains("Alpaca host"));
}

[Fact]
public void AlpacaCameraWithInvalidPortReturnsError()
{
    var settings = new ProfileSettings
    {
        Capture = new CaptureSettings
        {
            SourceType = FrameSourceType.AlpacaCamera,
            AlpacaHost = "localhost",
            AlpacaPort = -1,
        },
    };

    var errors = SettingsValidator.Validate(settings);

    Assert.Contains(errors, e => e.Contains("Alpaca port"));
}

[Fact]
public void AscomCameraWithEmptyProgIdReturnsError()
{
    var settings = new ProfileSettings
    {
        Capture = new CaptureSettings
        {
            SourceType = FrameSourceType.AscomCamera,
            AscomProgId = string.Empty,
        },
    };

    var errors = SettingsValidator.Validate(settings);

    Assert.Contains(errors, e => e.Contains("ASCOM ProgID"));
}

[Fact]
public void ExposureDurationBelowZeroReturnsError()
{
    var settings = new ProfileSettings
    {
        Capture = new CaptureSettings
        {
            SourceType = FrameSourceType.AlpacaCamera,
            AlpacaHost = "localhost",
            ExposureDuration = -1,
        },
    };

    var errors = SettingsValidator.Validate(settings);

    Assert.Contains(errors, e => e.Contains("Exposure duration"));
}

[Fact]
public void AutoExposureMinGreaterThanMaxReturnsError()
{
    var settings = new ProfileSettings
    {
        Capture = new CaptureSettings
        {
            SourceType = FrameSourceType.AlpacaCamera,
            AlpacaHost = "localhost",
            AutoExposure = true,
            ExposureMin = 10.0,
            ExposureMax = 5.0,
        },
    };

    var errors = SettingsValidator.Validate(settings);

    Assert.Contains(errors, e => e.Contains("Exposure min"));
}

[Fact]
public void AutoExposureTargetOutOfRangeReturnsError()
{
    var settings = new ProfileSettings
    {
        Capture = new CaptureSettings
        {
            SourceType = FrameSourceType.AlpacaCamera,
            AlpacaHost = "localhost",
            AutoExposure = true,
            AutoExposureTarget = 300,
        },
    };

    var errors = SettingsValidator.Validate(settings);

    Assert.Contains(errors, e => e.Contains("Auto-exposure target"));
}

[Fact]
public void ValidAlpacaSettingsReturnNoErrors()
{
    var settings = new ProfileSettings
    {
        Capture = new CaptureSettings
        {
            SourceType = FrameSourceType.AlpacaCamera,
            AlpacaHost = "192.168.1.100",
            AlpacaPort = 11111,
            AlpacaDeviceNumber = 0,
            ExposureDuration = 2.0,
        },
    };

    var errors = SettingsValidator.Validate(settings);

    Assert.Empty(errors);
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/SkyWatch.Tests --filter "FullyQualifiedName~SettingsValidatorTests" -v q`
Expected: FAIL — new properties don't exist yet

**Step 3: Add new properties to CaptureSettings**

In `src/SkyWatch.Core/Configuration/CaptureSettings.cs`, add:

```csharp
// Alpaca connection
public string AlpacaHost { get; set; } = string.Empty;
public int AlpacaPort { get; set; } = 11111;
public int AlpacaDeviceNumber { get; set; }

// ASCOM COM
public string AscomProgId { get; set; } = string.Empty;

// Exposure (shared by Alpaca + ASCOM)
public double ExposureDuration { get; set; } = 2.0;
public bool AutoExposure { get; set; }
public double AutoExposureTarget { get; set; } = 128.0;
public double ExposureMin { get; set; } = 0.1;
public double ExposureMax { get; set; } = 30.0;
```

**Step 4: Add validation rules to SettingsValidator**

In `src/SkyWatch.Core/Validation/SettingsValidator.cs`, add after existing capture validation (before threshold validation):

```csharp
// Alpaca-specific validation
if (settings.Capture.SourceType == FrameSourceType.AlpacaCamera)
{
    if (string.IsNullOrWhiteSpace(settings.Capture.AlpacaHost))
        errors.Add("Alpaca host is required for Alpaca camera source.");

    if (settings.Capture.AlpacaPort is < 1 or > 65535)
        errors.Add("Alpaca port must be between 1 and 65535.");

    if (settings.Capture.AlpacaDeviceNumber < 0)
        errors.Add("Alpaca device number cannot be negative.");
}

// ASCOM COM-specific validation
if (settings.Capture.SourceType == FrameSourceType.AscomCamera)
{
    if (string.IsNullOrWhiteSpace(settings.Capture.AscomProgId))
        errors.Add("ASCOM ProgID is required for ASCOM camera source.");
}

// Exposure validation (for camera sources)
if (settings.Capture.SourceType is FrameSourceType.AlpacaCamera or FrameSourceType.AscomCamera)
{
    if (settings.Capture.ExposureDuration <= 0)
        errors.Add("Exposure duration must be greater than 0.");

    if (settings.Capture.AutoExposure)
    {
        if (settings.Capture.AutoExposureTarget is < 0 or > 255)
            errors.Add("Auto-exposure target must be between 0 and 255.");

        if (settings.Capture.ExposureMin >= settings.Capture.ExposureMax)
            errors.Add("Exposure min must be less than exposure max.");

        if (settings.Capture.ExposureMin < 0)
            errors.Add("Exposure min cannot be negative.");
    }
}
```

Add `using SkyWatch.Contracts.Enums;` to the top of `SettingsValidator.cs`.

**Step 5: Run tests to verify they pass**

Run: `dotnet test tests/SkyWatch.Tests --filter "FullyQualifiedName~SettingsValidatorTests" -v q`
Expected: All pass

**Step 6: Run full test suite**

Run: `dotnet test SkyWatch.sln`
Expected: All tests pass

**Step 7: Commit**

```bash
git add -A
git commit -m "feat: add Alpaca/ASCOM/exposure fields to CaptureSettings with validation"
```

---

### Task 4: Add auto-exposure state to CaptureStatus

**Files:**
- Modify: `src/SkyWatch.Contracts/Models/CaptureStatus.cs`

**Step 1: Add auto-exposure fields**

```csharp
public double? CurrentExposureDuration { get; init; }
public bool AutoExposureEnabled { get; init; }
public double? AutoExposureTarget { get; init; }
```

**Step 2: Run build**

Run: `dotnet build SkyWatch.sln`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: add auto-exposure state to CaptureStatus"
```

---

### Task 5: Update MatConverter for 16-bit Mat support

**Files:**
- Modify: `src/SkyWatch.Capture/Helpers/MatConverter.cs`
- Modify: `tests/SkyWatch.Tests/Capture/MatConverterTests.cs`

**Step 1: Write failing tests for 16-bit support**

Add to `tests/SkyWatch.Tests/Capture/MatConverterTests.cs`:

```csharp
[Fact]
public void MatToFrameDataHandles16BitMono()
{
    using var mat = new Mat(40, 60, MatType.CV_16UC1, new Scalar(30000));

    var frameData = MatConverter.MatToFrameData(mat, FrameSourceType.AlpacaCamera);

    Assert.Equal(60, frameData.Width);
    Assert.Equal(40, frameData.Height);
    Assert.Equal(1, frameData.Channels);
    Assert.Equal(16, frameData.BitsPerChannel);
    Assert.Equal("Mono16", frameData.PixelFormat);
}

[Fact]
public void MatToFrameDataHandles8BitMono()
{
    using var mat = new Mat(40, 60, MatType.CV_8UC1, new Scalar(128));

    var frameData = MatConverter.MatToFrameData(mat, FrameSourceType.AlpacaCamera);

    Assert.Equal(1, frameData.Channels);
    Assert.Equal(8, frameData.BitsPerChannel);
    Assert.Equal("Mono8", frameData.PixelFormat);
}

[Fact]
public void MatToFrameDataHandles8BitBGR()
{
    using var mat = new Mat(40, 60, MatType.CV_8UC3, new Scalar(100, 150, 200));

    var frameData = MatConverter.MatToFrameData(mat, FrameSourceType.UsbCamera);

    Assert.Equal(3, frameData.Channels);
    Assert.Equal(8, frameData.BitsPerChannel);
    Assert.Equal("BGR24", frameData.PixelFormat);
}

[Fact]
public void FrameDataToMatHandles16BitFlag()
{
    using var original = new Mat(30, 40, MatType.CV_16UC1, new Scalar(10000));
    Cv2.ImEncode(".png", original, out var pngBytes);

    var frameData = new FrameData
    {
        ImageBytes = pngBytes,
        Width = 40,
        Height = 30,
        Channels = 1,
        BitsPerChannel = 16,
        PixelFormat = "Mono16",
        CapturedAt = DateTimeOffset.UtcNow,
        SourceType = FrameSourceType.AlpacaCamera,
    };

    using var decoded = MatConverter.FrameDataToMat(frameData);

    Assert.Equal(40, decoded.Width);
    Assert.Equal(30, decoded.Height);
    Assert.Equal(MatType.CV_16UC1, decoded.Type());
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/SkyWatch.Tests --filter "FullyQualifiedName~MatConverterTests" -v q`
Expected: FAIL

**Step 3: Update MatConverter.FrameDataToMat for bit-depth awareness**

Update `FrameDataToMat` in `src/SkyWatch.Capture/Helpers/MatConverter.cs`:

```csharp
public static Mat FrameDataToMat(FrameData frame)
{
    ArgumentNullException.ThrowIfNull(frame);

    var mode = frame.BitsPerChannel > 8
        ? ImreadModes.AnyDepth | ImreadModes.AnyColor
        : ImreadModes.Color;

    return Cv2.ImDecode(frame.ImageBytes, mode);
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/SkyWatch.Tests --filter "FullyQualifiedName~MatConverterTests" -v q`
Expected: All pass

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: update MatConverter for 16-bit Mat support"
```

---

### Task 6: Update FramePreprocessor for 16-bit images

**Files:**
- Modify: `src/SkyWatch.Vision/Preprocessing/FramePreprocessor.cs`
- Test: `tests/SkyWatch.Tests/Vision/FrameAnalyzerPipelineTests.cs` (existing tests should still pass)

**Step 1: Update Preprocess to handle 16-bit input**

The preprocessor converts to grayscale for analysis. For 16-bit input, it should:
1. Decode at native depth
2. Convert to grayscale if color
3. Scale 16-bit to 8-bit with auto-stretch for analysis

Update `src/SkyWatch.Vision/Preprocessing/FramePreprocessor.cs`:

```csharp
public static Mat Preprocess(FrameData frame, IReadOnlyList<MaskDefinition> masks)
{
    var readMode = frame.BitsPerChannel > 8
        ? ImreadModes.AnyDepth | ImreadModes.AnyColor
        : ImreadModes.Color;

    using var decoded = Cv2.ImDecode(frame.ImageBytes, readMode);
    if (decoded.Empty())
    {
        throw new InvalidOperationException("Failed to decode image from frame bytes.");
    }

    Mat gray;
    if (decoded.Channels() > 1)
    {
        gray = new Mat();
        Cv2.CvtColor(decoded, gray, ColorConversionCodes.BGR2GRAY);
    }
    else
    {
        gray = decoded.Clone();
    }

    // Scale 16-bit to 8-bit with auto-stretch for analysis
    if (gray.Depth() != MatType.CV_8U)
    {
        using var highDepth = gray;
        gray = new Mat();
        double minVal, maxVal;
        Cv2.MinMaxLoc(highDepth, out minVal, out maxVal);
        double scale = maxVal > minVal ? 255.0 / (maxVal - minVal) : 1.0;
        double offset = -minVal * scale;
        highDepth.ConvertTo(gray, MatType.CV_8UC1, scale, offset);
    }

    foreach (var mask in masks)
    {
        if (!mask.IsExclusion)
            continue;

        switch (mask.Shape)
        {
            case MaskShape.Rectangle:
                Cv2.Rectangle(
                    gray,
                    new Rect(mask.X, mask.Y, mask.Width, mask.Height),
                    Scalar.All(0),
                    -1);
                break;

            case MaskShape.Ellipse:
                Cv2.Ellipse(
                    gray,
                    new Point(mask.X + (mask.Width / 2), mask.Y + (mask.Height / 2)),
                    new Size(mask.Width / 2, mask.Height / 2),
                    0,
                    0,
                    360,
                    Scalar.All(0),
                    -1);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(masks), mask.Shape, "Unsupported mask shape.");
        }
    }

    return gray;
}
```

**Step 2: Run existing tests to verify nothing broke**

Run: `dotnet test tests/SkyWatch.Tests --filter "FullyQualifiedName~Vision" -v q`
Expected: All pass

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: update FramePreprocessor for 16-bit image support"
```

---

### Task 7: Implement AlpacaCameraFrameSource

**Files:**
- Create: `src/SkyWatch.Capture/Sources/AlpacaCameraFrameSource.cs`
- Create: `tests/SkyWatch.Tests/Capture/AlpacaCameraFrameSourceTests.cs`
- Modify: `src/SkyWatch.Capture/FrameSourceFactory.cs`

**Step 1: Write the failing tests**

Create `tests/SkyWatch.Tests/Capture/AlpacaCameraFrameSourceTests.cs`:

```csharp
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using SkyWatch.Capture.Sources;
using SkyWatch.Contracts.Enums;
using SkyWatch.Core.Configuration;
using System.Net;
using System.Text.Json;

namespace SkyWatch.Tests.Capture;

public sealed class AlpacaCameraFrameSourceTests : IAsyncDisposable
{
    private readonly Mock<ILoggerFactory> loggerFactoryMock = new();
    private readonly Mock<HttpMessageHandler> httpHandlerMock = new();
    private readonly CaptureSettings settings;

    public AlpacaCameraFrameSourceTests()
    {
        loggerFactoryMock
            .Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);

        settings = new CaptureSettings
        {
            SourceType = FrameSourceType.AlpacaCamera,
            AlpacaHost = "localhost",
            AlpacaPort = 11111,
            AlpacaDeviceNumber = 0,
            ExposureDuration = 1.0,
        };
    }

    [Fact]
    public void GetStatusReturnsAlpacaCameraType()
    {
        using var httpClient = new HttpClient(httpHandlerMock.Object);
        var source = new AlpacaCameraFrameSource(settings, httpClient,
            loggerFactoryMock.Object.CreateLogger<AlpacaCameraFrameSource>());

        var status = source.GetStatus();

        Assert.Equal(FrameSourceType.AlpacaCamera, status.SourceType);
        Assert.Contains("Alpaca", status.SourceDescription);
    }

    [Fact]
    public void GetStatusReportsNotConnectedBeforeStart()
    {
        using var httpClient = new HttpClient(httpHandlerMock.Object);
        var source = new AlpacaCameraFrameSource(settings, httpClient,
            loggerFactoryMock.Object.CreateLogger<AlpacaCameraFrameSource>());

        var status = source.GetStatus();

        Assert.False(status.IsConnected);
    }

    [Fact]
    public async Task GetLatestFrameReturnsNullBeforeStart()
    {
        using var httpClient = new HttpClient(httpHandlerMock.Object);
        var source = new AlpacaCameraFrameSource(settings, httpClient,
            loggerFactoryMock.Object.CreateLogger<AlpacaCameraFrameSource>());

        var frame = await source.GetLatestFrameAsync(CancellationToken.None);

        Assert.Null(frame);
    }

    [Fact]
    public void AutoExposureStatusReportedInGetStatus()
    {
        var autoSettings = new CaptureSettings
        {
            SourceType = FrameSourceType.AlpacaCamera,
            AlpacaHost = "localhost",
            AutoExposure = true,
            AutoExposureTarget = 128.0,
            ExposureDuration = 2.0,
        };

        using var httpClient = new HttpClient(httpHandlerMock.Object);
        var source = new AlpacaCameraFrameSource(autoSettings, httpClient,
            loggerFactoryMock.Object.CreateLogger<AlpacaCameraFrameSource>());

        var status = source.GetStatus();

        Assert.True(status.AutoExposureEnabled);
        Assert.Equal(128.0, status.AutoExposureTarget);
    }

    public async ValueTask DisposeAsync()
    {
        // cleanup if needed
        await Task.CompletedTask;
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/SkyWatch.Tests --filter "FullyQualifiedName~AlpacaCameraFrameSourceTests" -v q`
Expected: FAIL — class doesn't exist

**Step 3: Implement AlpacaCameraFrameSource**

Create `src/SkyWatch.Capture/Sources/AlpacaCameraFrameSource.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using SkyWatch.Contracts.Enums;
using SkyWatch.Contracts.Interfaces;
using SkyWatch.Contracts.Models;
using SkyWatch.Core.Configuration;

namespace SkyWatch.Capture.Sources;

/// <summary>
/// A frame source that captures images from an ASCOM Alpaca camera via HTTP REST.
/// </summary>
public sealed partial class AlpacaCameraFrameSource : IFrameSource
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly CaptureSettings settings;
    private readonly HttpClient httpClient;
    private readonly ILogger<AlpacaCameraFrameSource> logger;
    private readonly string baseUrl;

    private bool isStarted;
    private bool isConnected;
    private int framesCaptured;
    private DateTimeOffset? lastFrameTime;
    private string? errorMessage;
    private double currentExposure;
    private int clientTransactionId;

    public AlpacaCameraFrameSource(
        CaptureSettings settings,
        HttpClient httpClient,
        ILogger<AlpacaCameraFrameSource> logger)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

        baseUrl = $"http://{settings.AlpacaHost}:{settings.AlpacaPort}/api/v1/camera/{settings.AlpacaDeviceNumber}";
        currentExposure = settings.ExposureDuration;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SetConnectedAsync(true, cancellationToken);
            isStarted = true;
            isConnected = true;
            errorMessage = null;
            LogStarted(baseUrl);
        }
        catch (Exception ex)
        {
            errorMessage = $"Failed to connect to Alpaca camera: {ex.Message}";
            LogConnectionFailed(baseUrl, ex);
            throw;
        }
    }

    public async Task<FrameData?> GetLatestFrameAsync(CancellationToken cancellationToken)
    {
        if (!isStarted)
            return null;

        try
        {
            // Start exposure
            await StartExposureAsync(currentExposure, true, cancellationToken);

            // Poll for completion
            while (!await IsImageReadyAsync(cancellationToken))
            {
                await Task.Delay(100, cancellationToken);
            }

            // Download image
            var frameData = await GetImageBytesAsync(cancellationToken);
            framesCaptured++;
            lastFrameTime = DateTimeOffset.UtcNow;
            errorMessage = null;

            // Auto-exposure adjustment
            if (settings.AutoExposure && frameData is not null)
            {
                AdjustExposure(frameData);
            }

            return frameData;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            errorMessage = $"Error capturing frame: {ex.Message}";
            LogCaptureError(baseUrl, ex);
            return null;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (isConnected)
        {
            try
            {
                await SetConnectedAsync(false, cancellationToken);
            }
            catch (Exception ex)
            {
                LogDisconnectError(baseUrl, ex);
            }
        }

        isStarted = false;
        isConnected = false;
        LogStopped(baseUrl);
    }

    public CaptureStatus GetStatus()
    {
        return new CaptureStatus
        {
            IsConnected = isStarted && isConnected,
            SourceType = FrameSourceType.AlpacaCamera,
            SourceDescription = $"Alpaca Camera: {settings.AlpacaHost}:{settings.AlpacaPort} (device {settings.AlpacaDeviceNumber})",
            LastFrameAt = lastFrameTime,
            FramesCaptured = framesCaptured,
            ErrorMessage = errorMessage,
            CurrentExposureDuration = currentExposure,
            AutoExposureEnabled = settings.AutoExposure,
            AutoExposureTarget = settings.AutoExposure ? settings.AutoExposureTarget : null,
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (isStarted)
        {
            await StopAsync(CancellationToken.None);
        }
    }

    private async Task SetConnectedAsync(bool connected, CancellationToken cancellationToken)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Connected"] = connected.ToString(),
            ["ClientID"] = "1",
            ["ClientTransactionID"] = (++clientTransactionId).ToString(),
        });

        var response = await httpClient.PutAsync($"{baseUrl}/connected", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<AlpacaResponse>(json, JsonOptions);
        if (result?.ErrorNumber is not null and not 0)
        {
            throw new InvalidOperationException($"Alpaca error {result.ErrorNumber}: {result.ErrorMessage}");
        }
    }

    private async Task StartExposureAsync(double duration, bool light, CancellationToken cancellationToken)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Duration"] = duration.ToString("F6"),
            ["Light"] = light.ToString(),
            ["ClientID"] = "1",
            ["ClientTransactionID"] = (++clientTransactionId).ToString(),
        });

        var response = await httpClient.PutAsync($"{baseUrl}/startexposure", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<AlpacaResponse>(json, JsonOptions);
        if (result?.ErrorNumber is not null and not 0)
        {
            throw new InvalidOperationException($"Alpaca error {result.ErrorNumber}: {result.ErrorMessage}");
        }
    }

    private async Task<bool> IsImageReadyAsync(CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(
            $"{baseUrl}/imageready?ClientID=1&ClientTransactionID={++clientTransactionId}",
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<AlpacaValueResponse<bool>>(json, JsonOptions);
        return result?.Value ?? false;
    }

    private async Task<FrameData?> GetImageBytesAsync(CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(
            $"{baseUrl}/imagebytes?ClientID=1&ClientTransactionID={++clientTransactionId}",
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (bytes.Length == 0)
            return null;

        // Alpaca ImageBytes format: metadata header + raw pixel data
        // The response includes a header with array dimensions and type info
        return ParseAlpacaImageBytes(bytes);
    }

    private FrameData? ParseAlpacaImageBytes(byte[] rawResponse)
    {
        // ASCOM Alpaca ImageBytes binary format:
        // Bytes 0-3: MetadataVersion (int32, always 1)
        // Bytes 4-7: ErrorNumber (int32, 0 = success)
        // Bytes 8-11: ClientTransactionID (int32)
        // Bytes 12-15: ServerTransactionID (int32)
        // Bytes 16-19: DataStart offset (int32)
        // Bytes 20-23: ImageElementType (int32): 2=Int16, 3=Int32, 6=UInt16, etc.
        // Bytes 24-27: TransmissionElementType (int32)
        // Bytes 28-31: Rank (int32, 2 for 2D mono, 3 for 3D color)
        // Bytes 32-35: Dimension1 (width)
        // Bytes 36-39: Dimension2 (height)
        // Bytes 40-43: Dimension3 (color planes, if rank=3)
        // DataStart onwards: raw pixel data

        if (rawResponse.Length < 44)
        {
            LogInvalidImageResponse(rawResponse.Length);
            return null;
        }

        int metadataVersion = BitConverter.ToInt32(rawResponse, 0);
        int errorNumber = BitConverter.ToInt32(rawResponse, 4);
        if (errorNumber != 0)
        {
            LogAlpacaImageError(errorNumber);
            return null;
        }

        int dataStart = BitConverter.ToInt32(rawResponse, 16);
        int imageElementType = BitConverter.ToInt32(rawResponse, 20);
        int transmissionElementType = BitConverter.ToInt32(rawResponse, 24);
        int rank = BitConverter.ToInt32(rawResponse, 28);
        int width = BitConverter.ToInt32(rawResponse, 32);
        int height = BitConverter.ToInt32(rawResponse, 36);
        int colorPlanes = rank >= 3 ? BitConverter.ToInt32(rawResponse, 40) : 1;

        int bitsPerChannel = transmissionElementType switch
        {
            1 => 8,   // Byte
            2 => 16,  // Int16
            6 => 16,  // UInt16
            3 => 32,  // Int32
            _ => 16,  // Default to 16-bit
        };

        int bytesPerPixel = bitsPerChannel / 8;
        int channels = colorPlanes;

        // Extract raw pixel data
        var pixelData = new byte[rawResponse.Length - dataStart];
        Array.Copy(rawResponse, dataStart, pixelData, 0, pixelData.Length);

        // Convert to OpenCV Mat for proper encoding
        var matType = (channels, bitsPerChannel) switch
        {
            (1, 8) => MatType.CV_8UC1,
            (1, 16) => MatType.CV_16UC1,
            (3, 8) => MatType.CV_8UC3,
            (3, 16) => MatType.CV_16UC3,
            _ => MatType.CV_16UC1,
        };

        string pixelFormat = (channels, bitsPerChannel) switch
        {
            (1, 8) => "Mono8",
            (1, 16) => "Mono16",
            (3, 8) => "BGR24",
            (3, 16) => "BGR48",
            _ => $"Unknown{channels}ch{bitsPerChannel}bit",
        };

        using var mat = new Mat(height, width, matType);
        System.Runtime.InteropServices.Marshal.Copy(pixelData, 0, mat.Data, Math.Min(pixelData.Length, (int)mat.Total() * mat.ElemSize()));

        Cv2.ImEncode(".png", mat, out var pngBytes);

        return new FrameData
        {
            ImageBytes = pngBytes,
            Width = width,
            Height = height,
            Channels = channels,
            BitsPerChannel = bitsPerChannel,
            PixelFormat = pixelFormat,
            CapturedAt = DateTimeOffset.UtcNow,
            SourceType = FrameSourceType.AlpacaCamera,
        };
    }

    private void AdjustExposure(FrameData frame)
    {
        // Quick brightness estimate from the captured frame
        using var mat = Cv2.ImDecode(frame.ImageBytes, ImreadModes.AnyDepth | ImreadModes.Grayscale);
        if (mat.Empty())
            return;

        // Scale to 0-255 range regardless of bit depth
        double actualBrightness;
        if (mat.Depth() == MatType.CV_16U)
        {
            actualBrightness = Cv2.Mean(mat).Val0 / 257.0; // 65535/255 ≈ 257
        }
        else
        {
            actualBrightness = Cv2.Mean(mat).Val0;
        }

        if (actualBrightness <= 0)
            return;

        double target = settings.AutoExposureTarget;
        double newExposure = currentExposure * (target / actualBrightness);

        // Damping: 50% towards new value to prevent oscillation
        double adjusted = currentExposure + (newExposure - currentExposure) * 0.5;

        // Clamp
        currentExposure = Math.Clamp(adjusted, settings.ExposureMin, settings.ExposureMax);

        LogAutoExposureAdjusted(actualBrightness, target, currentExposure);
    }

    // Alpaca JSON response DTOs
    private sealed class AlpacaResponse
    {
        public int? ErrorNumber { get; set; }
        public string? ErrorMessage { get; set; }
    }

    private sealed class AlpacaValueResponse<T>
    {
        public T? Value { get; set; }
        public int? ErrorNumber { get; set; }
        public string? ErrorMessage { get; set; }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Alpaca camera started: {BaseUrl}")]
    private partial void LogStarted(string baseUrl);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to connect to Alpaca camera: {BaseUrl}")]
    private partial void LogConnectionFailed(string baseUrl, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error capturing frame from Alpaca camera: {BaseUrl}")]
    private partial void LogCaptureError(string baseUrl, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error disconnecting Alpaca camera: {BaseUrl}")]
    private partial void LogDisconnectError(string baseUrl, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Alpaca camera stopped: {BaseUrl}")]
    private partial void LogStopped(string baseUrl);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid Alpaca image response: only {Length} bytes")]
    private partial void LogInvalidImageResponse(int length);

    [LoggerMessage(Level = LogLevel.Error, Message = "Alpaca image error: {ErrorNumber}")]
    private partial void LogAlpacaImageError(int errorNumber);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Auto-exposure adjusted: brightness={Brightness:F1}, target={Target:F1}, newExposure={Exposure:F3}s")]
    private partial void LogAutoExposureAdjusted(double brightness, double target, double exposure);
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/SkyWatch.Tests --filter "FullyQualifiedName~AlpacaCameraFrameSourceTests" -v q`
Expected: All pass

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: implement AlpacaCameraFrameSource with auto-exposure"
```

---

### Task 8: Implement AscomComCameraFrameSource (Windows-only, conditional)

**Files:**
- Create: `src/SkyWatch.Capture/Sources/AscomComCameraFrameSource.cs`
- Modify: `src/SkyWatch.Capture/SkyWatch.Capture.csproj`

**Step 1: Add conditional ASCOM.DriverAccess NuGet reference**

In `src/SkyWatch.Capture/SkyWatch.Capture.csproj`, add:

```xml
<ItemGroup Condition="$([MSBuild]::IsOSPlatform('Windows'))">
  <PackageReference Include="ASCOM.DriverAccess" Version="7.*" />
</ItemGroup>
```

**Step 2: Implement AscomComCameraFrameSource with platform guard**

Create `src/SkyWatch.Capture/Sources/AscomComCameraFrameSource.cs`:

```csharp
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using SkyWatch.Contracts.Enums;
using SkyWatch.Contracts.Interfaces;
using SkyWatch.Contracts.Models;
using SkyWatch.Core.Configuration;

namespace SkyWatch.Capture.Sources;

/// <summary>
/// A frame source that captures images from a native ASCOM COM camera driver.
/// Windows only — throws PlatformNotSupportedException on other platforms.
/// </summary>
public sealed partial class AscomComCameraFrameSource : IFrameSource
{
    private readonly CaptureSettings settings;
    private readonly ILogger<AscomComCameraFrameSource> logger;

    private bool isStarted;
    private bool isConnected;
    private int framesCaptured;
    private DateTimeOffset? lastFrameTime;
    private string? errorMessage;
    private double currentExposure;

#if WINDOWS
    private ASCOM.DriverAccess.Camera? camera;
#endif

    public AscomComCameraFrameSource(
        CaptureSettings settings,
        ILogger<AscomComCameraFrameSource> logger)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException(
                "ASCOM COM camera support is only available on Windows. Use Alpaca camera for cross-platform support.");
        }

        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        currentExposure = settings.ExposureDuration;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
#if WINDOWS
        try
        {
            camera = new ASCOM.DriverAccess.Camera(settings.AscomProgId);
            camera.Connected = true;
            isStarted = true;
            isConnected = true;
            errorMessage = null;
            LogStarted(settings.AscomProgId);
        }
        catch (Exception ex)
        {
            errorMessage = $"Failed to connect to ASCOM camera: {ex.Message}";
            LogConnectionFailed(settings.AscomProgId, ex);
            throw;
        }
#else
        throw new PlatformNotSupportedException("ASCOM COM is only available on Windows.");
#endif
        return Task.CompletedTask;
    }

    public Task<FrameData?> GetLatestFrameAsync(CancellationToken cancellationToken)
    {
#if WINDOWS
        if (!isStarted || camera is null)
            return Task.FromResult<FrameData?>(null);

        try
        {
            camera.StartExposure(currentExposure, true);

            while (!camera.ImageReady)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Thread.Sleep(100);
            }

            var imageArray = (int[,])camera.ImageArray;
            int width = imageArray.GetLength(0);
            int height = imageArray.GetLength(1);

            // Determine bit depth from max ADU
            int maxAdu = camera.MaxADU;
            int bitsPerChannel = maxAdu switch
            {
                <= 255 => 8,
                <= 4095 => 12,
                _ => 16,
            };

            // Create Mat at appropriate depth
            var matType = bitsPerChannel > 8 ? MatType.CV_16UC1 : MatType.CV_8UC1;
            using var mat = new Mat(height, width, matType);

            // Copy pixel data
            unsafe
            {
                if (bitsPerChannel > 8)
                {
                    var ptr = (ushort*)mat.Data;
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            ptr[y * width + x] = (ushort)Math.Clamp(imageArray[x, y], 0, ushort.MaxValue);
                        }
                    }
                }
                else
                {
                    var ptr = (byte*)mat.Data;
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            ptr[y * width + x] = (byte)Math.Clamp(imageArray[x, y], 0, 255);
                        }
                    }
                }
            }

            string pixelFormat = bitsPerChannel > 8 ? "Mono16" : "Mono8";
            Cv2.ImEncode(".png", mat, out var pngBytes);

            var frameData = new FrameData
            {
                ImageBytes = pngBytes,
                Width = width,
                Height = height,
                Channels = 1,
                BitsPerChannel = bitsPerChannel,
                PixelFormat = pixelFormat,
                CapturedAt = DateTimeOffset.UtcNow,
                SourceType = FrameSourceType.AscomCamera,
            };

            framesCaptured++;
            lastFrameTime = DateTimeOffset.UtcNow;
            errorMessage = null;

            if (settings.AutoExposure)
            {
                AdjustExposure(mat, bitsPerChannel);
            }

            return Task.FromResult<FrameData?>(frameData);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            errorMessage = $"Error capturing frame: {ex.Message}";
            LogCaptureError(settings.AscomProgId, ex);
            return Task.FromResult<FrameData?>(null);
        }
#else
        throw new PlatformNotSupportedException("ASCOM COM is only available on Windows.");
#endif
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
#if WINDOWS
        if (camera is not null)
        {
            try
            {
                camera.Connected = false;
                camera.Dispose();
            }
            catch (Exception ex)
            {
                LogDisconnectError(settings.AscomProgId, ex);
            }

            camera = null;
        }
#endif
        isStarted = false;
        isConnected = false;
        LogStopped(settings.AscomProgId);
        return Task.CompletedTask;
    }

    public CaptureStatus GetStatus()
    {
        return new CaptureStatus
        {
            IsConnected = isStarted && isConnected,
            SourceType = FrameSourceType.AscomCamera,
            SourceDescription = $"ASCOM Camera: {settings.AscomProgId}",
            LastFrameAt = lastFrameTime,
            FramesCaptured = framesCaptured,
            ErrorMessage = errorMessage,
            CurrentExposureDuration = currentExposure,
            AutoExposureEnabled = settings.AutoExposure,
            AutoExposureTarget = settings.AutoExposure ? settings.AutoExposureTarget : null,
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (isStarted)
        {
            await StopAsync(CancellationToken.None);
        }
    }

#if WINDOWS
    private void AdjustExposure(Mat mat, int bitsPerChannel)
    {
        double actualBrightness;
        if (bitsPerChannel > 8)
        {
            actualBrightness = Cv2.Mean(mat).Val0 / 257.0;
        }
        else
        {
            actualBrightness = Cv2.Mean(mat).Val0;
        }

        if (actualBrightness <= 0)
            return;

        double target = settings.AutoExposureTarget;
        double newExposure = currentExposure * (target / actualBrightness);
        double adjusted = currentExposure + (newExposure - currentExposure) * 0.5;
        currentExposure = Math.Clamp(adjusted, settings.ExposureMin, settings.ExposureMax);

        LogAutoExposureAdjusted(actualBrightness, target, currentExposure);
    }
#endif

    [LoggerMessage(Level = LogLevel.Information, Message = "ASCOM camera started: {ProgId}")]
    private partial void LogStarted(string progId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to connect to ASCOM camera: {ProgId}")]
    private partial void LogConnectionFailed(string progId, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error capturing frame from ASCOM camera: {ProgId}")]
    private partial void LogCaptureError(string progId, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error disconnecting ASCOM camera: {ProgId}")]
    private partial void LogDisconnectError(string progId, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "ASCOM camera stopped: {ProgId}")]
    private partial void LogStopped(string progId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ASCOM auto-exposure adjusted: brightness={Brightness:F1}, target={Target:F1}, newExposure={Exposure:F3}s")]
    private partial void LogAutoExposureAdjusted(double brightness, double target, double exposure);
}
```

**Step 3: Add `AllowUnsafeBlocks` to the csproj**

In `src/SkyWatch.Capture/SkyWatch.Capture.csproj`, add in the top `<PropertyGroup>`:

```xml
<AllowUnsafeBlocks>true</AllowUnsafeBlocks>
```

And add the `WINDOWS` define for Windows builds:

```xml
<PropertyGroup Condition="$([MSBuild]::IsOSPlatform('Windows'))">
  <DefineConstants>$(DefineConstants);WINDOWS</DefineConstants>
</PropertyGroup>
```

**Step 4: Run build to verify**

Run: `dotnet build SkyWatch.sln`
Expected: Build succeeds on macOS (ASCOM code inside `#if WINDOWS` blocks is excluded)

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: implement AscomComCameraFrameSource with conditional compilation"
```

---

### Task 9: Update FrameSourceFactory for new source types

**Files:**
- Modify: `src/SkyWatch.Capture/FrameSourceFactory.cs`
- Modify: `tests/SkyWatch.Tests/Capture/FrameSourceFactoryTests.cs`

**Step 1: Write failing tests**

Add to `tests/SkyWatch.Tests/Capture/FrameSourceFactoryTests.cs`:

```csharp
[Fact]
public void CreatesAlpacaCameraFrameSourceForAlpacaType()
{
    var settings = new CaptureSettings
    {
        SourceType = FrameSourceType.AlpacaCamera,
        AlpacaHost = "localhost",
        AlpacaPort = 11111,
    };

    var source = FrameSourceFactory.Create(settings, loggerFactoryMock.Object);

    Assert.IsType<AlpacaCameraFrameSource>(source);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/SkyWatch.Tests --filter "FullyQualifiedName~FrameSourceFactoryTests.CreatesAlpaca" -v q`
Expected: FAIL

**Step 3: Update FrameSourceFactory**

The factory needs an `HttpClient` or `IHttpClientFactory` for Alpaca. Update the factory to accept `IServiceProvider`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SkyWatch.Capture.Sources;
using SkyWatch.Contracts.Enums;
using SkyWatch.Contracts.Interfaces;
using SkyWatch.Core.Configuration;

namespace SkyWatch.Capture;

public static class FrameSourceFactory
{
    public static IFrameSource Create(CaptureSettings settings, ILoggerFactory loggerFactory)
    {
        return Create(settings, loggerFactory, httpClientFactory: null);
    }

    public static IFrameSource Create(CaptureSettings settings, ILoggerFactory loggerFactory, IHttpClientFactory? httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        return settings.SourceType switch
        {
            FrameSourceType.Replay => new ReplayFrameSource(settings, loggerFactory.CreateLogger<ReplayFrameSource>()),
            FrameSourceType.UsbCamera => new UsbCameraFrameSource(settings, loggerFactory.CreateLogger<UsbCameraFrameSource>()),
            FrameSourceType.Rtsp => new RtspFrameSource(settings, loggerFactory.CreateLogger<RtspFrameSource>()),
            FrameSourceType.AlpacaCamera => CreateAlpacaSource(settings, loggerFactory, httpClientFactory),
            FrameSourceType.AscomCamera => new AscomComCameraFrameSource(settings, loggerFactory.CreateLogger<AscomComCameraFrameSource>()),
            _ => throw new ArgumentOutOfRangeException(nameof(settings), settings.SourceType, $"Unsupported frame source type: {settings.SourceType}"),
        };
    }

    private static AlpacaCameraFrameSource CreateAlpacaSource(CaptureSettings settings, ILoggerFactory loggerFactory, IHttpClientFactory? httpClientFactory)
    {
        var httpClient = httpClientFactory?.CreateClient("AlpacaCamera") ?? new HttpClient();
        return new AlpacaCameraFrameSource(settings, httpClient, loggerFactory.CreateLogger<AlpacaCameraFrameSource>());
    }
}
```

**Step 4: Update existing tests if needed (the null overload preserves backward compatibility)**

Update the test to use the new overload or verify the existing tests still pass with the original signature.

**Step 5: Run all factory tests**

Run: `dotnet test tests/SkyWatch.Tests --filter "FullyQualifiedName~FrameSourceFactoryTests" -v q`
Expected: All pass

**Step 6: Update FrameSourceManager and DI registration**

Check if `FrameSourceManager` or DI (`ServiceRegistration`) calls `FrameSourceFactory.Create` and update to pass `IHttpClientFactory`. The `FrameSourceManager` in `src/SkyWatch.Capture/FrameSourceManager.cs` needs to accept and forward `IHttpClientFactory`.

Search for usages: `grep -rn "FrameSourceFactory.Create" src/`

Update all call sites to pass `IHttpClientFactory` (or null for non-Alpaca sources).

**Step 7: Run full test suite**

Run: `dotnet test SkyWatch.sln`
Expected: All tests pass

**Step 8: Commit**

```bash
git add -A
git commit -m "feat: update FrameSourceFactory for Alpaca and ASCOM camera types"
```

---

### Task 10: Update PreviewApiController for 16-bit frame handling

**Files:**
- Modify: `src/SkyWatch.Web/Controllers/PreviewApiController.cs`

**Step 1: Update GetFrame to auto-stretch 16-bit frames to 8-bit JPEG**

The preview endpoint must return 8-bit JPEG regardless of source bit depth:

```csharp
[HttpGet("frame")]
public async Task<IActionResult> GetFrame(CancellationToken cancellationToken)
{
    if (frameSource is null)
    {
        LogNoFrameSource();
        return NoContent();
    }

    var frame = await frameSource.GetLatestFrameAsync(cancellationToken);
    if (frame is null)
    {
        LogNoFrameAvailable();
        return NoContent();
    }

    LogFrameServed(frame.Width, frame.Height);

    byte[] jpegBytes;
    if (frame.BitsPerChannel > 8)
    {
        // Auto-stretch 16-bit to 8-bit for JPEG preview
        using var mat = OpenCvSharp.Cv2.ImDecode(frame.ImageBytes,
            OpenCvSharp.ImreadModes.AnyDepth | OpenCvSharp.ImreadModes.AnyColor);
        if (mat.Empty())
            return NoContent();

        using var stretched = new OpenCvSharp.Mat();
        double minVal, maxVal;
        OpenCvSharp.Cv2.MinMaxLoc(mat, out minVal, out maxVal);
        double scale = maxVal > minVal ? 255.0 / (maxVal - minVal) : 1.0;
        double offset = -minVal * scale;
        mat.ConvertTo(stretched, OpenCvSharp.MatType.CV_8U, scale, offset);

        OpenCvSharp.Cv2.ImEncode(".jpg", stretched, out jpegBytes);
    }
    else
    {
        // Already 8-bit — decode and re-encode as JPEG
        using var mat = OpenCvSharp.Cv2.ImDecode(frame.ImageBytes, OpenCvSharp.ImreadModes.Color);
        if (mat.Empty())
            return File(frame.ImageBytes, "image/jpeg");

        OpenCvSharp.Cv2.ImEncode(".jpg", mat, out jpegBytes);
    }

    return File(jpegBytes, "image/jpeg");
}
```

Add `OpenCvSharp` NuGet reference to `SkyWatch.Web.csproj` if not already present, or add a project reference to `SkyWatch.Capture` (which already has OpenCvSharp).

**Step 2: Run build**

Run: `dotnet build SkyWatch.sln`
Expected: Build succeeds

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: update PreviewApiController for 16-bit frame auto-stretch"
```

---

### Task 11: Update TypeScript types and Sources page UI

**Files:**
- Modify: `src/SkyWatch.Web/ClientApp/src/services/types.ts`
- Modify: `src/SkyWatch.Web/ClientApp/src/pages/Sources.tsx`

**Step 1: Update CaptureSettings TypeScript type**

In `src/SkyWatch.Web/ClientApp/src/services/types.ts`:

```typescript
export interface CaptureSettings {
  sourceType: 'UsbCamera' | 'Rtsp' | 'Replay' | 'AlpacaCamera' | 'AscomCamera';
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
  // Alpaca
  alpacaHost: string;
  alpacaPort: number;
  alpacaDeviceNumber: number;
  // ASCOM COM
  ascomProgId: string;
  // Exposure
  exposureDuration: number;
  autoExposure: boolean;
  autoExposureTarget: number;
  exposureMin: number;
  exposureMax: number;
}
```

Update `CaptureStatusInfo` to include auto-exposure state:

```typescript
export interface CaptureStatusInfo {
  isConnected: boolean;
  sourceType: string;
  sourceDescription: string;
  framesCaptured: number;
  errorMessage: string | null;
  currentExposureDuration: number | null;
  autoExposureEnabled: boolean;
  autoExposureTarget: number | null;
}
```

**Step 2: Update defaultSettings in Sources.tsx**

```typescript
const defaultSettings: CaptureSettings = {
  sourceType: 'UsbCamera',
  sourceIdentifier: '',
  deviceIndex: 0,
  frameIntervalSeconds: 5,
  targetWidth: 1280,
  targetHeight: 720,
  reconnectIntervalSeconds: 10,
  staleFrameTimeoutSeconds: 30,
  connectionTimeoutSeconds: 10,
  flipHorizontal: false,
  flipVertical: false,
  rotationDegrees: 0,
  alpacaHost: '',
  alpacaPort: 11111,
  alpacaDeviceNumber: 0,
  ascomProgId: '',
  exposureDuration: 2.0,
  autoExposure: false,
  autoExposureTarget: 128,
  exposureMin: 0.1,
  exposureMax: 30.0,
};
```

**Step 3: Add Alpaca Camera and ASCOM Camera options to source type dropdown**

In the `<Select>` for source type, add:

```tsx
<MenuItem value="AlpacaCamera">Alpaca Camera</MenuItem>
<MenuItem value="AscomCamera">ASCOM Camera (Windows)</MenuItem>
```

**Step 4: Add conditional fields for AlpacaCamera**

After the Replay fields block, add:

```tsx
{settings.sourceType === 'AlpacaCamera' && (
  <>
    <Grid size={{ xs: 12, sm: 4 }}>
      <TextField
        fullWidth
        label="Alpaca Host"
        value={settings.alpacaHost}
        onChange={(e) => update('alpacaHost', e.target.value)}
        placeholder="192.168.1.100"
      />
    </Grid>
    <Grid size={{ xs: 12, sm: 4 }}>
      <TextField
        fullWidth
        label="Alpaca Port"
        type="number"
        value={settings.alpacaPort}
        onChange={(e) => update('alpacaPort', Number(e.target.value))}
        slotProps={{ htmlInput: { min: 1, max: 65535 } }}
      />
    </Grid>
    <Grid size={{ xs: 12, sm: 4 }}>
      <TextField
        fullWidth
        label="Device Number"
        type="number"
        value={settings.alpacaDeviceNumber}
        onChange={(e) => update('alpacaDeviceNumber', Number(e.target.value))}
        slotProps={{ htmlInput: { min: 0 } }}
      />
    </Grid>
  </>
)}
```

**Step 5: Add conditional fields for AscomCamera**

```tsx
{settings.sourceType === 'AscomCamera' && (
  <Grid size={{ xs: 12, sm: 8 }}>
    <TextField
      fullWidth
      label="ASCOM ProgID"
      value={settings.ascomProgId}
      onChange={(e) => update('ascomProgId', e.target.value)}
      placeholder="ASCOM.Simulator.Camera"
      helperText="Windows only. Example: ASCOM.Simulator.Camera"
    />
  </Grid>
)}
```

**Step 6: Add exposure settings section (shown for Alpaca and ASCOM)**

Add a new Card after the source-specific fields, visible for camera sources:

```tsx
{(settings.sourceType === 'AlpacaCamera' || settings.sourceType === 'AscomCamera') && (
  <Grid size={12}>
    <Card>
      <CardContent>
        <Typography variant="h6" gutterBottom>Exposure Settings</Typography>
        <Grid container spacing={2}>
          <Grid size={{ xs: 12, sm: 4 }}>
            <TextField
              fullWidth
              label="Exposure Duration (s)"
              type="number"
              value={settings.exposureDuration}
              onChange={(e) => update('exposureDuration', Number(e.target.value))}
              slotProps={{ htmlInput: { min: 0.001, step: 0.1 } }}
            />
          </Grid>
          <Grid size={{ xs: 12, sm: 4 }}>
            <FormControlLabel
              control={
                <Switch
                  checked={settings.autoExposure}
                  onChange={(e) => update('autoExposure', e.target.checked)}
                />
              }
              label="Auto-Exposure"
            />
          </Grid>
          {settings.autoExposure && (
            <>
              <Grid size={{ xs: 12, sm: 4 }}>
                <TextField
                  fullWidth
                  label="Target Brightness (0-255)"
                  type="number"
                  value={settings.autoExposureTarget}
                  onChange={(e) => update('autoExposureTarget', Number(e.target.value))}
                  slotProps={{ htmlInput: { min: 0, max: 255 } }}
                />
              </Grid>
              <Grid size={{ xs: 12, sm: 4 }}>
                <TextField
                  fullWidth
                  label="Min Exposure (s)"
                  type="number"
                  value={settings.exposureMin}
                  onChange={(e) => update('exposureMin', Number(e.target.value))}
                  slotProps={{ htmlInput: { min: 0.001, step: 0.1 } }}
                />
              </Grid>
              <Grid size={{ xs: 12, sm: 4 }}>
                <TextField
                  fullWidth
                  label="Max Exposure (s)"
                  type="number"
                  value={settings.exposureMax}
                  onChange={(e) => update('exposureMax', Number(e.target.value))}
                  slotProps={{ htmlInput: { min: 0.1, step: 1 } }}
                />
              </Grid>
            </>
          )}
        </Grid>
      </CardContent>
    </Card>
  </Grid>
)}
```

**Step 7: Display auto-exposure status in connection info**

If `captureStatus?.autoExposureEnabled`, show current exposure next to the connection chip:

```tsx
{captureStatus?.autoExposureEnabled && captureStatus.currentExposureDuration != null && (
  <Chip
    label={`Exposure: ${captureStatus.currentExposureDuration.toFixed(2)}s`}
    size="small"
    variant="outlined"
  />
)}
```

**Step 8: Build the frontend**

Run: `cd src/SkyWatch.Web/ClientApp && npm run build`
Expected: Build succeeds

**Step 9: Commit**

```bash
git add -A
git commit -m "feat: add Alpaca and ASCOM camera options to Sources page UI"
```

---

### Task 12: Register HttpClientFactory in DI and update FrameSourceManager

**Files:**
- Modify: `src/SkyWatch.Host/ServiceRegistration.cs` (or wherever DI is configured)
- Modify: `src/SkyWatch.Capture/FrameSourceManager.cs`

**Step 1: Check current DI registration**

Read `src/SkyWatch.Host/ServiceRegistration.cs` and `src/SkyWatch.Capture/FrameSourceManager.cs` to understand current DI wiring.

**Step 2: Register IHttpClientFactory**

In the DI registration, add:

```csharp
services.AddHttpClient("AlpacaCamera");
```

**Step 3: Update FrameSourceManager to accept and pass IHttpClientFactory**

The `FrameSourceManager` creates frame sources via `FrameSourceFactory`. It needs to receive and forward `IHttpClientFactory` so Alpaca sources get a properly managed `HttpClient`.

**Step 4: Run full test suite**

Run: `dotnet test SkyWatch.sln`
Expected: All tests pass

**Step 5: Run build**

Run: `dotnet build SkyWatch.sln`
Expected: Build succeeds

**Step 6: Commit**

```bash
git add -A
git commit -m "feat: register HttpClientFactory and wire Alpaca camera DI"
```

---

### Task 13: Update documentation

**Files:**
- Modify: `README.md`
- Modify: `CLAUDE.md` (if camera types are listed)

**Step 1: Update README with new camera source types**

Add Alpaca Camera and ASCOM Camera to the supported sources section. Document:
- Alpaca: cross-platform, requires Alpaca server (host/port/device number)
- ASCOM COM: Windows-only, requires ASCOM Platform and driver ProgID
- Auto-exposure: optional feedback loop
- 16-bit capture: full quality preserved, auto-stretched for preview

**Step 2: Update CLAUDE.md if needed**

Add `AlpacaCamera` and `AscomCamera` to the `FrameSourceType` enum documentation.

**Step 3: Commit**

```bash
git add -A
git commit -m "docs: update README and CLAUDE.md for ASCOM/Alpaca camera support"
```

---

### Task 14: Run full verification

**Step 1: Format check**

Run: `dotnet format SkyWatch.sln --verify-no-changes`
Expected: No formatting issues

**Step 2: Build**

Run: `dotnet build SkyWatch.sln`
Expected: Build succeeds with no warnings

**Step 3: Run all tests**

Run: `dotnet test SkyWatch.sln`
Expected: All tests pass

**Step 4: Build frontend**

Run: `cd src/SkyWatch.Web/ClientApp && npm run build`
Expected: Build succeeds

**Step 5: Fix any issues found, then commit**

```bash
git add -A
git commit -m "chore: final verification pass for ASCOM/Alpaca camera support"
```
