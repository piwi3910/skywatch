using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using SkyWatch.Capture.Helpers;
using SkyWatch.Contracts.Enums;
using SkyWatch.Contracts.Interfaces;
using SkyWatch.Contracts.Models;
using SkyWatch.Core.Configuration;

namespace SkyWatch.Capture.Sources;

/// <summary>
/// A frame source that captures images from a locally connected USB camera.
/// </summary>
public sealed partial class UsbCameraFrameSource : IFrameSource
{
    private readonly CaptureSettings settings;
    private readonly ILogger<UsbCameraFrameSource> logger;
    private readonly Mat frameMat = new();
    private VideoCapture? capture;
    private bool isStarted;
    private int framesCaptured;
    private DateTimeOffset? lastFrameTime;
    private DateTimeOffset? lastReconnectAttempt;
    private string? errorMessage;

    /// <summary>
    /// Initializes a new instance of the <see cref="UsbCameraFrameSource"/> class.
    /// </summary>
    /// <param name="settings">The capture settings.</param>
    /// <param name="logger">The logger instance.</param>
    public UsbCameraFrameSource(CaptureSettings settings, ILogger<UsbCameraFrameSource> logger)
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
                LogFrameReadFailed();
                return HandleReconnect();
            }

            var frameData = MatConverter.MatToFrameData(frameMat, FrameSourceType.UsbCamera);
            framesCaptured++;
            lastFrameTime = DateTimeOffset.UtcNow;
            errorMessage = null;

            return Task.FromResult<FrameData?>(frameData);
        }
        catch (OpenCVException ex)
        {
            LogCaptureError(ex);
            errorMessage = ex.Message;
            return HandleReconnect();
        }
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        ReleaseCapture();
        isStarted = false;
        LogStopped();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public CaptureStatus GetStatus()
    {
        return new CaptureStatus
        {
            IsConnected = isStarted && capture is not null && capture.IsOpened(),
            SourceType = FrameSourceType.UsbCamera,
            SourceDescription = $"USB Camera (device {settings.DeviceIndex})",
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

        capture = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new VideoCapture(settings.DeviceIndex, VideoCaptureAPIs.DSHOW)
            : new VideoCapture(settings.DeviceIndex);

        capture.Set(VideoCaptureProperties.FrameWidth, settings.TargetWidth);
        capture.Set(VideoCaptureProperties.FrameHeight, settings.TargetHeight);

        if (capture.IsOpened())
        {
            LogCameraOpened(settings.DeviceIndex);
            errorMessage = null;
        }
        else
        {
            errorMessage = $"Failed to open USB camera at device index {settings.DeviceIndex}";
            LogCameraOpenFailed(settings.DeviceIndex);
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
        LogAttemptingReconnect(settings.DeviceIndex);
        errorMessage = $"Reconnecting to USB camera at device index {settings.DeviceIndex}";

        try
        {
            OpenCapture();
        }
        catch (OpenCVException ex)
        {
            LogReconnectFailed(settings.DeviceIndex, ex);
            errorMessage = $"Reconnect failed: {ex.Message}";
        }

        return Task.FromResult<FrameData?>(null);
    }

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "USB camera opened at device index {DeviceIndex}")]
    private partial void LogCameraOpened(int deviceIndex);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Error, Message = "Failed to open USB camera at device index {DeviceIndex}")]
    private partial void LogCameraOpenFailed(int deviceIndex);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Warning, Message = "Failed to read frame from USB camera")]
    private partial void LogFrameReadFailed();

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Error, Message = "Error capturing frame from USB camera")]
    private partial void LogCaptureError(Exception ex);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Attempting reconnect to USB camera at device index {DeviceIndex}")]
    private partial void LogAttemptingReconnect(int deviceIndex);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Error, Message = "Reconnect to USB camera at device index {DeviceIndex} failed")]
    private partial void LogReconnectFailed(int deviceIndex, Exception ex);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "USB camera frame source stopped")]
    private partial void LogStopped();
}
