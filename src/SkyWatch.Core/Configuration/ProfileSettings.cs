using SkyWatch.Contracts.Enums;
using SkyWatch.Contracts.Models;

namespace SkyWatch.Core.Configuration;

public sealed class ProfileSettings
{
    public string Name { get; set; } = "default";

    public string Description { get; set; } = "Default profile";

    public CaptureSettings Capture { get; set; } = new();

    public ThresholdSettings Thresholds { get; set; } = new();

    public TimingSettings Timing { get; set; } = new();

    public List<MaskDefinition> Masks { get; set; } = new();

    public ManualOverrideMode ManualOverride { get; set; } = ManualOverrideMode.Automatic;
}
