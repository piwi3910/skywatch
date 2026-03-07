using OpenCvSharp;

namespace SkyWatch.Vision.Analyzers;

/// <summary>
/// Computes Shannon entropy of a grayscale image via its histogram.
/// </summary>
public static class EntropyCalculator
{
    private static readonly int[] Channels = { 0 };
    private static readonly int[] HistSize = { 256 };
    private static readonly Rangef[] Ranges = { new Rangef(0, 256) };

    /// <summary>
    /// Calculates Shannon entropy of the grayscale image.
    /// </summary>
    /// <param name="gray">Grayscale input image.</param>
    /// <returns>Entropy value from 0.0 (uniform) to 8.0 (maximum information).</returns>
    public static double Calculate(Mat gray)
    {
        using var hist = new Mat();
        Cv2.CalcHist(
            new[] { gray },
            Channels,
            null,
            hist,
            1,
            HistSize,
            Ranges);

        int totalPixels = gray.Rows * gray.Cols;
        double entropy = 0.0;

        for (int i = 0; i < 256; i++)
        {
            float count = hist.At<float>(i);
            if (count <= 0)
                continue;

            double p = count / totalPixels;
            entropy -= p * Math.Log2(p);
        }

        return entropy;
    }
}
