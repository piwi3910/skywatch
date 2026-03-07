namespace SkyWatch.Contracts.Enums;

/// <summary>
/// Identifies the type of frame source used for sky capture.
/// </summary>
public enum FrameSourceType
{
    /// <summary>A locally connected USB camera.</summary>
    UsbCamera,

    /// <summary>An RTSP network video stream.</summary>
    Rtsp,

    /// <summary>Replay of previously captured frames.</summary>
    Replay,

    /// <summary>A camera connected via ASCOM Alpaca REST API.</summary>
    AlpacaCamera,

    /// <summary>A camera connected via ASCOM COM (Windows only).</summary>
    AscomCamera,
}
