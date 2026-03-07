using OpenCvSharp;

namespace SkyWatch.Vision.Analyzers;

/// <summary>
/// Detects bright point sources (stars) in a grayscale image using template matching.
/// </summary>
public static class StarDetector
{
    private const int TemplateSize = 15;
    private const int StarRadius = 3;
    private const double GaussianSigma = 1.5;
    private const int DeduplicationDistance = 10;

    /// <summary>
    /// Detects stars in a grayscale image using template matching.
    /// </summary>
    /// <param name="gray">Grayscale input image.</param>
    /// <param name="detectionThreshold">Normalized correlation threshold (0.0-1.0).</param>
    /// <returns>Number of detected stars.</returns>
    public static int Detect(Mat gray, double detectionThreshold)
    {
        if (gray.Empty() || gray.Rows < TemplateSize || gray.Cols < TemplateSize)
            return 0;

        using var template = CreateStarTemplate();
        using var result = new Mat();

        Cv2.MatchTemplate(gray, template, result, TemplateMatchModes.CCoeffNormed);

        using var thresholded = new Mat();
        Cv2.Threshold(result, thresholded, detectionThreshold, 1.0, ThresholdTypes.Binary);

        // Convert to byte for finding contours/nonzero
        using var mask = new Mat();
        thresholded.ConvertTo(mask, MatType.CV_8UC1, 255.0);

        var locations = new List<Point>();
        for (int y = 0; y < mask.Rows; y++)
        {
            for (int x = 0; x < mask.Cols; x++)
            {
                if (mask.At<byte>(y, x) > 0)
                {
                    locations.Add(new Point(x, y));
                }
            }
        }

        return DeduplicatePoints(locations);
    }

    private static Mat CreateStarTemplate()
    {
        var template = new Mat(TemplateSize, TemplateSize, MatType.CV_8UC1, Scalar.All(0));
        int center = TemplateSize / 2;
        Cv2.Circle(template, new Point(center, center), StarRadius, Scalar.All(255), -1);
        Cv2.GaussianBlur(template, template, new Size(5, 5), GaussianSigma);
        return template;
    }

    private static int DeduplicatePoints(List<Point> points)
    {
        var kept = new List<Point>();
        int distSq = DeduplicationDistance * DeduplicationDistance;

        foreach (var pt in points)
        {
            bool tooClose = false;
            foreach (var existing in kept)
            {
                int dx = pt.X - existing.X;
                int dy = pt.Y - existing.Y;
                if ((dx * dx) + (dy * dy) < distSq)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
                kept.Add(pt);
        }

        return kept.Count;
    }
}
