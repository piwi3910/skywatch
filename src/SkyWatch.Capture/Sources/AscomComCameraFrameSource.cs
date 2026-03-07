using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using SkyWatch.Contracts.Enums;
using SkyWatch.Contracts.Interfaces;
using SkyWatch.Contracts.Models;
using SkyWatch.Core.Configuration;

namespace SkyWatch.Capture.Sources;

/// <summary>
/// A frame source that captures images from a camera via ASCOM COM drivers (Windows only).
/// </summary>
/// <remarks>
/// This class uses conditional compilation (<c>#if WINDOWS</c>) so that it compiles cleanly
/// on macOS and Linux where ASCOM COM drivers are not available.
/// </remarks>
public sealed partial class AscomComCameraFrameSource : IFrameSource
{
    private readonly CaptureSettings settings;
    private readonly ILogger<AscomComCameraFrameSource> logger;

    private bool isStarted;
    private int framesCaptured;
    private DateTimeOffset? lastFrameTime;
    private string? errorMessage;
    private double currentExposure;

#if WINDOWS
    private ASCOM.DriverAccess.Camera? camera;
#endif

    /// <summary>
    /// Initializes a new instance of the <see cref="AscomComCameraFrameSource"/> class.
    /// </summary>
    /// <param name="settings">The capture settings containing ASCOM ProgID and exposure configuration.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="PlatformNotSupportedException">Thrown when not running on Windows.</exception>
    public AscomComCameraFrameSource(CaptureSettings settings, ILogger<AscomComCameraFrameSource> logger)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        currentExposure = settings.ExposureDuration;
        isStarted = false;
        framesCaptured = 0;
        lastFrameTime = null;
        errorMessage = null;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("ASCOM COM camera is only supported on Windows.");
        }
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
#if WINDOWS
        LogStarting(settings.AscomProgId);

        try
        {
            camera = new ASCOM.DriverAccess.Camera(settings.AscomProgId);
            camera.Connected = true;

            isStarted = true;
            errorMessage = null;
            LogStarted(settings.AscomProgId);
        }
        catch (Exception ex)
        {
            LogConnectionFailed(settings.AscomProgId, ex);
            errorMessage = ex.Message;
            throw;
        }

        return Task.CompletedTask;
#else
        throw new PlatformNotSupportedException("ASCOM COM camera is only supported on Windows.");
#endif
    }

    /// <inheritdoc/>
    public Task<FrameData?> GetLatestFrameAsync(CancellationToken cancellationToken)
    {
#if WINDOWS
        if (!isStarted || camera is null)
        {
            return Task.FromResult<FrameData?>(null);
        }

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

            int maxAdu = camera.MaxADU;
            int bitsPerChannel = maxAdu <= 255 ? 8 : maxAdu <= 4095 ? 12 : 16;

            // For storage, use either 8-bit or 16-bit (12-bit stored in 16-bit container)
            int storageBits = bitsPerChannel <= 8 ? 8 : 16;
            int bytesPerPixel = storageBits / 8;

            var pixelData = new byte[width * height * bytesPerPixel];

            unsafe
            {
                fixed (byte* destPtr = pixelData)
                {
                    if (bytesPerPixel == 1)
                    {
                        for (int y = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                destPtr[(y * width) + x] = (byte)imageArray[x, y];
                            }
                        }
                    }
                    else
                    {
                        var dest16 = (ushort*)destPtr;
                        for (int y = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                dest16[(y * width) + x] = (ushort)imageArray[x, y];
                            }
                        }
                    }
                }
            }

            string pixelFormat = storageBits == 8 ? "Mono8" : "Mono16";

            var frameData = new FrameData
            {
                ImageBytes = pixelData,
                Width = width,
                Height = height,
                Channels = 1,
                BitsPerChannel = storageBits,
                PixelFormat = pixelFormat,
                CapturedAt = DateTimeOffset.UtcNow,
                SourceType = FrameSourceType.AscomCamera,
            };

            framesCaptured++;
            lastFrameTime = DateTimeOffset.UtcNow;
            errorMessage = null;

            if (settings.AutoExposure)
            {
                AdjustExposure(frameData);
            }

            return Task.FromResult<FrameData?>(frameData);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogCaptureError(ex);
            errorMessage = ex.Message;
            return Task.FromResult<FrameData?>(null);
        }
