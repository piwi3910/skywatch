using OpenCvSharp;
using SkyWatch.Vision.Analyzers;

namespace SkyWatch.Tests.Vision;

public sealed class ObstructionDetectorTests
{
    [Fact]
    public void DetectNormalImageReturnsLowObstructionScore()
    {
        using var image = new Mat(100, 100, MatType.CV_8UC1, Scalar.All(0));
        Cv2.Rectangle(image, new Rect(10, 10, 30, 30), Scalar.All(200), 2);
        Cv2.Circle(image, new Point(60, 60), 3, Scalar.All(255), -1);
        Cv2.Circle(image, new Point(80, 30), 3, Scalar.All(255), -1);

        double entropy = EntropyCalculator.Calculate(image);
        double edgeDensity = EdgeDensityAnalyzer.Analyze(image);

        double obstruction = ObstructionDetector.Detect(image, entropy, edgeDensity, starCount: 5);

        Assert.True(obstruction < 0.5, $"Expected low obstruction score for normal image, got {obstruction}");
    }

    [Fact]
    public void DetectUniformDarkImageReturnsHighObstructionScore()
    {
        using var dark = new Mat(100, 100, MatType.CV_8UC1, Scalar.All(5));

        double entropy = EntropyCalculator.Calculate(dark);
        double edgeDensity = EdgeDensityAnalyzer.Analyze(dark);

        double obstruction = ObstructionDetector.Detect(dark, entropy, edgeDensity, starCount: 0);

        Assert.True(obstruction >= 0.8, $"Expected high obstruction score for uniform dark image, got {obstruction}");
    }
}
