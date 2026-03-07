using OpenCvSharp;
using SkyWatch.Vision.Analyzers;

namespace SkyWatch.Tests.Vision;

public sealed class EdgeDensityAnalyzerTests
{
    [Fact]
    public void AnalyzeUniformImageReturnsApproximatelyZero()
    {
        using var uniform = new Mat(100, 100, MatType.CV_8UC1, Scalar.All(128));

        double edgeDensity = EdgeDensityAnalyzer.Analyze(uniform);

        Assert.Equal(0.0, edgeDensity, precision: 2);
    }

    [Fact]
    public void AnalyzeImageWithEdgesReturnsPositiveValue()
    {
        using var image = new Mat(100, 100, MatType.CV_8UC1, Scalar.All(0));

        Cv2.Rectangle(image, new Rect(20, 20, 60, 60), Scalar.All(255), 2);
        Cv2.Rectangle(image, new Rect(10, 10, 30, 30), Scalar.All(200), 2);

        double edgeDensity = EdgeDensityAnalyzer.Analyze(image);

        Assert.True(edgeDensity > 0.0, $"Expected edge density > 0 for image with edges, got {edgeDensity}");
    }
}
