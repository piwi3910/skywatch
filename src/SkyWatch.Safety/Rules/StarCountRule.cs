using SkyWatch.Contracts.Enums;
using SkyWatch.Contracts.Models;
using SkyWatch.Core.Configuration;

namespace SkyWatch.Safety.Rules;

internal sealed class StarCountRule : ISafetyRule
{
    public UnsafeReason Evaluate(SkyMetrics metrics, ThresholdSettings thresholds)
    {
        // Stars aren't visible in daytime; only apply star count check when dark
        if (metrics.AverageBrightness > thresholds.MaxAverageBrightness)
            return UnsafeReason.None;

        return metrics.StarCount < thresholds.MinStarCount
            ? UnsafeReason.StarCountTooLow
            : UnsafeReason.None;
    }
}
