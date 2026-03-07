namespace SkyWatch.Tools.Replay;

/// <summary>
/// Summary report produced by the replay runner.
/// </summary>
public sealed class ReplayReport
{
    /// <summary>Gets the source directory that was replayed.</summary>
    public required string SourceDirectory { get; init; }

    /// <summary>Gets the profile name used for analysis.</summary>
    public required string ProfileName { get; init; }

    /// <summary>Gets the total number of frames processed.</summary>
    public required int TotalFrames { get; init; }

    /// <summary>Gets the number of frames evaluated as safe.</summary>
    public required int SafeFrames { get; init; }

    /// <summary>Gets the number of frames evaluated as unsafe.</summary>
    public required int UnsafeFrames { get; init; }

    /// <summary>Gets the number of frames that produced errors.</summary>
    public required int ErrorFrames { get; init; }

    /// <summary>Gets the per-frame results.</summary>
    public required List<FrameResult> Frames { get; init; }

    /// <summary>Gets the time when replay started.</summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>Gets the time when replay completed.</summary>
    public required DateTimeOffset CompletedAt { get; init; }
}
