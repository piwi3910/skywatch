using SkyWatch.Contracts.Enums;

namespace SkyWatch.Web.Models;

/// <summary>
/// Request model for updating the manual override mode.
/// </summary>
public sealed class ManualOverrideRequest
{
    /// <summary>
    /// Gets or sets the manual override mode.
    /// </summary>
    public ManualOverrideMode Mode { get; set; }
}
