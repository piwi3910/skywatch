namespace SkyWatch.Contracts.Models;

public sealed class FrameAnalysisResult
{
    public required SkyMetrics Metrics { get; init; }

    public required DateTimeOffset AnalyzedAt { get; init; }

    public required DateTimeOffset FrameCapturedAt { get; init; }

    public required bool AnalysisSucceeded { get; init; }

    public string? ErrorMessage { get; init; }
}
