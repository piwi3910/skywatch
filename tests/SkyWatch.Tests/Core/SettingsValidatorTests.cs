using SkyWatch.Contracts.Models;
using SkyWatch.Core.Configuration;
using SkyWatch.Core.Validation;

namespace SkyWatch.Tests.Core;

public sealed class SettingsValidatorTests
{
    [Fact]
    public void ValidSettingsReturnsNoErrors()
    {
        var settings = new ProfileSettings();

        var errors = SettingsValidator.Validate(settings);

        Assert.Empty(errors);
    }

    [Fact]
    public void ZeroFrameIntervalReturnsError()
    {
        var settings = new ProfileSettings
        {
            Capture = new CaptureSettings { FrameIntervalSeconds = 0 },
        };

        var errors = SettingsValidator.Validate(settings);

        Assert.Contains(errors, e => e.Contains("Frame interval"));
    }

    [Fact]
    public void NegativeTargetWidthReturnsError()
    {
        var settings = new ProfileSettings
        {
            Capture = new CaptureSettings { TargetWidth = -1 },
        };

        var errors = SettingsValidator.Validate(settings);

        Assert.Contains(errors, e => e.Contains("Target width"));
    }

    [Fact]
    public void NegativeTargetHeightReturnsError()
    {
        var settings = new ProfileSettings
        {
            Capture = new CaptureSettings { TargetHeight = -1 },
        };

        var errors = SettingsValidator.Validate(settings);

        Assert.Contains(errors, e => e.Contains("Target height"));
    }

    [Fact]
    public void InvalidRotationReturnsError()
    {
        var settings = new ProfileSettings
        {
            Capture = new CaptureSettings { RotationDegrees = 45 },
        };

        var errors = SettingsValidator.Validate(settings);

        Assert.Contains(errors, e => e.Contains("Rotation"));
    }

    [Fact]
    public void BrightnessOutOfRangeReturnsError()
    {
        var settings = new ProfileSettings
        {
            Thresholds = new ThresholdSettings { MaxAverageBrightness = 300 },
        };

        var errors = SettingsValidator.Validate(settings);

        Assert.Contains(errors, e => e.Contains("brightness"));
    }

    [Fact]
    public void NegativeMinStarCountReturnsError()
    {
        var settings = new ProfileSettings
        {
            Thresholds = new ThresholdSettings { MinStarCount = -1 },
        };

        var errors = SettingsValidator.Validate(settings);

        Assert.Contains(errors, e => e.Contains("star count"));
    }

    [Fact]
    public void CloudinessOutOfRangeReturnsError()
    {
        var settings = new ProfileSettings
        {
            Thresholds = new ThresholdSettings { MaxCloudinessScore = 1.5 },
        };

        var errors = SettingsValidator.Validate(settings);

        Assert.Contains(errors, e => e.Contains("cloudiness"));
    }

    [Fact]
    public void EntropyOutOfRangeReturnsError()
    {
        var settings = new ProfileSettings
        {
            Thresholds = new ThresholdSettings { MinEntropy = 10 },
        };

        var errors = SettingsValidator.Validate(settings);

        Assert.Contains(errors, e => e.Contains("entropy"));
    }

    [Fact]
    public void NegativeDebounceReturnsError()
    {
        var settings = new ProfileSettings
        {
            Timing = new TimingSettings { UnsafeDebounceSeconds = -1 },
        };

        var errors = SettingsValidator.Validate(settings);

        Assert.Contains(errors, e => e.Contains("debounce"));
    }

    [Fact]
    public void EmptyMaskNameReturnsError()
    {
        var settings = new ProfileSettings
        {
            Masks = new List<MaskDefinition>
            {
                new()
                {
                    Name = string.Empty,
                    Shape = MaskShape.Rectangle,
                    X = 0,
                    Y = 0,
                    Width = 100,
                    Height = 100,
                },
            },
        };

        var errors = SettingsValidator.Validate(settings);

        Assert.Contains(errors, e => e.Contains("Mask name"));
    }

    [Fact]
    public void MaskWithZeroWidthReturnsError()
    {
        var settings = new ProfileSettings
        {
            Masks = new List<MaskDefinition>
            {
                new()
                {
                    Name = "test-mask",
                    Shape = MaskShape.Rectangle,
                    X = 0,
                    Y = 0,
                    Width = 0,
                    Height = 100,
                },
            },
        };

        var errors = SettingsValidator.Validate(settings);

        Assert.Contains(errors, e => e.Contains("positive width and height"));
    }

    [Fact]
    public void MultipleErrorsAllReturned()
    {
        var settings = new ProfileSettings
        {
            Capture = new CaptureSettings
            {
                FrameIntervalSeconds = 0,
                TargetWidth = -1,
                RotationDegrees = 45,
            },
            Thresholds = new ThresholdSettings
            {
                MinStarCount = -5,
            },
        };

        var errors = SettingsValidator.Validate(settings);

        Assert.True(errors.Count >= 4, $"Expected at least 4 errors but got {errors.Count}");
    }
}
