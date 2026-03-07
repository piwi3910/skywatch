using SkyWatch.Contracts.Enums;

namespace SkyWatch.Contracts.Models;

public sealed class FrameData
{
    public required byte[] ImageBytes { get; init; }

    public required int Width { get; init; }

    public required int Height { get; init; }

    public required int Channels { get; init; }

    public required DateTimeOffset CapturedAt { get; init; }

    public required FrameSourceType SourceType { get; init; }
}
