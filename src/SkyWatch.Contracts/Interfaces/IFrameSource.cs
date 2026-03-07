using SkyWatch.Contracts.Models;

namespace SkyWatch.Contracts.Interfaces;

public interface IFrameSource : IAsyncDisposable
{
    Task StartAsync(CancellationToken cancellationToken);

    Task<FrameData?> GetLatestFrameAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);

    CaptureStatus GetStatus();
}
