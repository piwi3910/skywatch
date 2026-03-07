using SkyWatch.Contracts.Enums;
using SkyWatch.Contracts.Models;
using SkyWatch.Core.Configuration;

namespace SkyWatch.Safety.Rules;

internal sealed class CloudinessRule : ISafetyRule
{
    public UnsafeReason Evaluate(SkyMetrics metrics, ThresholdSettings thresholds)
    {
        return metrics.CloudinessScore > thresholds.MaxCloudinessScore
            ? UnsafeReason.CloudCoverHigh
            : UnsafeReason.None;
    }
}
