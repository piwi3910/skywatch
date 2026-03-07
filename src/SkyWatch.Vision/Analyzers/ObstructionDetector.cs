using OpenCvSharp;

namespace SkyWatch.Vision.Analyzers;

/// <summary>
/// Detects obstructions (lens cap, condensation, etc.) by combining multiple signals.
/// </summary>
public static class ObstructionDetector
{
    private const double LowEntropyThreshold = 2.0;
    private const double LowEdgeDensityThreshold = 0.005;
    private const double QuadrantStdDevThreshold = 5.0;
    private const double QuadrantMeanDifferenceThreshold = 30.0;

    /// <summary>
    /// Detects obstruction by combining entropy, edge density, star count, and quadrant uniformity.
    /// </summary>
    /// <param name="gray">Grayscale input image.</param>
    /// <param name="entropy">Shannon entropy of the image.</param>
    /// <param name="edgeDensity">Edge density of the image.</param>
    /// <param name="starCount">Number of detected stars.</param>
    /// <returns>Obstruction score from 0.0 (no obstruction) to 1.0 (fully obstructed).</returns>
    public static double Detect(Mat gray, double entropy, double edgeDensity, int starCount)
    {
        double score = 0.0;

        if (entropy < LowEntropyThreshold)
            score += 0.4;

        if (edgeDensity < LowEdgeDensityThreshold)
            score += 0.3;

        if (starCount == 0)
            score += 0.2;

        score += AnalyzeQuadrantUniformity(gray);

        return Math.Clamp(score, 0.0, 1.0);
    }

    private static double AnalyzeQuadrantUniformity(Mat gray)
    {
        int midRow = gray.Rows / 2;
        int midCol = gray.Cols / 2;

        using var topLeft = new Mat(gray, new Rect(0, 0, midCol, midRow));
        using var topRight = new Mat(gray, new Rect(midCol, 0, gray.Cols - midCol, midRow));
        using var bottomLeft = new Mat(gray, new Rect(0, midRow, midCol, gray.Rows - midRow));
        using var bottomRight = new Mat(gray, new Rect(midCol, midRow, gray.Cols - midCol, gray.Rows - midRow));

        var quadrants = new[] { topLeft, topRight, bottomLeft, bottomRight };
        var means = new double[4];
        var stddevs = new double[4];

        for (int i = 0; i < 4; i++)
        {
            Cv2.MeanStdDev(quadrants[i], out var mean, out var stddev);
            means[i] = mean.Val0;
            stddevs[i] = stddev.Val0;
        }

        double overallMean = means.Average();

        for (int i = 0; i < 4; i++)
        {
            if (stddevs[i] < QuadrantStdDevThreshold &&
                Math.Abs(means[i] - overallMean) > QuadrantMeanDifferenceThreshold)
            {
                return 0.1;
            }
        }

        return 0.0;
    }
}
