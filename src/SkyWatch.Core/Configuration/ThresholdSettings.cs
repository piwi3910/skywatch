namespace SkyWatch.Core.Configuration;

public sealed class ThresholdSettings
{
    public double MaxAverageBrightness { get; set; } = 200.0;

    public int MinStarCount { get; set; } = 10;

    public double MaxCloudinessScore { get; set; } = 0.7;

    public double MinEntropy { get; set; } = 2.0;

    public double MinEdgeDensity { get; set; } = 0.005;

    public double ObstructionThreshold { get; set; } = 0.8;

    public double StarDetectionThreshold { get; set; } = 0.6;

    public double ConfidenceThreshold { get; set; } = 0.5;
}
