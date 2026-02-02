namespace UkVfr.Core.Airspace;

/// <summary>
/// Represents a single runway at an aerodrome.
/// </summary>
public sealed class Runway
{
    public required string Designator { get; init; }
    public required double HeadingMag { get; init; }
    public required double LengthM { get; init; }
    public CircuitDirection CircuitDirection { get; init; } = CircuitDirection.Left;
    public double CircuitHeightQfeFt { get; init; } = 1000;
}

public enum CircuitDirection
{
    Left,
    Right
}
