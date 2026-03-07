using OpenCvSharp;

namespace SkyWatch.Vision.Analyzers;

/// <summary>
/// Estimates cloudiness by combining star visibility ratio with contrast analysis.
/// </summary>
public static class CloudinessAnalyzer
{
    private const double StarRatioWeight = 0.7;
    private const double ContrastWeight = 0.3;
    private const double MaxExpectedStdDev = 80.0;

    /// <summary>
    /// Analyzes cloudiness based on star count relative to baseline and image contrast.
    /// </summary>
    /// <param name="gray">Grayscale input image.</param>
    /// <param name="starCount">Detected star count in the current frame.</param>
    /// <param name="baselineStarCount">Expected star count under clear conditions.</param>
    /// <returns>Cloudiness score from 0.0 (clear) to 1.0 (fully cloudy).</returns>
    public static double Analyze(Mat gray, int starCount, int baselineStarCount)
    {
        int effectiveBaseline = baselineStarCount > 0 ? baselineStarCount : 10;

        double starRatio = 1.0 - ((double)starCount / effectiveBaseline);
        starRatio = Math.Clamp(starRatio, 0.0, 1.0);

        Cv2.MeanStdDev(gray, out _, out var stddev);
        double stddevValue = stddev.Val0;

        // Lower stddev means more uniform (cloudy) image; higher stddev means more contrast (clear)
        double contrastFactor = 1.0 - Math.Clamp(stddevValue / MaxExpectedStdDev, 0.0, 1.0);

        double cloudiness = (StarRatioWeight * starRatio) + (ContrastWeight * contrastFactor);
        return Math.Clamp(cloudiness, 0.0, 1.0);
    }
}
