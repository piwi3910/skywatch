using Microsoft.Extensions.Logging;
using OpenCvSharp;
using SkyWatch.Contracts.Interfaces;
using SkyWatch.Contracts.Models;
using SkyWatch.Core.Configuration;
using SkyWatch.Vision.Analyzers;
using SkyWatch.Vision.Preprocessing;

namespace SkyWatch.Vision;

/// <summary>
/// Orchestrates all image analyzers to produce a complete frame analysis result.
/// </summary>
public sealed partial class FrameAnalyzerPipeline : IFrameAnalyzer
{
    private readonly ProfileSettings profileSettings;
    private readonly ILogger<FrameAnalyzerPipeline> logger;

    public FrameAnalyzerPipeline(ProfileSettings profileSettings, ILogger<FrameAnalyzerPipeline> logger)
    {
        this.profileSettings = profileSettings;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public Task<FrameAnalysisResult> AnalyzeAsync(FrameData frame, CancellationToken cancellationToken)
    {
        try
        {
            var result = AnalyzeFrame(frame);
            LogAnalysisCompleted(
                result.Metrics.AverageBrightness,
                result.Metrics.StarCount,
                result.Metrics.CloudinessScore,
                result.Metrics.Entropy);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            LogAnalysisFailed(ex.Message);
            var failedResult = new FrameAnalysisResult
            {
                Metrics = new SkyMetrics
                {
                    AverageBrightness = 0,
                    StarCount = 0,
                    CloudinessScore = 0,
                    Entropy = 0,
                    EdgeDensity = 0,
                    ObstructionScore = 0,
                    Confidence = 0,
                },
                AnalyzedAt = DateTimeOffset.UtcNow,
                FrameCapturedAt = frame.CapturedAt,
                AnalysisSucceeded = false,
                ErrorMessage = ex.Message,
            };
            return Task.FromResult(failedResult);
        }
    }

    private static double ComputeConfidence(
        double brightness,
        int starCount,
        double cloudiness,
        double entropy,
        double edgeDensity,
        double obstruction,
        ThresholdSettings thresholds)
    {
        double brightnessConfidence = Math.Clamp(
            1.0 - (brightness / thresholds.MaxAverageBrightness), 0.0, 1.0);

        double starConfidence = Math.Clamp(
            (double)starCount / thresholds.MinStarCount, 0.0, 1.0);

        double cloudinessConfidence = Math.Clamp(
            1.0 - (cloudiness / thresholds.MaxCloudinessScore), 0.0, 1.0);

        double entropyConfidence = Math.Clamp(
            entropy / (thresholds.MinEntropy * 2.0), 0.0, 1.0);

        double edgeConfidence = Math.Clamp(
            edgeDensity / (thresholds.MinEdgeDensity * 2.0), 0.0, 1.0);

        double obstructionConfidence = Math.Clamp(
            1.0 - (obstruction / thresholds.ObstructionThreshold), 0.0, 1.0);

        double averageConfidence = (brightnessConfidence + starConfidence + cloudinessConfidence +
                                    entropyConfidence + edgeConfidence + obstructionConfidence) / 6.0;

        return Math.Clamp(averageConfidence, 0.0, 1.0);
    }

    private FrameAnalysisResult AnalyzeFrame(FrameData frame)
    {
        using var gray = FramePreprocessor.Preprocess(frame, profileSettings.Masks);

        var thresholds = profileSettings.Thresholds;

        double brightness = BrightnessAnalyzer.Analyze(gray);
        int starCount = StarDetector.Detect(gray, thresholds.StarDetectionThreshold);
        double entropy = EntropyCalculator.Calculate(gray);
        double edgeDensity = EdgeDensityAnalyzer.Analyze(gray);
        double cloudiness = CloudinessAnalyzer.Analyze(gray, starCount, thresholds.MinStarCount);
        double obstruction = ObstructionDetector.Detect(gray, entropy, edgeDensity, starCount);
        double confidence = ComputeConfidence(brightness, starCount, cloudiness, entropy, edgeDensity, obstruction, thresholds);

        return new FrameAnalysisResult
        {
            Metrics = new SkyMetrics
            {
                AverageBrightness = brightness,
                StarCount = starCount,
                CloudinessScore = cloudiness,
                Entropy = entropy,
                EdgeDensity = edgeDensity,
                ObstructionScore = obstruction,
                Confidence = confidence,
            },
            AnalyzedAt = DateTimeOffset.UtcNow,
            FrameCapturedAt = frame.CapturedAt,
            AnalysisSucceeded = true,
        };
    }

    [LoggerMessage(
        Level = Microsoft.Extensions.Logging.LogLevel.Debug,
        Message = "Frame analysis completed: Brightness={Brightness:F1}, Stars={StarCount}, Cloudiness={Cloudiness:F2}, Entropy={Entropy:F2}")]
    private partial void LogAnalysisCompleted(double brightness, int starCount, double cloudiness, double entropy);

    [LoggerMessage(
        Level = Microsoft.Extensions.Logging.LogLevel.Warning,
        Message = "Frame analysis failed: {ErrorMessage}")]
    private partial void LogAnalysisFailed(string errorMessage);
}
