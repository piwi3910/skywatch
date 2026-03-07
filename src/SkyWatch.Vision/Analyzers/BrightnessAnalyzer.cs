using OpenCvSharp;

namespace SkyWatch.Vision.Analyzers;

/// <summary>
/// Computes the average brightness of a grayscale image.
/// </summary>
public static class BrightnessAnalyzer
{
    /// <summary>
    /// Analyzes the average brightness of a grayscale Mat.
    /// </summary>
    /// <param name="gray">Grayscale input image.</param>
    /// <returns>Average brightness value (0-255).</returns>
    public static double Analyze(Mat gray)
    {
        return Cv2.Mean(gray).Val0;
    }
}
