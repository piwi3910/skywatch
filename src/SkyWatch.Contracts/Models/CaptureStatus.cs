using SkyWatch.Contracts.Enums;

namespace SkyWatch.Contracts.Models;

public sealed class CaptureStatus
{
    public required bool IsConnected { get; init; }

    public required FrameSourceType SourceType { get; init; }

    public required string SourceDescription { get; init; }

    public DateTimeOffset? LastFrameAt { get; init; }

    public int FramesCaptured { get; init; }

    public string? ErrorMessage { get; init; }

    public double? CurrentExposureDuration { get; init; }

    public bool AutoExposureEnabled { get; init; }

    public double? AutoExposureTarget { get; init; }
}
