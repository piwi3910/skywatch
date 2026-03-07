using Microsoft.Extensions.Logging;
using Moq;
using OpenCvSharp;
using SkyWatch.Capture.Sources;
using SkyWatch.Contracts.Enums;
using SkyWatch.Core.Configuration;

namespace SkyWatch.Tests.Capture;

public sealed class ReplayFrameSourceTests : IDisposable
{
    private readonly string tempDirectory;
    private readonly Mock<ILogger<ReplayFrameSource>> loggerMock = new();

    public ReplayFrameSourceTests()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), $"skywatch_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public async Task ReturnsFramesInOrder()
    {
        CreateTestImages(3);
        var settings = CreateSettings();
        await using var source = new ReplayFrameSource(settings, loggerMock.Object);
        await source.StartAsync(CancellationToken.None);

        var frame1 = await source.GetLatestFrameAsync(CancellationToken.None);
        var frame2 = await source.GetLatestFrameAsync(CancellationToken.None);
        var frame3 = await source.GetLatestFrameAsync(CancellationToken.None);

        Assert.NotNull(frame1);
        Assert.NotNull(frame2);
        Assert.NotNull(frame3);

        // Each frame should have different pixel data since they are different colors
        Assert.False(frame1.ImageBytes.SequenceEqual(frame2.ImageBytes));
        Assert.False(frame2.ImageBytes.SequenceEqual(frame3.ImageBytes));
    }

    [Fact]
    public async Task CyclesAfterReachingEnd()
    {
        CreateTestImages(2);
        var settings = CreateSettings();
        await using var source = new ReplayFrameSource(settings, loggerMock.Object);
        await source.StartAsync(CancellationToken.None);

        var frame1 = await source.GetLatestFrameAsync(CancellationToken.None);
        var frame2 = await source.GetLatestFrameAsync(CancellationToken.None);
        var frame3 = await source.GetLatestFrameAsync(CancellationToken.None);

        Assert.NotNull(frame1);
        Assert.NotNull(frame2);
        Assert.NotNull(frame3);

        // Frame 3 should be the same as frame 1 (cycled)
        Assert.True(frame1.ImageBytes.SequenceEqual(frame3.ImageBytes));
    }

    [Fact]
    public async Task ReportsCorrectFrameCount()
    {
        CreateTestImages(3);
        var settings = CreateSettings();
        await using var source = new ReplayFrameSource(settings, loggerMock.Object);
        await source.StartAsync(CancellationToken.None);

        _ = await source.GetLatestFrameAsync(CancellationToken.None);
        _ = await source.GetLatestFrameAsync(CancellationToken.None);

        var status = source.GetStatus();

        Assert.True(status.IsConnected);
        Assert.Equal(FrameSourceType.Replay, status.SourceType);
        Assert.Equal(2, status.FramesCaptured);
        Assert.NotNull(status.LastFrameAt);
    }

    [Fact]
    public async Task EmptyDirectoryReturnsNull()
    {
        var settings = CreateSettings();
        await using var source = new ReplayFrameSource(settings, loggerMock.Object);
        await source.StartAsync(CancellationToken.None);

        var frame = await source.GetLatestFrameAsync(CancellationToken.None);

        Assert.Null(frame);

        var status = source.GetStatus();
        Assert.False(status.IsConnected);
        Assert.NotNull(status.ErrorMessage);
    }

    [Fact]
    public async Task StartStopLifecycleWorks()
    {
        CreateTestImages(1);
        var settings = CreateSettings();
        await using var source = new ReplayFrameSource(settings, loggerMock.Object);

        var statusBefore = source.GetStatus();
        Assert.False(statusBefore.IsConnected);

        await source.StartAsync(CancellationToken.None);

        var statusDuring = source.GetStatus();
        Assert.True(statusDuring.IsConnected);

        await source.StopAsync(CancellationToken.None);

        var statusAfter = source.GetStatus();
        Assert.False(statusAfter.IsConnected);
    }

    [Fact]
    public async Task RespectsFrameInterval()
    {
        CreateTestImages(3);
        var settings = CreateSettings();
        settings.FrameIntervalSeconds = 60; // Very long interval
        await using var source = new ReplayFrameSource(settings, loggerMock.Object);
        await source.StartAsync(CancellationToken.None);

        // First frame should be returned (no previous frame time)
        var frame1 = await source.GetLatestFrameAsync(CancellationToken.None);
        Assert.NotNull(frame1);

        // Second request should return null because interval hasn't elapsed
        var frame2 = await source.GetLatestFrameAsync(CancellationToken.None);
        Assert.Null(frame2);
    }

    [Fact]
    public async Task NonExistentDirectoryReturnsNull()
    {
        var settings = new CaptureSettings
        {
            SourceType = FrameSourceType.Replay,
            SourceIdentifier = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid():N}"),
            FrameIntervalSeconds = 0,
        };

        await using var source = new ReplayFrameSource(settings, loggerMock.Object);
        await source.StartAsync(CancellationToken.None);

        var frame = await source.GetLatestFrameAsync(CancellationToken.None);

        Assert.Null(frame);
        Assert.False(source.GetStatus().IsConnected);
    }

    private void CreateTestImages(int count)
    {
        var colors = new Scalar[]
        {
            new(255, 0, 0),    // Blue
            new(0, 255, 0),    // Green
            new(0, 0, 255),    // Red
        };

        for (int i = 0; i < count; i++)
        {
            using var mat = new Mat(10, 10, MatType.CV_8UC3, colors[i % colors.Length]);
            var filePath = Path.Combine(tempDirectory, $"frame_{i:D3}.png");
            Cv2.ImWrite(filePath, mat);
        }
    }

    private CaptureSettings CreateSettings()
    {
        return new CaptureSettings
        {
            SourceType = FrameSourceType.Replay,
            SourceIdentifier = tempDirectory,
            FrameIntervalSeconds = 0,
        };
    }
}
