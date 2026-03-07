using OpenCvSharp;
using SkyWatch.Vision.Analyzers;

namespace SkyWatch.Tests.Vision;

public sealed class CloudinessAnalyzerTests
{
    [Fact]
    public void AnalyzeHighStarCountReturnsLowCloudiness()
    {
        using var image = new Mat(100, 100, MatType.CV_8UC1, Scalar.All(10));
        for (int i = 0; i < 20; i++)
        {
            Cv2.Circle(image, new Point(5 + (i * 5), 50), 2, Scalar.All(255), -1);
        }

        double cloudiness = CloudinessAnalyzer.Analyze(image, starCount: 20, baselineStarCount: 20);

        Assert.True(cloudiness < 0.5, $"Expected low cloudiness for high star count, got {cloudiness}");
    }

    [Fact]
    public void AnalyzeZeroStarsReturnsHighCloudiness()
    {
        using var uniform = new Mat(100, 100, MatType.CV_8UC1, Scalar.All(128));

        double cloudiness = CloudinessAnalyzer.Analyze(uniform, starCount: 0, baselineStarCount: 20);

        Assert.True(cloudiness > 0.5, $"Expected high cloudiness for zero stars, got {cloudiness}");
    }
}
