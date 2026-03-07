using System.Runtime.InteropServices;
using OpenCvSharp;
using SkyWatch.Vision.Analyzers;

namespace SkyWatch.Tests.Vision;

public sealed class EntropyCalculatorTests
{
    [Fact]
    public void CalculateUniformImageReturnsApproximatelyZero()
    {
        using var uniform = new Mat(100, 100, MatType.CV_8UC1, Scalar.All(128));

        double entropy = EntropyCalculator.Calculate(uniform);

        Assert.Equal(0.0, entropy, precision: 1);
    }

    [Fact]
    public void CalculateNoisyImageReturnsHighEntropy()
    {
        using var noisy = new Mat(100, 100, MatType.CV_8UC1);
        var rng = new Random(42);
        var data = new byte[100 * 100];
        rng.NextBytes(data);
        Marshal.Copy(data, 0, noisy.Data, data.Length);

        double entropy = EntropyCalculator.Calculate(noisy);

        Assert.True(entropy > 5.0, $"Expected entropy > 5.0 for noisy image, got {entropy}");
    }
}
