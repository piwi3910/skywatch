namespace SkyWatch.Contracts.Enums;

/// <summary>
/// Represents the overall safety state of the sky for observatory operations.
/// </summary>
public enum SafetyState
{
    /// <summary>System is starting up and has not yet determined safety.</summary>
    Initializing,

    /// <summary>Sky conditions are safe for observations.</summary>
    Safe,

    /// <summary>Sky conditions are unsafe for observations.</summary>
    Unsafe,

    /// <summary>Safety state could not be determined.</summary>
    Unknown,

    /// <summary>Camera is disconnected or not functioning.</summary>
    CameraError,

    /// <summary>Camera view is obstructed.</summary>
    Obstructed,
}