#else
        throw new PlatformNotSupportedException("ASCOM COM camera is only supported on Windows.");
#endif
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
#if WINDOWS
        if (!isStarted)
        {
            return Task.CompletedTask;
        }

        try
        {
            if (camera is not null)
            {
                camera.Connected = false;
                camera.Dispose();
                camera = null;
            }
        }
        catch (Exception ex)
        {
            LogDisconnectError(ex);
        }

        isStarted = false;
        LogStopped(settings.AscomProgId);

        return Task.CompletedTask;
#else
        throw new PlatformNotSupportedException("ASCOM COM camera is only supported on Windows.");
#endif
    }

    /// <inheritdoc/>
    public CaptureStatus GetStatus()
    {
        return new CaptureStatus
        {
            IsConnected = isStarted,
            SourceType = FrameSourceType.AscomCamera,
            SourceDescription = $"ASCOM COM Camera: {settings.AscomProgId}",
            LastFrameAt = lastFrameTime,
            FramesCaptured = framesCaptured,
            ErrorMessage = errorMessage,
            CurrentExposureDuration = currentExposure,
            AutoExposureEnabled = settings.AutoExposure,
            AutoExposureTarget = settings.AutoExposure ? settings.AutoExposureTarget : null,
        };
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (isStarted)
        {
            await StopAsync(CancellationToken.None);
        }
    }

    private void AdjustExposure(FrameData frame)
    {
        double sum = 0;
        int bytesPerSample = frame.BitsPerChannel / 8;
        int sampleCount = frame.ImageBytes.Length / bytesPerSample;

        if (sampleCount == 0)
        {
            return;
        }

        double maxValue = frame.BitsPerChannel == 16 ? 65535.0 : 255.0;

        for (int i = 0; i < frame.ImageBytes.Length; i += bytesPerSample)
        {
            double sampleValue = bytesPerSample == 2
                ? BitConverter.ToUInt16(frame.ImageBytes, i)
                : frame.ImageBytes[i];
            sum += sampleValue;
        }

        double meanRaw = sum / sampleCount;
        double meanBrightness = meanRaw / maxValue * 255.0;

        const double dampingFactor = 0.5;
        double ratio = settings.AutoExposureTarget / Math.Max(meanBrightness, 1.0);
        double adjustment = 1.0 + ((ratio - 1.0) * dampingFactor);
        double newExposure = currentExposure * adjustment;

        newExposure = Math.Clamp(newExposure, settings.ExposureMin, settings.ExposureMax);

        LogAutoExposureAdjusted(currentExposure, newExposure, meanBrightness);
        currentExposure = newExposure;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting ASCOM COM camera: {ProgId}")]
    private partial void LogStarting(string progId);

    [LoggerMessage(Level = LogLevel.Information, Message = "ASCOM COM camera started: {ProgId}")]
    private partial void LogStarted(string progId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to connect to ASCOM COM camera: {ProgId}")]
    private partial void LogConnectionFailed(string progId, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error capturing frame from ASCOM COM camera")]
    private partial void LogCaptureError(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Error disconnecting from ASCOM COM camera")]
    private partial void LogDisconnectError(Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "ASCOM COM camera stopped: {ProgId}")]
    private partial void LogStopped(string progId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Auto-exposure adjusted from {OldExposure:F3}s to {NewExposure:F3}s (mean brightness: {MeanBrightness:F1})")]
    private partial void LogAutoExposureAdjusted(double oldExposure, double newExposure, double meanBrightness);
}
