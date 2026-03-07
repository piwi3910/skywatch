using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SkyWatch.Contracts.Enums;
using SkyWatch.Contracts.Models;
using SkyWatch.Core.Configuration;
using SkyWatch.Safety;

namespace SkyWatch.Tests.Safety;

public sealed class SafetyEvaluatorEngineTests
{
    private static readonly DateTimeOffset StartTime = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly ILogger<SafetyEvaluatorEngine> Logger = NullLogger<SafetyEvaluatorEngine>.Instance;

    private static ProfileSettings DefaultSettings => new()
    {
        Thresholds = new ThresholdSettings(),
        Timing = new TimingSettings
        {
            WarmupSeconds = 0,
            UnsafeDebounceSeconds = 0,
            SafeRecoveryDebounceSeconds = 0,
            MinimumUnsafeHoldSeconds = 0,
        },
    };

    private static FrameAnalysisResult SafeAnalysis => new()
    {
        AnalysisSucceeded = true,
        AnalyzedAt = StartTime,
        FrameCapturedAt = StartTime,
        Metrics = new SkyMetrics
        {
            AverageBrightness = 50,
            StarCount = 20,
            CloudinessScore = 0.1,
            Entropy = 5.0,
            EdgeDensity = 0.05,
            ObstructionScore = 0.1,
            Confidence = 0.95,
        },
    };

    [Fact]
    public void EvaluateAllMetricsSafeReturnsSafeEvaluation()
    {
        var engine = new SafetyEvaluatorEngine(DefaultSettings, Logger, StartTime);

        var now = StartTime.AddSeconds(1);
        engine.Evaluate(SafeAnalysis, now);
        var result = engine.Evaluate(SafeAnalysis, now.AddSeconds(1));

        Assert.Equal(SafetyState.Safe, result.State);
        Assert.True(result.IsSafe);
        Assert.Equal(UnsafeReason.None, result.Reasons);
    }

    [Fact]
    public void EvaluateBrightnessTooHighReturnsUnsafeWithReason()
    {
        var engine = new SafetyEvaluatorEngine(DefaultSettings, Logger, StartTime);
        var analysis = new FrameAnalysisResult
        {
            AnalysisSucceeded = true,
            AnalyzedAt = StartTime,
            FrameCapturedAt = StartTime,
            Metrics = new SkyMetrics
            {
                AverageBrightness = 250,
                StarCount = 20,
                CloudinessScore = 0.1,
                Entropy = 5.0,
                EdgeDensity = 0.05,
                ObstructionScore = 0.1,
                Confidence = 0.9,
            },
        };

        var now = StartTime.AddSeconds(1);
        engine.Evaluate(analysis, now);
        var result = engine.Evaluate(analysis, now.AddSeconds(1));

        Assert.Equal(SafetyState.Unsafe, result.State);
        Assert.False(result.IsSafe);
        Assert.True(result.Reasons.HasFlag(UnsafeReason.BrightnessTooHigh));
    }

    [Fact]
    public void EvaluateMultipleReasonsReturnsCombinedFlags()
    {
        var engine = new SafetyEvaluatorEngine(DefaultSettings, Logger, StartTime);
        var analysis = new FrameAnalysisResult
        {
            AnalysisSucceeded = true,
            AnalyzedAt = StartTime,
            FrameCapturedAt = StartTime,
            Metrics = new SkyMetrics
            {
                AverageBrightness = 50,
                StarCount = 2,
                CloudinessScore = 0.9,
                Entropy = 5.0,
                EdgeDensity = 0.05,
                ObstructionScore = 0.1,
                Confidence = 0.9,
            },
        };

        var now = StartTime.AddSeconds(1);
        engine.Evaluate(analysis, now);
        var result = engine.Evaluate(analysis, now.AddSeconds(1));

        Assert.True(result.Reasons.HasFlag(UnsafeReason.CloudCoverHigh));
        Assert.True(result.Reasons.HasFlag(UnsafeReason.StarCountTooLow));
        Assert.Equal(SafetyState.Unsafe, result.State);
    }

    [Fact]
    public void EvaluateManualForceUnsafeReturnsUnsafeRegardlessOfMetrics()
    {
        var settings = DefaultSettings;
        settings.ManualOverride = ManualOverrideMode.ForceUnsafe;
        var engine = new SafetyEvaluatorEngine(settings, Logger, StartTime);

        var result = engine.Evaluate(SafeAnalysis, StartTime.AddSeconds(1));

        Assert.Equal(SafetyState.Unsafe, result.State);
        Assert.False(result.IsSafe);
        Assert.True(result.Reasons.HasFlag(UnsafeReason.ManualUnsafe));
    }

    [Fact]
    public void EvaluateManualForceSafeReturnsSafeRegardlessOfMetrics()
    {
        var settings = DefaultSettings;
        settings.ManualOverride = ManualOverrideMode.ForceSafe;
        var engine = new SafetyEvaluatorEngine(settings, Logger, StartTime);

        var badAnalysis = new FrameAnalysisResult
        {
            AnalysisSucceeded = true,
            AnalyzedAt = StartTime,
            FrameCapturedAt = StartTime,
            Metrics = new SkyMetrics
            {
                AverageBrightness = 250,
                StarCount = 0,
                CloudinessScore = 1.0,
                Entropy = 0.1,
                EdgeDensity = 0.0001,
                ObstructionScore = 1.0,
                Confidence = 0.9,
            },
        };

        var result = engine.Evaluate(badAnalysis, StartTime.AddSeconds(1));

        Assert.Equal(SafetyState.Safe, result.State);
        Assert.True(result.IsSafe);
    }

    [Fact]
    public void EvaluateAnalysisFailedReturnsUnsafeWithAnalysisFailed()
    {
        var engine = new SafetyEvaluatorEngine(DefaultSettings, Logger, StartTime);
        var analysis = new FrameAnalysisResult
        {
            AnalysisSucceeded = false,
            AnalyzedAt = StartTime,
            FrameCapturedAt = StartTime,
            ErrorMessage = "Camera timeout",
            Metrics = new SkyMetrics(),
        };

        var now = StartTime.AddSeconds(1);
        engine.Evaluate(analysis, now);
        var result = engine.Evaluate(analysis, now.AddSeconds(1));

        Assert.Equal(SafetyState.Unsafe, result.State);
        Assert.False(result.IsSafe);
        Assert.True(result.Reasons.HasFlag(UnsafeReason.AnalysisFailed));
    }

    [Fact]
    public void EvaluateExplanationContainsHumanReadableText()
    {
        var engine = new SafetyEvaluatorEngine(DefaultSettings, Logger, StartTime);
        var analysis = new FrameAnalysisResult
        {
            AnalysisSucceeded = true,
            AnalyzedAt = StartTime,
            FrameCapturedAt = StartTime,
            Metrics = new SkyMetrics
            {
                AverageBrightness = 50,
                StarCount = 2,
                CloudinessScore = 0.9,
                Entropy = 5.0,
                EdgeDensity = 0.05,
                ObstructionScore = 0.1,
                Confidence = 0.9,
            },
        };

        var now = StartTime.AddSeconds(1);
        engine.Evaluate(analysis, now);
        var result = engine.Evaluate(analysis, now.AddSeconds(1));

        Assert.Contains("cloud cover high", result.Explanation);
        Assert.Contains("star count too low", result.Explanation);
    }
}
