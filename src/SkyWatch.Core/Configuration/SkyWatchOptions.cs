namespace SkyWatch.Core.Configuration;

public sealed class SkyWatchOptions
{
    public const string SectionName = "SkyWatch";

    public string ActiveProfile { get; set; } = "default";

    public string ProfilesDirectory { get; set; } = "profiles";

    public string LogsDirectory { get; set; } = "logs";

    public string DebugFramesDirectory { get; set; } = "debug-frames";

    public bool SaveDebugFramesOnStateChange { get; set; } = true;
}
