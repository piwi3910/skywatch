namespace SkyWatch.Contracts.Enums;

/// <summary>
/// Specifies the manual override mode for safety evaluation.
/// </summary>
public enum ManualOverrideMode
{
    /// <summary>Safety is determined automatically by analysis.</summary>
    Automatic,

    /// <summary>Safety is forced to safe regardless of analysis.</summary>
    ForceSafe,

    /// <summary>Safety is forced to unsafe regardless of analysis.</summary>
    ForceUnsafe,
}
