using SkyWatch.Contracts.Enums;
using SkyWatch.Contracts.Models;
using SkyWatch.Core.Configuration;
using SkyWatch.Safety.Rules;

namespace SkyWatch.Tests.Safety.Rules;

public sealed class BrightnessRuleTests
{
    private readonly BrightnessRule rule = new();

    [Fact]
    public void EvaluateBrightnessAboveThresholdReturnsBrightnessTooHigh()
    {
        var metrics = new SkyMetrics { AverageBrightness = 250 };
        var thresholds = new ThresholdSettings { MaxAverageBrightness = 200 };

        var result = rule.Evaluate(metrics, thresholds);

        Assert.Equal(UnsafeReason.BrightnessTooHigh, result);
    }

    [Fact]
    public void EvaluateBrightnessBelowThresholdReturnsNone()
    {
        var metrics = new SkyMetrics { AverageBrightness = 150 };
        var thresholds = new ThresholdSettings { MaxAverageBrightness = 200 };

        var result = rule.Evaluate(metrics, thresholds);

        Assert.Equal(UnsafeReason.None, result);
    }

    [Fact]
    public void EvaluateBrightnessExactlyAtThresholdReturnsNone()
    {
        var metrics = new SkyMetrics { AverageBrightness = 200 };
        var thresholds = new ThresholdSettings { MaxAverageBrightness = 200 };

        var result = rule.Evaluate(metrics, thresholds);

        Assert.Equal(UnsafeReason.None, result);
    }
}
