using Microsoft.Extensions.Logging;
using OpenCvSharp;
using SkyWatch.Capture.Helpers;
using SkyWatch.Contracts.Enums;
using SkyWatch.Contracts.Interfaces;
using SkyWatch.Contracts.Models;
using SkyWatch.Core.Configuration;

namespace SkyWatch.Capture.Sources;

/// <summary>
/// A frame source that replays images from a directory of image files.
/// </summary>
public sealed partial class ReplayFrameSource : IFrameSource
{
    private static readonly string[] SupportedExtensions = ["*.png", "*.jpg", "*.jpeg", "*.bmp"];

    private readonly CaptureSettings settings;
    private readonly ILogger<ReplayFrameSource> logger;
    private string[] imageFiles = [];
    private int currentIndex;
    private int framesCaptured;
    private bool isStarted;
    private DateTimeOffset? lastFrameTime;
    private string? errorMessage;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReplayFrameSource"/> class.
    /// </summary>
    /// <param name="settings">The capture settings.</param>
    /// <param name="logger">The logger instance.</param>
    public ReplayFrameSource(CaptureSettings settings, ILogger<ReplayFrameSource> logger)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var directory = settings.SourceIdentifier;

        if (!Directory.Exists(directory))
        {
            errorMessage = $"Replay directory does not exist: {directory}";
            LogDirectoryNotFound(directory);
            isStarted = true;
            imageFiles = [];
            return Task.CompletedTask;
        }

        imageFiles = SupportedExtensions
            .SelectMany(ext => Directory.GetFiles(directory, ext))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (imageFiles.Length == 0)
        {
            LogEmptyDirectory(directory);
            errorMessage = $"No image files found in directory: {directory}";
        }
        else
        {
            LogStarted(directory, imageFiles.Length);
            errorMessage = null;
        }

        currentIndex = 0;
        framesCaptured = 0;
        isStarted = true;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<FrameData?> GetLatestFrameAsync(CancellationToken cancellationToken)
    {
        if (!isStarted || imageFiles.Length == 0)
        {
            return Task.FromResult<FrameData?>(null);
        }

        if (lastFrameTime.HasValue)
        {
            var elapsed = DateTimeOffset.UtcNow - lastFrameTime.Value;
            if (elapsed.TotalSeconds < settings.FrameIntervalSeconds)
            {
                return Task.FromResult<FrameData?>(null);
            }
        }

        var filePath = imageFiles[currentIndex];

        try
        {
            using var mat = Cv2.ImRead(filePath, ImreadModes.Color);

            if (mat.Empty())
            {
                LogFailedToReadImage(filePath);
                currentIndex = (currentIndex + 1) % imageFiles.Length;
                return Task.FromResult<FrameData?>(null);
            }

            var frameData = MatConverter.MatToFrameData(mat, FrameSourceType.Replay);
            currentIndex = (currentIndex + 1) % imageFiles.Length;
            framesCaptured++;
            lastFrameTime = DateTimeOffset.UtcNow;

            LogFrameRead(filePath, framesCaptured);

            return Task.FromResult<FrameData?>(frameData);
        }
        catch (OpenCVException ex)
        {
            LogImageReadError(filePath, ex);
            currentIndex = (currentIndex + 1) % imageFiles.Length;
            return Task.FromResult<FrameData?>(null);
        }
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        isStarted = false;
        LogStopped();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public CaptureStatus GetStatus()
    {
        return new CaptureStatus
        {
            IsConnected = isStarted && imageFiles.Length > 0,
            SourceType = FrameSourceType.Replay,
            SourceDescription = $"Replay: {settings.SourceIdentifier} ({imageFiles.Length} files)",
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
    }

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Warning, Message = "Replay directory does not exist: {Directory}")]
    private partial void LogDirectoryNotFound(string directory);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Warning, Message = "No image files found in replay directory: {Directory}")]
    private partial void LogEmptyDirectory(string directory);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Replay frame source started from {Directory} with {Count} images")]
    private partial void LogStarted(string directory, int count);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Debug, Message = "Read frame from {FilePath} (total: {FramesCaptured})")]
    private partial void LogFrameRead(string filePath, int framesCaptured);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Warning, Message = "Failed to read image file: {FilePath}")]
    private partial void LogFailedToReadImage(string filePath);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Error, Message = "Error reading image file: {FilePath}")]
    private partial void LogImageReadError(string filePath, Exception ex);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Replay frame source stopped")]
    private partial void LogStopped();
}
