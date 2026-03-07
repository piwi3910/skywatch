namespace SkyWatch.Contracts.Enums;

/// <summary>
/// Flags indicating the reasons why the sky is considered unsafe.
/// </summary>
[Flags]
public enum UnsafeReason
{
    /// <summary>No unsafe conditions detected.</summary>
    None = 0,

    /// <summary>Cloud cover exceeds the safe threshold.</summary>
    CloudCoverHigh = 1 << 0,

    /// <summary>Sky brightness is too high for observations.</summary>
    BrightnessTooHigh = 1 << 1,

    /// <summary>Detected star count is below the expected threshold.</summary>
    StarCountTooLow = 1 << 2,

    /// <summary>An obstruction was detected in the camera field of view.</summary>
    ObstructionDetected = 1 << 3,

    /// <summary>Condensation was detected on the camera lens.</summary>
    LensCondensationDetected = 1 << 4,

    /// <summary>Rain is suspected based on image analysis.</summary>
    RainSuspected = 1 << 5,

    /// <summary>The camera is disconnected or unavailable.</summary>
    CameraDisconnected = 1 << 6,

    /// <summary>Frame analysis failed to complete.</summary>
    AnalysisFailed = 1 << 7,

    /// <summary>A manual override has forced the state to unsafe.</summary>
    ManualUnsafe = 1 << 8,
}
