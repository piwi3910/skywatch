using System.Text.Json;
using System.Text.Json.Serialization;
using SkyWatch.Contracts.Enums;
using SkyWatch.Contracts.Interfaces;
using SkyWatch.Contracts.Models;
using SkyWatch.Core.Configuration;
using SkyWatch.Web.WebSocket;

namespace SkyWatch.Host.Workers;

/// <summary>
/// Background worker that orchestrates the capture, analyze, and evaluate pipeline.
/// </summary>
public sealed partial class SkyWatchWorker : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly IFrameSource frameSource;
    private readonly IFrameAnalyzer frameAnalyzer;
    private readonly ISafetyEvaluator safetyEvaluator;
    private readonly ISafetyStateStore stateStore;
    private readonly WebSocketConnectionManager webSocketManager;
    private readonly ProfileSettings profileSettings;
    private readonly ILogger<SkyWatchWorker> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkyWatchWorker"/> class.
    /// </summary>
    /// <param name="frameSource">The frame source for capturing images.</param>
    /// <param name="frameAnalyzer">The frame analyzer for processing images.</param>
    /// <param name="safetyEvaluator">The safety evaluator for determining sky safety.</param>
    /// <param name="stateStore">The state store for tracking current safety state.</param>
    /// <param name="webSocketManager">The WebSocket connection manager for broadcasting updates.</param>
    /// <param name="profileSettings">The active profile settings.</param>
    /// <param name="logger">The logger instance.</param>
    public SkyWatchWorker(
        IFrameSource frameSource,
        IFrameAnalyzer frameAnalyzer,
        ISafetyEvaluator safetyEvaluator,
        ISafetyStateStore stateStore,
        WebSocketConnectionManager webSocketManager,
        ProfileSettings profileSettings,
        ILogger<SkyWatchWorker> logger)
    {
        this.frameSource = frameSource;
        this.frameAnalyzer = frameAnalyzer;
        this.safetyEvaluator = safetyEvaluator;
        this.stateStore = stateStore;
        this.webSocketManager = webSocketManager;
        this.profileSettings = profileSettings;
        this.logger = logger;

        stateStore.StateChanged += OnStateChanged;
    }

    /// <inheritdoc/>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        LogWorkerStopping();
        await frameSource.StopAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        stateStore.StateChanged -= OnStateChanged;
        base.Dispose();
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await frameSource.StartAsync(stoppingToken);
        LogWorkerStarted(profileSettings.Capture.FrameIntervalSeconds);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(profileSettings.Capture.FrameIntervalSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var frame = await frameSource.GetLatestFrameAsync(stoppingToken);

                if (frame is null)
                {
                    LogFrameUnavailable();
                    var captureStatus = frameSource.GetStatus();
                    if (profileSettings.Timing.FailSafeOnCameraLoss && !captureStatus.IsConnected)
                    {
                        var failSafeEval = CreateCameraErrorEvaluation(DateTimeOffset.UtcNow);
                        stateStore.Update(failSafeEval);
                        await BroadcastStatusAsync(failSafeEval, null, captureStatus, stoppingToken);
                    }

                    continue;
                }

                var analysis = await frameAnalyzer.AnalyzeAsync(frame, stoppingToken);
                var evaluation = safetyEvaluator.Evaluate(analysis, DateTimeOffset.UtcNow);
                stateStore.Update(evaluation);
                await BroadcastStatusAsync(evaluation, analysis, frameSource.GetStatus(), stoppingToken);

                if (webSocketManager.ConnectionCount > 0)
                {
                    await webSocketManager.BroadcastBinaryAsync(frame.ImageBytes, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogProcessingError(ex);
            }
        }
    }

    private static SafetyEvaluation CreateCameraErrorEvaluation(DateTimeOffset now) => new()
    {
        State = SafetyState.CameraError,
        IsSafe = false,
        Reasons = UnsafeReason.CameraDisconnected,
        Confidence = 1.0,
        Explanation = "Camera disconnected - fail-safe activated",
        EvaluatedAt = now,
    };

    private async Task BroadcastStatusAsync(
        SafetyEvaluation evaluation,
        FrameAnalysisResult? analysis,
        CaptureStatus captureStatus,
        CancellationToken cancellationToken)
    {
        if (webSocketManager.ConnectionCount <= 0)
        {
            return;
        }

        var statusMessage = new
        {
            type = "status",
            data = new
            {
                safetyState = evaluation.State.ToString(),
                evaluation.IsSafe,
                reasons = evaluation.Reasons.ToString(),
                evaluation.Confidence,
                evaluation.Explanation,
                metrics = analysis is null
                    ? null
                    : new
                    {
                        brightness = analysis.Metrics.AverageBrightness,
                        starCount = analysis.Metrics.StarCount,
                        cloudiness = analysis.Metrics.CloudinessScore,
                        entropy = analysis.Metrics.Entropy,
                        edgeDensity = analysis.Metrics.EdgeDensity,
                        obstructionScore = analysis.Metrics.ObstructionScore,
                    },
                capture = new
                {
                    captureStatus.IsConnected,
                    sourceType = captureStatus.SourceType.ToString(),
                    captureStatus.FramesCaptured,
                    captureStatus.ErrorMessage,
                },
            },
        };

        var json = JsonSerializer.Serialize(statusMessage, JsonOptions);
        await webSocketManager.BroadcastTextAsync(json, cancellationToken);
    }

    private void OnStateChanged(object? sender, SafetyEvaluation evaluation)
    {
        LogStateChanged(evaluation.State.ToString(), evaluation.IsSafe, evaluation.Explanation);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "SkyWatch worker started with {IntervalSeconds}s capture interval")]
    private partial void LogWorkerStarted(int intervalSeconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No frame available from source")]
    private partial void LogFrameUnavailable();

    [LoggerMessage(Level = LogLevel.Error, Message = "Error during processing cycle")]
    private partial void LogProcessingError(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Safety state changed: {State} (IsSafe={IsSafe}) - {Explanation}")]
    private partial void LogStateChanged(string state, bool isSafe, string explanation);

    [LoggerMessage(Level = LogLevel.Information, Message = "SkyWatch worker stopping")]
    private partial void LogWorkerStopping();
}
