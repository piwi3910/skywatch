using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SkyWatch.Capture.Sources;
using SkyWatch.Contracts.Enums;
using SkyWatch.Contracts.Models;
using SkyWatch.Core.Configuration;
using SkyWatch.Safety;
using SkyWatch.Vision;

namespace SkyWatch.Tools.Replay;

/// <summary>
/// Orchestrates replay of saved images through the analysis and safety evaluation pipeline.
/// </summary>
public sealed partial class ReplayRunner
{
    private static readonly string[] SupportedExtensions = ["*.png", "*.jpg", "*.jpeg", "*.bmp"];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string sourceDirectory;
    private readonly ProfileSettings profileSettings;
    private readonly ILogger<ReplayRunner> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReplayRunner"/> class.
    /// </summary>
    /// <param name="sourceDirectory">Directory containing images to replay.</param>
    /// <param name="profileSettings">Profile settings for analysis and safety evaluation.</param>
    /// <param name="logger">The logger instance.</param>
    public ReplayRunner(string sourceDirectory, ProfileSettings profileSettings, ILogger<ReplayRunner> logger)
    {
        this.sourceDirectory = sourceDirectory ?? throw new ArgumentNullException(nameof(sourceDirectory));
        this.profileSettings = profileSettings ?? throw new ArgumentNullException(nameof(profileSettings));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Prints a human-readable summary of the replay report to the console.
    /// </summary>
    /// <param name="report">The replay report to display.</param>
    public static void PrintSummary(ReplayReport report)
    {
        Console.WriteLine();
        Console.WriteLine("SkyWatch Replay Report");
        Console.WriteLine("======================");
        Console.WriteLine($"Source: {report.SourceDirectory}");
        Console.WriteLine($"Profile: {report.ProfileName}");
        Console.WriteLine($"Frames: {report.TotalFrames}");

        if (report.TotalFrames > 0)
        {
            double safePercent = 100.0 * report.SafeFrames / report.TotalFrames;
            double unsafePercent = 100.0 * report.UnsafeFrames / report.TotalFrames;
            double errorPercent = 100.0 * report.ErrorFrames / report.TotalFrames;

            Console.WriteLine($"Safe: {report.SafeFrames} ({safePercent:F0}%)");
            Console.WriteLine($"Unsafe: {report.UnsafeFrames} ({unsafePercent:F0}%)");
            Console.WriteLine($"Errors: {report.ErrorFrames} ({errorPercent:F0}%)");
        }

        Console.WriteLine();
        Console.WriteLine("Frame Details:");

        foreach (var frame in report.Frames)
        {
            string stateLabel = frame.State switch
            {
                SafetyState.Safe => "Safe",
                SafetyState.Unsafe => "Unsafe",
                _ => frame.State.ToString(),
            };

            string reasonsLabel = frame.Reasons != UnsafeReason.None
                ? $" [{frame.Reasons}]"
                : string.Empty;

            string metrics = $"(brightness={frame.Metrics.AverageBrightness:F0}, " +
                             $"stars={frame.Metrics.StarCount}, " +
                             $"cloudiness={frame.Metrics.CloudinessScore:F2})";

            Console.WriteLine($"  {frame.FrameNumber,3}. {frame.FileName,-25} -> {stateLabel,-10}{reasonsLabel} {metrics}");
        }
    }

    /// <summary>
    /// Writes the replay report as JSON to the specified file path.
    /// </summary>
    /// <param name="report">The replay report to serialize.</param>
    /// <param name="outputPath">The file path to write the JSON report to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    public static async Task WriteJsonReportAsync(ReplayReport report, string outputPath, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(report, JsonOptions);
        await File.WriteAllTextAsync(outputPath, json, cancellationToken);
    }

