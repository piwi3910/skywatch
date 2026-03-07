using Microsoft.Extensions.Logging.Abstractions;
using OpenCvSharp;
using SkyWatch.Contracts.Enums;
using SkyWatch.Contracts.Models;
using SkyWatch.Core.Configuration;
using SkyWatch.Vision;

namespace SkyWatch.Tests.Vision;

public sealed class FrameAnalyzerPipelineTests
{
    private readonly FrameAnalyzerPipeline pipeline = new(
        new ProfileSettings(),
        NullLogger<FrameAnalyzerPipeline>.Instance);

    [Fact]
    public async Task AnalyzeBlackImageReturnsLowBrightness()
    {
        using var black = new Mat(100, 100, MatType.CV_8UC3, Scalar.All(0));
        var frame = CreateFrameDataFromMat(black);

        var result = await pipeline.AnalyzeAsync(frame, CancellationToken.None);

        Assert.True(result.AnalysisSucceeded);
        Assert.True(result.Metrics.AverageBrightness < 1.0);
    }

    [Fact]
    public async Task AnalyzeWhiteImageReturnsHighBrightness()
    {
        using var white = new Mat(100, 100, MatType.CV_8UC3, Scalar.All(255));
        var frame = CreateFrameDataFromMat(white);

        var result = await pipeline.AnalyzeAsync(frame, CancellationToken.None);

        Assert.True(result.AnalysisSucceeded);
        Assert.True(result.Metrics.AverageBrightness > 254.0);
    }

    [Fact]
    public async Task AnalyzeNormalImageReturnsSuccessfulResult()
    {
        using var gray = new Mat(100, 100, MatType.CV_8UC3, Scalar.All(128));
        var frame = CreateFrameDataFromMat(gray);

        var result = await pipeline.AnalyzeAsync(frame, CancellationToken.None);

        Assert.True(result.AnalysisSucceeded);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task AnalyzeInvalidDataReturnsFailedResult()
    {
        var frame = new FrameData
        {
            ImageBytes = [0xFF, 0xFE, 0x00, 0x01],
            Width = 100,
            Height = 100,
            Channels = 3,
            BitsPerChannel = 8,
            PixelFormat = "BGR24",
            CapturedAt = DateTimeOffset.UtcNow,
            SourceType = FrameSourceType.Replay,
        };

        var result = await pipeline.AnalyzeAsync(frame, CancellationToken.None);

        Assert.False(result.AnalysisSucceeded);
        Assert.NotNull(result.ErrorMessage);
        Assert.NotEmpty(result.ErrorMessage);
    }

    [Fact]
    public async Task AnalyzeBlackImageDetectsHighObstruction()
    {
        using var black = new Mat(100, 100, MatType.CV_8UC3, Scalar.All(0));
        var frame = CreateFrameDataFromMat(black);

        var result = await pipeline.AnalyzeAsync(frame, CancellationToken.None);

        Assert.True(result.AnalysisSucceeded);
        Assert.True(result.Metrics.ObstructionScore > 0.5);
    }

    [Fact]
    public async Task AnalyzeStarFieldDetectsStars()
    {
        using var dark = new Mat(200, 200, MatType.CV_8UC3, Scalar.All(10));

        // Draw bright dots to simulate stars
        Cv2.Circle(dark, new Point(50, 50), 2, Scalar.All(255), -1);
        Cv2.Circle(dark, new Point(100, 30), 2, Scalar.All(255), -1);
        Cv2.Circle(dark, new Point(150, 80), 2, Scalar.All(255), -1);
        Cv2.Circle(dark, new Point(70, 150), 2, Scalar.All(255), -1);
        Cv2.Circle(dark, new Point(130, 170), 2, Scalar.All(255), -1);
        Cv2.Circle(dark, new Point(30, 100), 2, Scalar.All(240), -1);
        Cv2.Circle(dark, new Point(170, 120), 2, Scalar.All(230), -1);

        var frame = CreateFrameDataFromMat(dark);

        var result = await pipeline.AnalyzeAsync(frame, CancellationToken.None);

        Assert.True(result.AnalysisSucceeded);
        Assert.True(result.Metrics.StarCount > 0);
    }

    private static FrameData CreateFrameDataFromMat(Mat mat)
    {
        Cv2.ImEncode(".png", mat, out var buf);
        return new FrameData
        {
            ImageBytes = buf,
            Width = mat.Width,
            Height = mat.Height,
            Channels = mat.Channels(),
            BitsPerChannel = 8,
            PixelFormat = "BGR24",
            CapturedAt = DateTimeOffset.UtcNow,
            SourceType = FrameSourceType.Replay,
        };
    }
}
