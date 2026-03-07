namespace SkyWatch.Contracts.Models;

/// <summary>
/// Defines the shape of a mask region.
/// </summary>
public enum MaskShape
{
    /// <summary>A rectangular mask region.</summary>
    Rectangle,

    /// <summary>An elliptical mask region.</summary>
    Ellipse,
}

/// <summary>
/// Defines a mask region to include or exclude from analysis.
/// </summary>
public sealed class MaskDefinition
{
    public required string Name { get; init; }

    public required MaskShape Shape { get; init; }

    public required int X { get; init; }

    public required int Y { get; init; }

    public required int Width { get; init; }

    public required int Height { get; init; }

    public bool IsExclusion { get; init; } = true;
}
