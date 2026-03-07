using OpenCvSharp;
using SkyWatch.Capture.Helpers;
using SkyWatch.Contracts.Enums;
using SkyWatch.Contracts.Models;

namespace SkyWatch.Tests.Capture;

public sealed class MatConverterTests
{
    [Fact]
    public void MatToFrameDataConvertsCorrectly()
    {
        using var mat = new Mat(80, 120, MatType.CV_8UC3, new Scalar(100, 150, 200));

        var frameData = MatConverter.MatToFrameData(mat, FrameSourceType.UsbCamera);

        Assert.Equal(120, frameData.Width);
        Assert.Equal(80, frameData.Height);
        Assert.Equal(3, frameData.Channels);
        Assert.NotNull(frameData.ImageBytes);
        Assert.NotEmpty(frameData.ImageBytes);
    }

    [Fact]
    public void FrameDataToMatConvertsCorrectly()
    {
        using var original = new Mat(60, 90, MatType.CV_8UC3, new Scalar(50, 100, 150));
        Cv2.ImEncode(".png", original, out var pngBytes);

        var frameData = new FrameData
        {
            ImageBytes = pngBytes,
            Width = 90,
            Height = 60,
            Channels = 3,
            CapturedAt = DateTimeOffset.UtcNow,
            SourceType = FrameSourceType.Replay,
        };

        using var decoded = MatConverter.FrameDataToMat(frameData);

        Assert.Equal(90, decoded.Width);
        Assert.Equal(60, decoded.Height);
        Assert.Equal(3, decoded.Channels());
    }

    [Fact]
    public void RoundTripPreservesImage()
    {
        using var original = new Mat(50, 50, MatType.CV_8UC3, new Scalar(30, 60, 90));

        var frameData = MatConverter.MatToFrameData(original, FrameSourceType.UsbCamera);
        using var roundTripped = MatConverter.FrameDataToMat(frameData);

        Assert.Equal(original.Width, roundTripped.Width);
        Assert.Equal(original.Height, roundTripped.Height);
        Assert.Equal(original.Channels(), roundTripped.Channels());

        // PNG is lossless, so pixel values should match exactly
        using var diff = new Mat();
        Cv2.Absdiff(original, roundTripped, diff);
        var scalar = Cv2.Mean(diff);
        double maxChannelMean = Math.Max(scalar.Val0, Math.Max(scalar.Val1, scalar.Val2));
        Assert.True(maxChannelMean <= 1.0, $"Mean pixel difference was {maxChannelMean}, expected <= 1.0");
    }

    [Fact]
    public void MatToFrameDataSetsTimestamp()
    {
        var before = DateTimeOffset.UtcNow;

        using var mat = new Mat(10, 10, MatType.CV_8UC3, Scalar.All(0));
        var frameData = MatConverter.MatToFrameData(mat, FrameSourceType.Rtsp);

        var after = DateTimeOffset.UtcNow;

        Assert.InRange(frameData.CapturedAt, before, after);
    }

    [Fact]
    public void MatToFrameDataSetsSourceType()
    {
        using var mat = new Mat(10, 10, MatType.CV_8UC3, Scalar.All(0));

        var frameData = MatConverter.MatToFrameData(mat, FrameSourceType.Rtsp);

        Assert.Equal(FrameSourceType.Rtsp, frameData.SourceType);
    }
}
