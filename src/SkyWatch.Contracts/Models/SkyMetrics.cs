namespace SkyWatch.Contracts.Models;

public sealed class SkyMetrics
{
    public double AverageBrightness { get; init; }

    public int StarCount { get; init; }

    public double CloudinessScore { get; init; }

    public double Entropy { get; init; }

    public double EdgeDensity { get; init; }

    public double ObstructionScore { get; init; }

    public double Confidence { get; init; }
}
