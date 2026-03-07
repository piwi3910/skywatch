using OpenCvSharp;
using SkyWatch.Vision.Analyzers;

namespace SkyWatch.Tests.Vision;

public sealed class StarDetectorTests
{
    [Fact]
    public void DetectStarFieldFindsApproximatelyCorrectCount()
    {
        using var starField = new Mat(200, 200, MatType.CV_8UC1, Scalar.All(0));

        var positions = new[]
        {
            new Point(30, 30),
            new Point(80, 50),
            new Point(150, 40),
            new Point(60, 150),
            new Point(140, 160),
        };

        foreach (var pos in positions)
        {
            Cv2.Circle(starField, pos, 3, Scalar.All(255), -1);
        }

        int starCount = StarDetector.Detect(starField, 0.6);

        Assert.InRange(starCount, 3, 10);
    }

    [Fact]
    public void DetectBlackImageReturnsZero()
    {
        using var black = new Mat(100, 100, MatType.CV_8UC1, Scalar.All(0));

        int starCount = StarDetector.Detect(black, 0.6);

        Assert.Equal(0, starCount);
    }

    [Fact]
    public void DetectWhiteImageReturnsZero()
    {
        using var white = new Mat(100, 100, MatType.CV_8UC1, Scalar.All(255));

        int starCount = StarDetector.Detect(white, 0.6);

        Assert.Equal(0, starCount);
    }
}
