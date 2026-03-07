using SkyWatch.Contracts.Enums;

namespace SkyWatch.Core.Configuration;

public sealed class CaptureSettings
{
    public FrameSourceType SourceType { get; set; } = FrameSourceType.Replay;

    public string SourceIdentifier { get; set; } = string.Empty;

    public int DeviceIndex { get; set; }

    public int FrameIntervalSeconds { get; set; } = 3;

    public int TargetWidth { get; set; } = 1280;

    public int TargetHeight { get; set; } = 720;

    public int ReconnectIntervalSeconds { get; set; } = 5;

    public int StaleFrameTimeoutSeconds { get; set; } = 15;

    public int ConnectionTimeoutSeconds { get; set; } = 10;

    public bool FlipHorizontal { get; set; }

    public bool FlipVertical { get; set; }

    public int RotationDegrees { get; set; }
}
