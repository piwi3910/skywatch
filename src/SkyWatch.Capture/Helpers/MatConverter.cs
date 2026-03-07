using OpenCvSharp;
using SkyWatch.Contracts.Enums;
using SkyWatch.Contracts.Models;

namespace SkyWatch.Capture.Helpers;

/// <summary>
/// Converts between OpenCvSharp <see cref="Mat"/> and <see cref="FrameData"/>.
/// </summary>
public static class MatConverter
{
    /// <summary>
    /// Encodes a <see cref="Mat"/> to PNG bytes and wraps them in a <see cref="FrameData"/>.
    /// </summary>
    /// <param name="mat">The source image matrix.</param>
    /// <param name="sourceType">The type of frame source that produced this image.</param>
    /// <returns>A new <see cref="FrameData"/> containing the PNG-encoded bytes.</returns>
    public static FrameData MatToFrameData(Mat mat, FrameSourceType sourceType)
    {
        ArgumentNullException.ThrowIfNull(mat);

        Cv2.ImEncode(".png", mat, out var imageBytes);

        int channels = mat.Channels();
        int bitsPerChannel = mat.Depth() switch
        {
            MatType.CV_8U => 8,
            MatType.CV_16U => 16,
            _ => 8,
        };

        string pixelFormat = (channels, bitsPerChannel) switch
        {
            (1, 8) => "Mono8",
            (1, 16) => "Mono16",
            (3, 8) => "BGR24",
            (3, 16) => "BGR48",
            (4, 8) => "BGRA32",
            _ => $"Unknown{channels}ch{bitsPerChannel}bit",
        };

        return new FrameData
        {
            ImageBytes = imageBytes,
            Width = mat.Width,
            Height = mat.Height,
            Channels = channels,
            BitsPerChannel = bitsPerChannel,
            PixelFormat = pixelFormat,
            CapturedAt = DateTimeOffset.UtcNow,
            SourceType = sourceType,
        };
    }

    /// <summary>
    /// Decodes a <see cref="FrameData"/> back into an OpenCvSharp <see cref="Mat"/>.
    /// The caller is responsible for disposing the returned Mat.
    /// </summary>
    /// <param name="frame">The frame data containing PNG-encoded image bytes.</param>
    /// <returns>A new <see cref="Mat"/> decoded from the frame data.</returns>
    public static Mat FrameDataToMat(FrameData frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        var mode = frame.BitsPerChannel > 8
            ? ImreadModes.AnyDepth | ImreadModes.AnyColor
            : ImreadModes.Color;

        return Cv2.ImDecode(frame.ImageBytes, mode);
    }
}
