using Microsoft.Extensions.Logging;
using OpenCvSharp;
using SkyWatch.Capture.Helpers;
using SkyWatch.Contracts.Enums;
using SkyWatch.Contracts.Interfaces;
using SkyWatch.Contracts.Models;
using SkyWatch.Core.Configuration;

namespace SkyWatch.Capture.Sources;

/// <summary>
/// A frame source that captures images from an RTSP network video stream.
/// </summary>
public sealed partial class RtspFrameSource : IFrameSource
{
    private readonly CaptureSettings settings;
    private readonly ILogger<RtspFrameSource> logger;
    private readonly Mat frameMat = new();
    private VideoCapture? capture;
    private bool isStarted;
    private int framesCaptured;
    private DateTimeOffset? lastFrameTime;
    private DateTimeOffset? lastReconnectAttempt;
    private string? errorMessage;

    /// <summary>
    /// Initializes a new instance of the <see cref="RtspFrameSource"/> class.
    /// </summary>
    /// <param name="settings">The capture settings.</param>
    /// <param name="logger">The logger instance.</param>
    public RtspFrameSource(CaptureSettings settings, ILogger<RtspFrameSource> logger)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        OpenCapture();
        isStarted = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<FrameData?> GetLatestFrameAsync(CancellationToken cancellationToken)
    {
        if (!isStarted)
        {
            return Task.FromResult<FrameData?>(null);
        }

        if (capture is null || !capture.IsOpened())
        {
            return HandleReconnect();
        }

        try
        {
            if (!capture.Read(frameMat) || frameMat.Empty())
            {
                LogFrameReadFailed(settings.SourceIdentifier);
                return HandleReconnect();
            }

            var frameData = MatConverter.MatToFrameData(frameMat, FrameSourceType.Rtsp);
            framesCaptured++;
            lastFrameTime = DateTimeOffset.UtcNow;
            errorMessage = null;

            return Task.FromResult<FrameData?>(frameData);
        }
        catch (OpenCVException ex)
        {
            LogCaptureError(settings.SourceIdentifier, ex);
            errorMessage = ex.Message;
            return HandleReconnect();
        }
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        ReleaseCapture();
        isStarted = false;
        LogStopped(settings.SourceIdentifier);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public CaptureStatus GetStatus()
    {
        return new CaptureStatus
        {
            IsConnected = isStarted && capture is not null && capture.IsOpened(),
            SourceType = FrameSourceType.Rtsp,
            SourceDescription = $"RTSP: {settings.SourceIdentifier}",
            LastFrameAt = lastFrameTime,
            FramesCaptured = framesCaptured,
            ErrorMessage = errorMessage,
        };
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (isStarted)
        {
            await StopAsync(CancellationToken.None);
        }

        frameMat.Dispose();
    }

    private void OpenCapture()
    {
        ReleaseCapture();

        capture = new VideoCapture(settings.SourceIdentifier, VideoCaptureAPIs.FFMPEG);

        // CAP_PROP_OPEN_TIMEOUT_MSEC = 53 (not exposed in all OpenCvSharp versions)
        const int capPropOpenTimeoutMsec = 53;
        capture.Set((VideoCaptureProperties)capPropOpenTimeoutMsec, settings.ConnectionTimeoutSeconds * 1000);

        if (capture.IsOpened())
        {
            LogStreamOpened(settings.SourceIdentifier);
            errorMessage = null;
        }
        else
        {
            errorMessage = $"Failed to open RTSP stream: {settings.SourceIdentifier}";
            LogStreamOpenFailed(settings.SourceIdentifier);
        }
    }

    private void ReleaseCapture()
    {
        if (capture is not null)
        {
            capture.Release();
            capture.Dispose();
            capture = null;
        }
    }

    private Task<FrameData?> HandleReconnect()
    {
        var now = DateTimeOffset.UtcNow;

        if (lastReconnectAttempt.HasValue)
        {
            var elapsed = now - lastReconnectAttempt.Value;
            if (elapsed.TotalSeconds < settings.ReconnectIntervalSeconds)
            {
                return Task.FromResult<FrameData?>(null);
            }
        }

        lastReconnectAttempt = now;
        LogAttemptingReconnect(settings.SourceIdentifier);
        errorMessage = $"Reconnecting to RTSP stream: {settings.SourceIdentifier}";

        try
        {
            OpenCapture();
        }
        catch (OpenCVException ex)
        {
            LogReconnectFailed(settings.SourceIdentifier, ex);
            errorMessage = $"Reconnect failed: {ex.Message}";
        }

        return Task.FromResult<FrameData?>(null);
    }

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "RTSP stream opened: {StreamUrl}")]
    private partial void LogStreamOpened(string streamUrl);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Error, Message = "Failed to open RTSP stream: {StreamUrl}")]
    private partial void LogStreamOpenFailed(string streamUrl);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Warning, Message = "Failed to read frame from RTSP stream: {StreamUrl}")]
    private partial void LogFrameReadFailed(string streamUrl);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Error, Message = "Error capturing frame from RTSP stream: {StreamUrl}")]
    private partial void LogCaptureError(string streamUrl, Exception ex);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Attempting reconnect to RTSP stream: {StreamUrl}")]
    private partial void LogAttemptingReconnect(string streamUrl);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Error, Message = "Reconnect to RTSP stream {StreamUrl} failed")]
    private partial void LogReconnectFailed(string streamUrl, Exception ex);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "RTSP frame source stopped for {StreamUrl}")]
    private partial void LogStopped(string streamUrl);
}
