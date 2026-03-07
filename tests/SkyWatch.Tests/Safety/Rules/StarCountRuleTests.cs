using SkyWatch.Contracts.Enums;
using SkyWatch.Contracts.Models;
using SkyWatch.Core.Configuration;
using SkyWatch.Safety.Rules;

namespace SkyWatch.Tests.Safety.Rules;

public sealed class StarCountRuleTests
{
    private readonly StarCountRule rule = new();

    [Fact]
    public void EvaluateLowStarCountDarkSkyReturnsStarCountTooLow()
    {
        var metrics = new SkyMetrics { StarCount = 5, AverageBrightness = 50 };
        var thresholds = new ThresholdSettings { MinStarCount = 10, MaxAverageBrightness = 200 };

        var result = rule.Evaluate(metrics, thresholds);

        Assert.Equal(UnsafeReason.StarCountTooLow, result);
    }

    [Fact]
    public void EvaluateLowStarCountBrightSkyReturnsNone()
    {
        var metrics = new SkyMetrics { StarCount = 5, AverageBrightness = 220 };
        var thresholds = new ThresholdSettings { MinStarCount = 10, MaxAverageBrightness = 200 };

        var result = rule.Evaluate(metrics, thresholds);

        Assert.Equal(UnsafeReason.None, result);
    }

    [Fact]
    public void EvaluateSufficientStarCountReturnsNone()
    {
        var metrics = new SkyMetrics { StarCount = 15, AverageBrightness = 50 };
        var thresholds = new ThresholdSettings { MinStarCount = 10, MaxAverageBrightness = 200 };

        var result = rule.Evaluate(metrics, thresholds);

        Assert.Equal(UnsafeReason.None, result);
    }
}
