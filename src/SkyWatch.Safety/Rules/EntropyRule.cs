using SkyWatch.Contracts.Enums;
using SkyWatch.Contracts.Models;
using SkyWatch.Core.Configuration;

namespace SkyWatch.Safety.Rules;

internal sealed class EntropyRule : ISafetyRule
{
    public UnsafeReason Evaluate(SkyMetrics metrics, ThresholdSettings thresholds)
    {
        // Very low entropy indicates a blocked lens
        return metrics.Entropy < thresholds.MinEntropy
            ? UnsafeReason.ObstructionDetected
            : UnsafeReason.None;
    }
}
