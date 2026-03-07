using SkyWatch.Contracts.Enums;
using SkyWatch.Core.Configuration;

namespace SkyWatch.Tests.Core;

public sealed class ConfigurationDefaultsTests
{
    [Fact]
    public void CaptureSettingsDefaults()
    {
        var capture = new CaptureSettings();

        Assert.Equal(FrameSourceType.Replay, capture.SourceType);
        Assert.Equal(string.Empty, capture.SourceIdentifier);
        Assert.Equal(0, capture.DeviceIndex);
        Assert.Equal(3, capture.FrameIntervalSeconds);
        Assert.Equal(1280, capture.TargetWidth);
        Assert.Equal(720, capture.TargetHeight);
        Assert.Equal(5, capture.ReconnectIntervalSeconds);
        Assert.Equal(15, capture.StaleFrameTimeoutSeconds);
        Assert.Equal(10, capture.ConnectionTimeoutSeconds);
        Assert.False(capture.FlipHorizontal);
        Assert.False(capture.FlipVertical);
        Assert.Equal(0, capture.RotationDegrees);
    }

    [Fact]
    public void ThresholdSettingsDefaults()
    {
        var thresholds = new ThresholdSettings();

        Assert.Equal(200.0, thresholds.MaxAverageBrightness);
        Assert.Equal(10, thresholds.MinStarCount);
        Assert.Equal(0.7, thresholds.MaxCloudinessScore);
        Assert.Equal(2.0, thresholds.MinEntropy);
        Assert.Equal(0.005, thresholds.MinEdgeDensity);
        Assert.Equal(0.8, thresholds.ObstructionThreshold);
        Assert.Equal(0.6, thresholds.StarDetectionThreshold);
        Assert.Equal(0.5, thresholds.ConfidenceThreshold);
    }

    [Fact]
    public void TimingSettingsDefaults()
    {
        var timing = new TimingSettings();

        Assert.Equal(30, timing.WarmupSeconds);
        Assert.Equal(10, timing.UnsafeDebounceSeconds);
        Assert.Equal(300, timing.SafeRecoveryDebounceSeconds);
        Assert.Equal(60, timing.MinimumUnsafeHoldSeconds);
        Assert.True(timing.FailSafeOnCameraLoss);
        Assert.True(timing.FailSafeOnStaleFrame);
    }

    [Fact]
    public void AlpacaSettingsDefaults()
    {
        var alpaca = new AlpacaSettings();

        Assert.Equal(11111, alpaca.Port);
        Assert.Equal(32227, alpaca.DiscoveryPort);
        Assert.True(alpaca.EnableDiscovery);
        Assert.Equal("SkyWatch", alpaca.ServerName);
        Assert.Equal("SkyWatch", alpaca.Manufacturer);
        Assert.Equal("SkyWatch SafetyMonitor", alpaca.DeviceName);
        Assert.False(string.IsNullOrEmpty(alpaca.UniqueId));
    }

    [Fact]
    public void SkyWatchOptionsDefaults()
    {
        var options = new SkyWatchOptions();

        Assert.Equal("default", options.ActiveProfile);
        Assert.Equal("profiles", options.ProfilesDirectory);
        Assert.Equal("logs", options.LogsDirectory);
        Assert.Equal("debug-frames", options.DebugFramesDirectory);
        Assert.True(options.SaveDebugFramesOnStateChange);
    }
}
