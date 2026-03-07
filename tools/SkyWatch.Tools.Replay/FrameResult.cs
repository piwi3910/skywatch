using SkyWatch.Contracts.Enums;
using SkyWatch.Contracts.Models;

namespace SkyWatch.Tools.Replay;

/// <summary>
/// Result of analyzing and evaluating a single frame.
/// </summary>
public sealed class FrameResult
{
    /// <summary>Gets the sequential frame number.</summary>
    public required int FrameNumber { get; init; }

    /// <summary>Gets the source image file name.</summary>
    public required string FileName { get; init; }

    /// <summary>Gets the sky metrics from analysis.</summary>
    public required SkyMetrics Metrics { get; init; }

    /// <summary>Gets the safety state determined by evaluation.</summary>
    public required SafetyState State { get; init; }

    /// <summary>Gets the unsafe reason flags.</summary>
    public required UnsafeReason Reasons { get; init; }

    /// <summary>Gets the human-readable explanation of the evaluation.</summary>
    public required string Explanation { get; init; }
}
