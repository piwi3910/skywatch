using SkyWatch.Contracts.Enums;
using SkyWatch.Contracts.Models;
using SkyWatch.Core.Configuration;
using SkyWatch.Safety.Rules;

namespace SkyWatch.Tests.Safety.Rules;

public sealed class CloudinessRuleTests
{
    private readonly CloudinessRule rule = new();

    [Fact]
    public void EvaluateCloudinessAboveThresholdReturnsCloudCoverHigh()
    {
        var metrics = new SkyMetrics { CloudinessScore = 0.8 };
        var thresholds = new ThresholdSettings { MaxCloudinessScore = 0.7 };

        var result = rule.Evaluate(metrics, thresholds);

        Assert.Equal(UnsafeReason.CloudCoverHigh, result);
    }

    [Fact]
    public void EvaluateCloudinessBelowThresholdReturnsNone()
    {
        var metrics = new SkyMetrics { CloudinessScore = 0.5 };
        var thresholds = new ThresholdSettings { MaxCloudinessScore = 0.7 };

        var result = rule.Evaluate(metrics, thresholds);

        Assert.Equal(UnsafeReason.None, result);
    }
}
