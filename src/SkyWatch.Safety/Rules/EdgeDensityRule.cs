using SkyWatch.Contracts.Enums;
using SkyWatch.Contracts.Models;
using SkyWatch.Core.Configuration;

namespace SkyWatch.Safety.Rules;

internal sealed class EdgeDensityRule : ISafetyRule
{
    public UnsafeReason Evaluate(SkyMetrics metrics, ThresholdSettings thresholds)
    {
        // Very low edge density indicates condensation or fog on the lens
        return metrics.EdgeDensity < thresholds.MinEdgeDensity
            ? UnsafeReason.LensCondensationDetected
            : UnsafeReason.None;
    }
}
