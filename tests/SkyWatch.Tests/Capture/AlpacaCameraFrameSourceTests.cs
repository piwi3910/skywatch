using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using SkyWatch.Capture.Sources;
using SkyWatch.Contracts.Enums;
using SkyWatch.Core.Configuration;

namespace SkyWatch.Tests.Capture;

public sealed class AlpacaCameraFrameSourceTests
{
    private readonly Mock<ILogger<AlpacaCameraFrameSource>> loggerMock = new();

    [Fact]
    public void GetStatusReturnsAlpacaCameraType()
    {
        var (source, _) = CreateSource();

        var status = source.GetStatus();

        Assert.Equal(FrameSourceType.AlpacaCamera, status.SourceType);
    }

    [Fact]
    public void GetStatusReportsNotConnectedBeforeStart()
    {
        var (source, _) = CreateSource();

        var status = source.GetStatus();

        Assert.False(status.IsConnected);
        Assert.Equal(0, status.FramesCaptured);
        Assert.Null(status.LastFrameAt);
    }

    [Fact]
    public async Task GetLatestFrameReturnsNullBeforeStart()
    {
        var (source, _) = CreateSource();

        var frame = await source.GetLatestFrameAsync(CancellationToken.None);

        Assert.Null(frame);
    }

    [Fact]
    public void AutoExposureStatusReportedInGetStatus()
    {
        var settings = CreateSettings();
        settings.AutoExposure = true;
        settings.AutoExposureTarget = 120.0;

        var (source, _) = CreateSource(settings);

        var status = source.GetStatus();

        Assert.True(status.AutoExposureEnabled);
        Assert.Equal(120.0, status.AutoExposureTarget);
        Assert.Equal(settings.ExposureDuration, status.CurrentExposureDuration);
    }

    private static CaptureSettings CreateSettings()
    {
        return new CaptureSettings
        {
            SourceType = FrameSourceType.AlpacaCamera,
            AlpacaHost = "localhost",
            AlpacaPort = 11111,
            AlpacaDeviceNumber = 0,
            ExposureDuration = 2.0,
            AutoExposure = false,
            AutoExposureTarget = 128.0,
            ExposureMin = 0.1,
            ExposureMax = 30.0,
        };
    }

    private (AlpacaCameraFrameSource Source, Mock<HttpMessageHandler> Handler) CreateSource(CaptureSettings? settings = null)
    {
        settings ??= CreateSettings();
        var handlerMock = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(handlerMock.Object);
        var source = new AlpacaCameraFrameSource(settings, httpClient, loggerMock.Object);
        return (source, handlerMock);
    }
}