    /// <summary>
    /// Runs the replay pipeline against all images in the source directory.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="ReplayReport"/> summarizing the results.</returns>
    public async Task<ReplayReport> RunAsync(CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;

        if (!Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException($"Source directory does not exist: {sourceDirectory}");
        }

        var imageFiles = SupportedExtensions
            .SelectMany(ext => Directory.GetFiles(sourceDirectory, ext))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (imageFiles.Length == 0)
        {
            throw new InvalidOperationException($"No image files found in directory: {sourceDirectory}");
        }

        LogReplayStarting(imageFiles.Length, sourceDirectory);

        var captureSettings = new CaptureSettings
        {
            SourceType = FrameSourceType.Replay,
            SourceIdentifier = sourceDirectory,
            FrameIntervalSeconds = 0,
        };

        // Use a separate logger factory for internal components to keep output clean
        using var internalLoggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Warning));

        var frameSource = new ReplayFrameSource(captureSettings, internalLoggerFactory.CreateLogger<ReplayFrameSource>());
        var analyzer = new FrameAnalyzerPipeline(profileSettings, internalLoggerFactory.CreateLogger<FrameAnalyzerPipeline>());

        // Set warmup and debounce to 0 so the safety engine evaluates each frame independently
        var replaySettings = new ProfileSettings
        {
            Name = profileSettings.Name,
            Description = profileSettings.Description,
            Capture = profileSettings.Capture,
            Thresholds = profileSettings.Thresholds,
            Timing = new TimingSettings
            {
                WarmupSeconds = 0,
                UnsafeDebounceSeconds = 0,
                SafeRecoveryDebounceSeconds = 0,
                MinimumUnsafeHoldSeconds = 0,
                FailSafeOnCameraLoss = profileSettings.Timing.FailSafeOnCameraLoss,
                FailSafeOnStaleFrame = profileSettings.Timing.FailSafeOnStaleFrame,
            },
            Masks = profileSettings.Masks,
            ManualOverride = profileSettings.ManualOverride,
        };

        var safetyEngine = new SafetyEvaluatorEngine(
            replaySettings,
            internalLoggerFactory.CreateLogger<SafetyEvaluatorEngine>());

        await frameSource.StartAsync(cancellationToken);

        var frameResults = new List<FrameResult>();
        int safeCount = 0;
        int unsafeCount = 0;
        int errorCount = 0;

        try
        {
            for (int i = 0; i < imageFiles.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var frame = await frameSource.GetLatestFrameAsync(cancellationToken);

                if (frame is null)
                {
                    LogFrameSkipped(i + 1);
                    errorCount++;
                    frameResults.Add(new FrameResult
                    {
                        FrameNumber = i + 1,
                        FileName = Path.GetFileName(imageFiles[i]),
                        Metrics = new SkyMetrics
                        {
                            AverageBrightness = 0,
                            StarCount = 0,
                            CloudinessScore = 0,
                            Entropy = 0,
                            EdgeDensity = 0,
                            ObstructionScore = 0,
                            Confidence = 0,
                        },
                        State = SafetyState.Unknown,
                        Reasons = UnsafeReason.AnalysisFailed,
                        Explanation = "Failed to read frame",
                    });
                    continue;
                }

                var analysis = await analyzer.AnalyzeAsync(frame, cancellationToken);
                var evaluation = safetyEngine.Evaluate(analysis, DateTimeOffset.UtcNow);

                var fileName = Path.GetFileName(imageFiles[i]);
                LogFrameProcessed(i + 1, fileName, evaluation.State);

                if (!analysis.AnalysisSucceeded)
                {
                    errorCount++;
                }
                else if (evaluation.IsSafe)
                {
                    safeCount++;
                }
                else
                {
                    unsafeCount++;
                }

                frameResults.Add(new FrameResult
                {
                    FrameNumber = i + 1,
                    FileName = fileName,
                    Metrics = analysis.Metrics,
                    State = evaluation.State,
                    Reasons = evaluation.Reasons,
                    Explanation = evaluation.Explanation,
                });
            }
        }
        finally
        {
            await frameSource.StopAsync(CancellationToken.None);
        }

        var completedAt = DateTimeOffset.UtcNow;
        LogReplayCompleted(frameResults.Count, safeCount, unsafeCount, errorCount);

        return new ReplayReport
        {
            SourceDirectory = sourceDirectory,
            ProfileName = profileSettings.Name,
            TotalFrames = frameResults.Count,
            SafeFrames = safeCount,
            UnsafeFrames = unsafeCount,
            ErrorFrames = errorCount,
            Frames = frameResults,
            StartedAt = startedAt,
            CompletedAt = completedAt,
        };
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting replay of {Count} images from {Directory}")]
    private partial void LogReplayStarting(int count, string directory);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Frame {FrameNumber}: {FileName} -> {State}")]
    private partial void LogFrameProcessed(int frameNumber, string fileName, SafetyState state);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Frame {FrameNumber} could not be read, skipping")]
    private partial void LogFrameSkipped(int frameNumber);

    [LoggerMessage(Level = LogLevel.Information, Message = "Replay completed: {Total} frames, {Safe} safe, {Unsafe} unsafe, {Errors} errors")]
    private partial void LogReplayCompleted(int total, int safe, int @unsafe, int errors);
}
