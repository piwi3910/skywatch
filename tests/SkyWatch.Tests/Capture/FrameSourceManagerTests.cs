using Microsoft.Extensions.Logging;
using Moq;
using SkyWatch.Capture;
using SkyWatch.Contracts.Enums;
using SkyWatch.Contracts.Interfaces;
using SkyWatch.Contracts.Models;
using SkyWatch.Core.Configuration;

namespace SkyWatch.Tests.Capture;

public sealed class FrameSourceManagerTests : IAsyncDisposable
{
    private readonly Mock<ILoggerFactory> loggerFactoryMock = new();
    private readonly Mock<ILogger<FrameSourceManager>> loggerMock = new();
    private readonly FrameSourceManager manager;

    public FrameSourceManagerTests()
    {
        loggerFactoryMock
            .Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);

        manager = new FrameSourceManager(loggerMock.Object, loggerFactoryMock.Object);
    }

    public async ValueTask DisposeAsync()
    {
        await manager.DisposeAsync();
    }

    [Fact]
    public void GetStatusWhenNoSourceReturnsDisconnected()
    {
        var status = manager.GetStatus();

        Assert.False(status.IsConnected);
        Assert.Equal("No source configured", status.SourceDescription);
    }

    [Fact]
    public async Task GetLatestFrameAsyncWhenNoSourceReturnsNull()
    {
        var frame = await manager.GetLatestFrameAsync(CancellationToken.None);

        Assert.Null(frame);
    }

    [Fact]
    public async Task StartAsyncWhenNoSourceDoesNotThrow()
    {
        await manager.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsyncWhenNoSourceDoesNotThrow()
    {
        await manager.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task InitializeAsyncCreatesAndStartsSource()
    {
        var sourceMock = new Mock<IFrameSource>();
        var settings = new CaptureSettings { SourceType = FrameSourceType.Replay };

        var factoryManager = new FrameSourceManager(loggerMock.Object, loggerFactoryMock.Object, _ => sourceMock.Object);
        await factoryManager.InitializeAsync(settings, CancellationToken.None);

        sourceMock.Verify(s => s.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
        await factoryManager.DisposeAsync();
    }

    [Fact]
    public async Task InitializeAsyncWhenFactoryThrowsDoesNotCrash()
    {
        var factoryManager = new FrameSourceManager(
            loggerMock.Object,
            loggerFactoryMock.Object,
            _ => throw new InvalidOperationException("Factory failed"));

        var settings = new CaptureSettings { SourceType = FrameSourceType.Replay };

        await factoryManager.InitializeAsync(settings, CancellationToken.None);

        Assert.False(factoryManager.GetStatus().IsConnected);
        await factoryManager.DisposeAsync();
    }

    [Fact]
    public async Task InitializeAsyncWhenStartThrowsDoesNotCrash()
    {
        var sourceMock = new Mock<IFrameSource>();
        sourceMock.Setup(s => s.StartAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Start failed"));
        sourceMock.Setup(s => s.GetStatus()).Returns(new CaptureStatus
        {
            IsConnected = false,
            SourceType = FrameSourceType.Replay,
            SourceDescription = "test",
        });

        var factoryManager = new FrameSourceManager(loggerMock.Object, loggerFactoryMock.Object, _ => sourceMock.Object);
        await factoryManager.InitializeAsync(new CaptureSettings(), CancellationToken.None);

        var status = factoryManager.GetStatus();
        Assert.NotNull(status);
        await factoryManager.DisposeAsync();
    }

    [Fact]
    public async Task SwitchSourceAsyncWithFrameSourceStopsOldAndStartsNew()
    {
        var oldSourceMock = new Mock<IFrameSource>();
        var newSourceMock = new Mock<IFrameSource>();

        var callCount = 0;
        var factoryManager = new FrameSourceManager(
            loggerMock.Object,
            loggerFactoryMock.Object,
            _ => callCount++ == 0 ? oldSourceMock.Object : newSourceMock.Object);

        await factoryManager.InitializeAsync(new CaptureSettings(), CancellationToken.None);

        await factoryManager.SwitchSourceAsync(newSourceMock.Object, CancellationToken.None);

        oldSourceMock.Verify(s => s.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
        oldSourceMock.Verify(s => s.DisposeAsync(), Times.Once);
        newSourceMock.Verify(s => s.StartAsync(It.IsAny<CancellationToken>()), Times.Once);

        await factoryManager.DisposeAsync();
    }

    [Fact]
    public async Task SwitchSourceAsyncWithSettingsCreatesFromFactoryAndSwitches()
    {
        var sourceMock = new Mock<IFrameSource>();
        sourceMock.Setup(s => s.GetStatus()).Returns(new CaptureStatus
        {
            IsConnected = true,
            SourceType = FrameSourceType.Rtsp,
            SourceDescription = "rtsp://test",
        });

        var factoryManager = new FrameSourceManager(loggerMock.Object, loggerFactoryMock.Object, _ => sourceMock.Object);
        var settings = new CaptureSettings { SourceType = FrameSourceType.Rtsp };

        await factoryManager.SwitchSourceAsync(settings, CancellationToken.None);

        var status = factoryManager.GetStatus();
        Assert.True(status.IsConnected);
        Assert.Equal(FrameSourceType.Rtsp, status.SourceType);

        await factoryManager.DisposeAsync();
    }

    [Fact]
    public async Task SwitchSourceAsyncWhenOldSourceStopThrowsStillStartsNewSource()
    {
        var oldSourceMock = new Mock<IFrameSource>();
        oldSourceMock.Setup(s => s.StopAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Stop failed"));

        var newSourceMock = new Mock<IFrameSource>();

        var callCount = 0;
        var factoryManager = new FrameSourceManager(
            loggerMock.Object,
            loggerFactoryMock.Object,
            _ => callCount++ == 0 ? oldSourceMock.Object : newSourceMock.Object);

        await factoryManager.InitializeAsync(new CaptureSettings(), CancellationToken.None);
        await factoryManager.SwitchSourceAsync(newSourceMock.Object, CancellationToken.None);

        newSourceMock.Verify(s => s.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
        await factoryManager.DisposeAsync();
    }

    [Fact]
    public async Task SwitchSourceAsyncWhenNewSourceStartThrowsDoesNotCrash()
    {
        var newSourceMock = new Mock<IFrameSource>();
        newSourceMock.Setup(s => s.StartAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Start failed"));
        newSourceMock.Setup(s => s.GetStatus()).Returns(new CaptureStatus
        {
            IsConnected = false,
            SourceType = FrameSourceType.Replay,
            SourceDescription = "test",
        });

        var factoryManager = new FrameSourceManager(loggerMock.Object, loggerFactoryMock.Object, _ => newSourceMock.Object);

        await factoryManager.SwitchSourceAsync(newSourceMock.Object, CancellationToken.None);

        var status = factoryManager.GetStatus();
        Assert.NotNull(status);
        await factoryManager.DisposeAsync();
    }

    [Fact]
    public async Task GetLatestFrameAsyncDelegatesToCurrentSource()
    {
        var expectedFrame = new FrameData
        {
            ImageBytes = new byte[] { 1, 2, 3 },
            Width = 100,
            Height = 100,
            Channels = 3,
            BitsPerChannel = 8,
            PixelFormat = "BGR24",
            CapturedAt = DateTimeOffset.UtcNow,
            SourceType = FrameSourceType.Replay,
        };

        var sourceMock = new Mock<IFrameSource>();
        sourceMock.Setup(s => s.GetLatestFrameAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedFrame);

        var factoryManager = new FrameSourceManager(loggerMock.Object, loggerFactoryMock.Object, _ => sourceMock.Object);
        await factoryManager.InitializeAsync(new CaptureSettings(), CancellationToken.None);

        var frame = await factoryManager.GetLatestFrameAsync(CancellationToken.None);

        Assert.Same(expectedFrame, frame);
        await factoryManager.DisposeAsync();
    }

    [Fact]
    public async Task StartAsyncDelegatesToCurrentSource()
    {
        var sourceMock = new Mock<IFrameSource>();

        var factoryManager = new FrameSourceManager(loggerMock.Object, loggerFactoryMock.Object, _ => sourceMock.Object);
        await factoryManager.InitializeAsync(new CaptureSettings(), CancellationToken.None);

        // StartAsync is called once during InitializeAsync, reset
        sourceMock.Invocations.Clear();

        await factoryManager.StartAsync(CancellationToken.None);

        sourceMock.Verify(s => s.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
        await factoryManager.DisposeAsync();
    }

    [Fact]
    public async Task StopAsyncDelegatesToCurrentSource()
    {
        var sourceMock = new Mock<IFrameSource>();

        var factoryManager = new FrameSourceManager(loggerMock.Object, loggerFactoryMock.Object, _ => sourceMock.Object);
        await factoryManager.InitializeAsync(new CaptureSettings(), CancellationToken.None);

        await factoryManager.StopAsync(CancellationToken.None);

        sourceMock.Verify(s => s.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
        await factoryManager.DisposeAsync();
    }

    [Fact]
    public async Task GetStatusDelegatesToCurrentSource()
    {
        var expectedStatus = new CaptureStatus
        {
            IsConnected = true,
            SourceType = FrameSourceType.UsbCamera,
            SourceDescription = "USB Camera 0",
            FramesCaptured = 42,
        };

        var sourceMock = new Mock<IFrameSource>();
        sourceMock.Setup(s => s.GetStatus()).Returns(expectedStatus);

        var factoryManager = new FrameSourceManager(loggerMock.Object, loggerFactoryMock.Object, _ => sourceMock.Object);
        await factoryManager.InitializeAsync(new CaptureSettings(), CancellationToken.None);

        var status = factoryManager.GetStatus();

        Assert.Same(expectedStatus, status);
        await factoryManager.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsyncStopsAndDisposesCurrentSource()
    {
        var sourceMock = new Mock<IFrameSource>();

        var factoryManager = new FrameSourceManager(loggerMock.Object, loggerFactoryMock.Object, _ => sourceMock.Object);
        await factoryManager.InitializeAsync(new CaptureSettings(), CancellationToken.None);

        await factoryManager.DisposeAsync();

        sourceMock.Verify(s => s.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
        sourceMock.Verify(s => s.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task DisposeAsyncWhenStopThrowsStillDisposes()
    {
        var sourceMock = new Mock<IFrameSource>();
        sourceMock.Setup(s => s.StopAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Stop failed"));

        var factoryManager = new FrameSourceManager(loggerMock.Object, loggerFactoryMock.Object, _ => sourceMock.Object);
        await factoryManager.InitializeAsync(new CaptureSettings(), CancellationToken.None);

        await factoryManager.DisposeAsync();

        sourceMock.Verify(s => s.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task SwitchSourceAsyncWithSettingsWhenFactoryThrowsDoesNotCrash()
    {
        var factoryManager = new FrameSourceManager(
            loggerMock.Object,
            loggerFactoryMock.Object,
            _ => throw new InvalidOperationException("Factory failed"));

        var settings = new CaptureSettings { SourceType = FrameSourceType.Replay };

        await factoryManager.SwitchSourceAsync(settings, CancellationToken.None);

        Assert.False(factoryManager.GetStatus().IsConnected);
        await factoryManager.DisposeAsync();
    }
}
