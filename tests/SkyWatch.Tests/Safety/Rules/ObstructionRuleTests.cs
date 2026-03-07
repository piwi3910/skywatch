using SkyWatch.Contracts.Enums;
using SkyWatch.Contracts.Models;
using SkyWatch.Core.Configuration;
using SkyWatch.Safety.Rules;

namespace SkyWatch.Tests.Safety.Rules;

public sealed class ObstructionRuleTests
{
    private readonly ObstructionRule rule = new();

    [Fact]
    public void EvaluateObstructionAboveThresholdReturnsObstructionDetected()
    {
        var metrics = new SkyMetrics { ObstructionScore = 0.9 };
        var thresholds = new ThresholdSettings { ObstructionThreshold = 0.8 };

        var result = rule.Evaluate(metrics, thresholds);

        Assert.Equal(UnsafeReason.ObstructionDetected, result);
    }

    [Fact]
    public void EvaluateObstructionBelowThresholdReturnsNone()
    {
        var metrics = new SkyMetrics { ObstructionScore = 0.5 };
        var thresholds = new ThresholdSettings { ObstructionThreshold = 0.8 };

        var result = rule.Evaluate(metrics, thresholds);

        Assert.Equal(UnsafeReason.None, result);
    }
}
