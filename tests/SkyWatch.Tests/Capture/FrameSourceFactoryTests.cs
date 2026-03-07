using Microsoft.Extensions.Logging;
using Moq;
using SkyWatch.Capture;
using SkyWatch.Capture.Sources;
using SkyWatch.Contracts.Enums;
using SkyWatch.Core.Configuration;

namespace SkyWatch.Tests.Capture;

public sealed class FrameSourceFactoryTests
{
    private readonly Mock<ILoggerFactory> loggerFactoryMock = new();

    public FrameSourceFactoryTests()
    {
        loggerFactoryMock
            .Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);
    }

    [Fact]
    public void CreatesReplayFrameSourceForReplayType()
    {
        var settings = new CaptureSettings { SourceType = FrameSourceType.Replay };

        var source = FrameSourceFactory.Create(settings, loggerFactoryMock.Object);

        Assert.IsType<ReplayFrameSource>(source);
    }

    [Fact]
    public void CreatesUsbCameraFrameSourceForUsbCameraType()
    {
        var settings = new CaptureSettings { SourceType = FrameSourceType.UsbCamera };

        var source = FrameSourceFactory.Create(settings, loggerFactoryMock.Object);

        Assert.IsType<UsbCameraFrameSource>(source);
    }

    [Fact]
    public void CreatesRtspFrameSourceForRtspType()
    {
        var settings = new CaptureSettings { SourceType = FrameSourceType.Rtsp };

        var source = FrameSourceFactory.Create(settings, loggerFactoryMock.Object);

        Assert.IsType<RtspFrameSource>(source);
    }

    [Fact]
    public void ThrowsArgumentNullExceptionForNullSettings()
    {
        Assert.Throws<ArgumentNullException>(() =>
            FrameSourceFactory.Create(null!, loggerFactoryMock.Object));
    }

    [Fact]
    public void ThrowsArgumentNullExceptionForNullLoggerFactory()
    {
        var settings = new CaptureSettings { SourceType = FrameSourceType.Replay };

        Assert.Throws<ArgumentNullException>(() =>
            FrameSourceFactory.Create(settings, null!));
    }
}
