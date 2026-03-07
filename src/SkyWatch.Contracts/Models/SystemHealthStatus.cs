namespace SkyWatch.Contracts.Models;

public sealed class SystemHealthStatus
{
    public required bool IsHealthy { get; init; }

    public required CaptureStatus CaptureStatus { get; init; }

    public required SafetyEvaluation CurrentSafety { get; init; }

    public required DateTimeOffset Uptime { get; init; }

    public required string Version { get; init; }
}
