using OpenCvSharp;
using SkyWatch.Vision.Analyzers;

namespace SkyWatch.Tests.Vision;

public sealed class BrightnessAnalyzerTests
{
    [Fact]
    public void AnalyzeBlackImageReturnsApproximatelyZero()
    {
        using var black = new Mat(100, 100, MatType.CV_8UC1, Scalar.All(0));

        double brightness = BrightnessAnalyzer.Analyze(black);

        Assert.Equal(0.0, brightness, precision: 1);
    }

    [Fact]
    public void AnalyzeWhiteImageReturnsApproximately255()
    {
        using var white = new Mat(100, 100, MatType.CV_8UC1, Scalar.All(255));

        double brightness = BrightnessAnalyzer.Analyze(white);

        Assert.Equal(255.0, brightness, precision: 1);
    }

    [Fact]
    public void AnalyzeGrayImageReturnsApproximately128()
    {
        using var gray = new Mat(100, 100, MatType.CV_8UC1, Scalar.All(128));

        double brightness = BrightnessAnalyzer.Analyze(gray);

        Assert.Equal(128.0, brightness, precision: 1);
    }
}
