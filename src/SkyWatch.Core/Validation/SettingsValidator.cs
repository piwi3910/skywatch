using SkyWatch.Core.Configuration;

namespace SkyWatch.Core.Validation;

public static class SettingsValidator
{
    public static IReadOnlyList<string> Validate(ProfileSettings settings)
    {
        var errors = new List<string>();

        // Capture settings validation
        if (settings.Capture.FrameIntervalSeconds <= 0)
            errors.Add("Frame interval must be greater than 0 seconds.");

        if (settings.Capture.TargetWidth <= 0)
            errors.Add("Target width must be greater than 0.");

        if (settings.Capture.TargetHeight <= 0)
            errors.Add("Target height must be greater than 0.");

        if (settings.Capture.ReconnectIntervalSeconds < 0)
            errors.Add("Reconnect interval cannot be negative.");

        if (settings.Capture.StaleFrameTimeoutSeconds <= 0)
            errors.Add("Stale frame timeout must be greater than 0.");

        if (settings.Capture.ConnectionTimeoutSeconds <= 0)
            errors.Add("Connection timeout must be greater than 0.");

        if (settings.Capture.RotationDegrees is not (0 or 90 or 180 or 270))
            errors.Add("Rotation must be 0, 90, 180, or 270 degrees.");

        // Threshold settings validation
        if (settings.Thresholds.MaxAverageBrightness is < 0 or > 255)
            errors.Add("Max average brightness must be between 0 and 255.");

        if (settings.Thresholds.MinStarCount < 0)
            errors.Add("Minimum star count cannot be negative.");

        if (settings.Thresholds.MaxCloudinessScore is < 0 or > 1)
            errors.Add("Max cloudiness score must be between 0 and 1.");

        if (settings.Thresholds.MinEntropy is < 0 or > 8)
            errors.Add("Min entropy must be between 0 and 8.");

        if (settings.Thresholds.MinEdgeDensity is < 0 or > 1)
            errors.Add("Min edge density must be between 0 and 1.");

        if (settings.Thresholds.ObstructionThreshold is < 0 or > 1)
            errors.Add("Obstruction threshold must be between 0 and 1.");

        if (settings.Thresholds.StarDetectionThreshold is < 0 or > 1)
            errors.Add("Star detection threshold must be between 0 and 1.");

        if (settings.Thresholds.ConfidenceThreshold is < 0 or > 1)
            errors.Add("Confidence threshold must be between 0 and 1.");

        // Timing settings validation
        if (settings.Timing.WarmupSeconds < 0)
            errors.Add("Warmup seconds cannot be negative.");

        if (settings.Timing.UnsafeDebounceSeconds < 0)
            errors.Add("Unsafe debounce seconds cannot be negative.");

        if (settings.Timing.SafeRecoveryDebounceSeconds < 0)
            errors.Add("Safe recovery debounce seconds cannot be negative.");

        if (settings.Timing.MinimumUnsafeHoldSeconds < 0)
            errors.Add("Minimum unsafe hold seconds cannot be negative.");

        // Mask validation
        foreach (var mask in settings.Masks)
        {
            if (string.IsNullOrWhiteSpace(mask.Name))
                errors.Add("Mask name cannot be empty.");

            if (mask.Width <= 0 || mask.Height <= 0)
                errors.Add($"Mask '{mask.Name}' must have positive width and height.");
        }

        return errors.AsReadOnly();
    }
}
