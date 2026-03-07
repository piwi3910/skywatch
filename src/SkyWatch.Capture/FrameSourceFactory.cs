using System.Net.Http;
using Microsoft.Extensions.Logging;
using SkyWatch.Capture.Sources;
using SkyWatch.Contracts.Enums;
using SkyWatch.Contracts.Interfaces;
using SkyWatch.Core.Configuration;

namespace SkyWatch.Capture;

/// <summary>
/// Factory that creates the appropriate <see cref="IFrameSource"/> based on configuration.
/// </summary>
public static class FrameSourceFactory
{
    /// <summary>
    /// Creates an <see cref="IFrameSource"/> based on the source type in the given settings.
    /// </summary>
    /// <param name="settings">The capture settings determining which source to create.</param>
    /// <param name="loggerFactory">The logger factory for creating typed loggers.</param>
    /// <returns>An <see cref="IFrameSource"/> matching the configured source type.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the source type is not supported.</exception>
    public static IFrameSource Create(CaptureSettings settings, ILoggerFactory loggerFactory)
    {
        return Create(settings, loggerFactory, httpClientFactory: null);
    }

    /// <summary>
    /// Creates an <see cref="IFrameSource"/> based on the source type in the given settings,
    /// using an optional <see cref="IHttpClientFactory"/> for HTTP-based sources.
    /// </summary>
    /// <param name="settings">The capture settings determining which source to create.</param>
    /// <param name="loggerFactory">The logger factory for creating typed loggers.</param>
    /// <param name="httpClientFactory">An optional HTTP client factory for Alpaca camera sources.</param>
    /// <returns>An <see cref="IFrameSource"/> matching the configured source type.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the source type is not supported.</exception>
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
