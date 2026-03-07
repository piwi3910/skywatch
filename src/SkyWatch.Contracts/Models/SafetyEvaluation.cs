using SkyWatch.Contracts.Enums;

namespace SkyWatch.Contracts.Models;

public sealed class SafetyEvaluation
{
    public required SafetyState State { get; init; }

    public required bool IsSafe { get; init; }

    public required UnsafeReason Reasons { get; init; }

    public required double Confidence { get; init; }

    public required string Explanation { get; init; }

    public required DateTimeOffset EvaluatedAt { get; init; }

    public DateTimeOffset? LastStateChange { get; init; }
}
