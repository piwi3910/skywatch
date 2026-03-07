using SkyWatch.Contracts.Models;

namespace SkyWatch.Contracts.Interfaces;

public interface IFrameAnalyzer
{
    Task<FrameAnalysisResult> AnalyzeAsync(FrameData frame, CancellationToken cancellationToken);
}
