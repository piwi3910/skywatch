using SkyWatch.Contracts.Enums;
using SkyWatch.Contracts.Models;
using SkyWatch.Core.Configuration;

namespace SkyWatch.Safety.Rules;

internal interface ISafetyRule
{
    UnsafeReason Evaluate(SkyMetrics metrics, ThresholdSettings thresholds);
}
