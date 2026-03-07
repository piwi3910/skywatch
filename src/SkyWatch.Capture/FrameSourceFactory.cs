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
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        return settings.SourceType switch
        {
            FrameSourceType.Replay => new ReplayFrameSource(settings, loggerFactory.CreateLogger<ReplayFrameSource>()),
            FrameSourceType.UsbCamera => new UsbCameraFrameSource(settings, loggerFactory.CreateLogger<UsbCameraFrameSource>()),
            FrameSourceType.Rtsp => new RtspFrameSource(settings, loggerFactory.CreateLogger<RtspFrameSource>()),
            _ => throw new ArgumentOutOfRangeException(nameof(settings), settings.SourceType, $"Unsupported frame source type: {settings.SourceType}"),
        };
    }
}
