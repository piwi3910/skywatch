using System.Text;
using Microsoft.Extensions.Logging;
using SkyWatch.Contracts.Enums;
using SkyWatch.Contracts.Interfaces;
using SkyWatch.Contracts.Models;
using SkyWatch.Core.Configuration;
using SkyWatch.Safety.Rules;

namespace SkyWatch.Safety;

/// <summary>
/// Evaluates sky safety by running a set of rules against metrics and applying hysteresis.
/// </summary>
public sealed partial class SafetyEvaluatorEngine : ISafetyEvaluator
{
    private readonly ProfileSettings settings;
    private readonly HysteresisTracker hysteresis;
    private readonly IReadOnlyList<ISafetyRule> rules;
    private readonly ILogger<SafetyEvaluatorEngine> logger;

    public SafetyEvaluatorEngine(ProfileSettings settings, ILogger<SafetyEvaluatorEngine> logger)
    {
        this.settings = settings;
        this.logger = logger;
        hysteresis = new HysteresisTracker(DateTimeOffset.UtcNow);
        rules = CreateRules();
    }

    internal SafetyEvaluatorEngine(ProfileSettings settings, ILogger<SafetyEvaluatorEngine> logger, DateTimeOffset startupTime)
    {
        this.settings = settings;
        this.logger = logger;
        hysteresis = new HysteresisTracker(startupTime);
        rules = CreateRules();
    }

    public SafetyEvaluation Evaluate(FrameAnalysisResult analysis, DateTimeOffset now)
    {
        // 1. Check manual override
        if (settings.ManualOverride == ManualOverrideMode.ForceUnsafe)
        {
            LogManualOverride(logger, "ForceUnsafe");
            return new SafetyEvaluation
            {
                State = SafetyState.Unsafe,
                IsSafe = false,
                Reasons = UnsafeReason.ManualUnsafe,
                Confidence = 1.0,
                Explanation = "Manual override: forced unsafe",
                EvaluatedAt = now,
                LastStateChange = hysteresis.LastTransitionTime,
            };
        }

        if (settings.ManualOverride == ManualOverrideMode.ForceSafe)
        {
            LogManualOverride(logger, "ForceSafe");
            return new SafetyEvaluation
            {
                State = SafetyState.Safe,
                IsSafe = true,
                Reasons = UnsafeReason.None,
                Confidence = 1.0,
                Explanation = "Manual override: forced safe",
                EvaluatedAt = now,
                LastStateChange = hysteresis.LastTransitionTime,
            };
        }

        // 2. Check analysis failure
        if (!analysis.AnalysisSucceeded)
        {
            LogAnalysisFailed(logger, analysis.ErrorMessage ?? "Unknown error");
            return BuildTransitionResult(SafetyState.Unsafe, UnsafeReason.AnalysisFailed, 0, now);
        }

        // 3. Run all rules, collect UnsafeReason flags
        var reasons = UnsafeReason.None;
        foreach (var rule in rules)
            reasons |= rule.Evaluate(analysis.Metrics, settings.Thresholds);

        // 4. Determine proposed state
        var proposedState = reasons == UnsafeReason.None ? SafetyState.Safe : SafetyState.Unsafe;

        // 5. Apply hysteresis
        if (hysteresis.IsInWarmup(settings.Timing, now))
        {
            LogWarmup(logger);
            return new SafetyEvaluation
            {
                State = SafetyState.Initializing,
                IsSafe = false,
                Reasons = reasons,
                Confidence = analysis.Metrics.Confidence,
                Explanation = "System warming up",
                EvaluatedAt = now,
                LastStateChange = hysteresis.LastTransitionTime,
            };
        }

        return BuildTransitionResult(proposedState, reasons, analysis.Metrics.Confidence, now);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Manual override active: {Mode}")]
    private static partial void LogManualOverride(ILogger logger, string mode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Frame analysis failed: {Error}")]
    private static partial void LogAnalysisFailed(ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "System in warmup period")]
    private static partial void LogWarmup(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Safety state transitioned to {State}")]
    private static partial void LogStateTransition(ILogger logger, string state);

    private static string BuildExplanation(SafetyState state, UnsafeReason reasons)
    {
        if (state == SafetyState.Safe)
            return "Safe: all metrics within thresholds";

        if (state == SafetyState.Initializing)
            return "System initializing";

        if (reasons == UnsafeReason.None)
            return $"{state}: no specific reasons";

        var parts = new StringBuilder();
        parts.Append("Unsafe: ");

        var first = true;
        foreach (UnsafeReason value in Enum.GetValues<UnsafeReason>())
        {
            if (value == UnsafeReason.None)
                continue;

            if ((reasons & value) != 0)
            {
                if (!first)
                    parts.Append(", ");

                parts.Append(FormatReason(value));
                first = false;
            }
        }

        return parts.ToString();
    }

    private static string FormatReason(UnsafeReason reason)
    {
        return reason switch
        {
            UnsafeReason.CloudCoverHigh => "cloud cover high",
            UnsafeReason.BrightnessTooHigh => "brightness too high",
            UnsafeReason.StarCountTooLow => "star count too low",
            UnsafeReason.ObstructionDetected => "obstruction detected",
            UnsafeReason.LensCondensationDetected => "lens condensation detected",
            UnsafeReason.RainSuspected => "rain suspected",
            UnsafeReason.CameraDisconnected => "camera disconnected",
            UnsafeReason.AnalysisFailed => "analysis failed",
            UnsafeReason.ManualUnsafe => "manual override",
            _ => reason.ToString(),
        };
    }

    private static IReadOnlyList<ISafetyRule> CreateRules() =>
    [
        new BrightnessRule(),
        new StarCountRule(),
        new CloudinessRule(),
        new ObstructionRule(),
        new EntropyRule(),
        new EdgeDensityRule(),
    ];

    private SafetyEvaluation BuildTransitionResult(SafetyState proposedState, UnsafeReason reasons, double confidence, DateTimeOffset now)
    {
        bool shouldTransition = hysteresis.ShouldTransition(proposedState, settings.Timing, now);
        SafetyState finalState;

        if (shouldTransition)
        {
            hysteresis.TransitionTo(proposedState, now);
            finalState = proposedState;
            LogStateTransition(logger, finalState.ToString());
        }
        else
        {
            finalState = hysteresis.CurrentState;
        }

        var explanation = BuildExplanation(finalState, reasons);

        return new SafetyEvaluation
        {
            State = finalState,
            IsSafe = finalState == SafetyState.Safe,
            Reasons = reasons,
            Confidence = confidence,
            Explanation = explanation,
            EvaluatedAt = now,
            LastStateChange = hysteresis.LastTransitionTime,
        };
    }
}
