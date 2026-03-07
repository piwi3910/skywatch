using OpenCvSharp;

namespace SkyWatch.Vision.Analyzers;

/// <summary>
/// Computes edge density of a grayscale image using Canny edge detection.
/// </summary>
public static class EdgeDensityAnalyzer
{
    /// <summary>
    /// Analyzes edge density by applying Canny edge detection.
    /// </summary>
    /// <param name="gray">Grayscale input image.</param>
    /// <returns>Edge density from 0.0 to 1.0.</returns>
    public static double Analyze(Mat gray)
    {
        using var blurred = new Mat();
        Cv2.GaussianBlur(gray, blurred, new Size(3, 3), 0);

        using var edges = new Mat();
        Cv2.Canny(blurred, edges, 50, 200);

        return Cv2.CountNonZero(edges) / (double)(edges.Rows * edges.Cols);
    }
}
