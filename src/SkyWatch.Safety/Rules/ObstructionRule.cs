using SkyWatch.Contracts.Enums;
using SkyWatch.Contracts.Models;
using SkyWatch.Core.Configuration;

namespace SkyWatch.Safety.Rules;

internal sealed class ObstructionRule : ISafetyRule
{
    public UnsafeReason Evaluate(SkyMetrics metrics, ThresholdSettings thresholds)
    {
        return metrics.ObstructionScore > thresholds.ObstructionThreshold
            ? UnsafeReason.ObstructionDetected
            : UnsafeReason.None;
    }
}
