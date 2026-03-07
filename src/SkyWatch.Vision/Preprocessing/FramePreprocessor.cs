using OpenCvSharp;
using SkyWatch.Contracts.Models;

namespace SkyWatch.Vision.Preprocessing;

/// <summary>
/// Prepares frames for analysis by decoding, converting to grayscale, and applying masks.
/// </summary>
public static class FramePreprocessor
{
    /// <summary>
    /// Preprocesses a frame for analysis. Caller is responsible for disposing the returned Mat.
    /// </summary>
    /// <param name="frame">The frame data to preprocess.</param>
    /// <param name="masks">Mask definitions to apply.</param>
    /// <returns>A grayscale Mat with masks applied.</returns>
    public static Mat Preprocess(FrameData frame, IReadOnlyList<MaskDefinition> masks)
    {
        var readMode = frame.BitsPerChannel > 8
            ? ImreadModes.AnyDepth | ImreadModes.AnyColor
            : ImreadModes.Color;

        using var decoded = Cv2.ImDecode(frame.ImageBytes, readMode);
        if (decoded.Empty())
        {
            throw new InvalidOperationException("Failed to decode image from frame bytes.");
        }

        Mat gray;
        if (decoded.Channels() > 1)
        {
            gray = new Mat();
            Cv2.CvtColor(decoded, gray, ColorConversionCodes.BGR2GRAY);
        }
        else
        {
            gray = decoded.Clone();
        }

        // Scale 16-bit to 8-bit with auto-stretch for analysis
        if (gray.Depth() != MatType.CV_8U)
        {
            using var highDepth = gray;
            gray = new Mat();
            Cv2.MinMaxLoc(highDepth, out double minVal, out double maxVal);
            double scale = maxVal > minVal ? 255.0 / (maxVal - minVal) : 1.0;
            double offset = -minVal * scale;
            highDepth.ConvertTo(gray, MatType.CV_8UC1, scale, offset);
        }

        foreach (var mask in masks)
        {
            if (!mask.IsExclusion)
                continue;

            switch (mask.Shape)
            {
                case MaskShape.Rectangle:
                    Cv2.Rectangle(
                        gray,
                        new Rect(mask.X, mask.Y, mask.Width, mask.Height),
                        Scalar.All(0),
                        -1);
                    break;

                case MaskShape.Ellipse:
                    Cv2.Ellipse(
                        gray,
                        new Point(mask.X + (mask.Width / 2), mask.Y + (mask.Height / 2)),
                        new Size(mask.Width / 2, mask.Height / 2),
                        0,
                        0,
                        360,
                        Scalar.All(0),
                        -1);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(masks), mask.Shape, "Unsupported mask shape.");
            }
        }

        return gray;
    }
}
