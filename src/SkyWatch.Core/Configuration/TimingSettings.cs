namespace SkyWatch.Core.Configuration;

public sealed class TimingSettings
{
    public int WarmupSeconds { get; set; } = 30;

    public int UnsafeDebounceSeconds { get; set; } = 10;

    public int SafeRecoveryDebounceSeconds { get; set; } = 300;

    public int MinimumUnsafeHoldSeconds { get; set; } = 60;

    public bool FailSafeOnCameraLoss { get; set; } = true;

    public bool FailSafeOnStaleFrame { get; set; } = true;
}
