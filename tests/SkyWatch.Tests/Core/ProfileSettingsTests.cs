using System.Text.Json;
using System.Text.Json.Serialization;
using SkyWatch.Contracts.Enums;
using SkyWatch.Core.Configuration;

namespace SkyWatch.Tests.Core;

public sealed class ProfileSettingsTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void DefaultValuesAreCorrect()
    {
        var settings = new ProfileSettings();

        Assert.Equal("default", settings.Name);
        Assert.Equal("Default profile", settings.Description);
        Assert.NotNull(settings.Capture);
        Assert.NotNull(settings.Thresholds);
        Assert.NotNull(settings.Timing);
        Assert.NotNull(settings.Masks);
        Assert.Empty(settings.Masks);
        Assert.Equal(ManualOverrideMode.Automatic, settings.ManualOverride);
    }

    [Fact]
    public void JsonSerializationRoundTrip()
    {
        var original = new ProfileSettings
        {
            Name = "round-trip",
            Description = "Round trip test",
            Capture = new CaptureSettings
            {
                FrameIntervalSeconds = 7,
                TargetWidth = 1920,
                TargetHeight = 1080,
                RotationDegrees = 180,
                SourceType = FrameSourceType.UsbCamera,
            },
            Thresholds = new ThresholdSettings
            {
                MaxAverageBrightness = 150.0,
                MinStarCount = 25,
                MaxCloudinessScore = 0.5,
            },
            Timing = new TimingSettings
            {
                WarmupSeconds = 45,
                UnsafeDebounceSeconds = 20,
            },
            ManualOverride = ManualOverrideMode.ForceSafe,
        };

        var json = JsonSerializer.Serialize(original, SerializerOptions);
        var deserialized = JsonSerializer.Deserialize<ProfileSettings>(json, SerializerOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(original.Name, deserialized.Name);
        Assert.Equal(original.Description, deserialized.Description);
        Assert.Equal(original.Capture.FrameIntervalSeconds, deserialized.Capture.FrameIntervalSeconds);
        Assert.Equal(original.Capture.TargetWidth, deserialized.Capture.TargetWidth);
        Assert.Equal(original.Capture.TargetHeight, deserialized.Capture.TargetHeight);
        Assert.Equal(original.Capture.RotationDegrees, deserialized.Capture.RotationDegrees);
        Assert.Equal(original.Capture.SourceType, deserialized.Capture.SourceType);
        Assert.Equal(original.Thresholds.MaxAverageBrightness, deserialized.Thresholds.MaxAverageBrightness);
        Assert.Equal(original.Thresholds.MinStarCount, deserialized.Thresholds.MinStarCount);
        Assert.Equal(original.Thresholds.MaxCloudinessScore, deserialized.Thresholds.MaxCloudinessScore);
        Assert.Equal(original.Timing.WarmupSeconds, deserialized.Timing.WarmupSeconds);
        Assert.Equal(original.Timing.UnsafeDebounceSeconds, deserialized.Timing.UnsafeDebounceSeconds);
        Assert.Equal(original.ManualOverride, deserialized.ManualOverride);
    }

    [Fact]
    public void JsonSerializationEnumsAsStrings()
    {
        var settings = new ProfileSettings
        {
            Capture = new CaptureSettings { SourceType = FrameSourceType.Rtsp },
            ManualOverride = ManualOverrideMode.ForceUnsafe,
        };

        var json = JsonSerializer.Serialize(settings, SerializerOptions);

        Assert.Contains("\"Rtsp\"", json);
        Assert.Contains("\"ForceUnsafe\"", json);
        Assert.DoesNotContain("\"2\"", json);
    }
}
