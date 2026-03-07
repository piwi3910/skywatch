using SkyWatch.Contracts.Models;

namespace SkyWatch.Contracts.Interfaces;

public interface ISafetyEvaluator
{
    SafetyEvaluation Evaluate(FrameAnalysisResult analysis, DateTimeOffset now);
}
