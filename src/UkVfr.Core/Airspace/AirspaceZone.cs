namespace UkVfr.Core.Airspace;

/// <summary>
/// Represents an airspace zone (CTR, CTA, MATZ, ATZ, Danger area, etc.)
/// with a classification and vertical limits.
/// </summary>
public sealed class AirspaceZone
{
    public required string Name { get; init; }
    public required AirspaceClass Class { get; init; }
    public required AirspaceCategory Category { get; init; }

    /// <summary>Boundary polygon as (lat, lon) pairs. Circles use a centre + radius instead.</summary>
    public IReadOnlyList<GeoPoint>? Boundary { get; init; }
    public GeoPoint? Centre { get; init; }
    public double? RadiusNm { get; init; }

    public required double LowerLimitFt { get; init; }
    public required double UpperLimitFt { get; init; }

    /// <summary>Whether limits are AMSL, AGL, or flight level.</summary>
    public AltitudeReference LowerReference { get; init; } = AltitudeReference.Amsl;
    public AltitudeReference UpperReference { get; init; } = AltitudeReference.Amsl;
}

public readonly record struct GeoPoint(double LatitudeDeg, double LongitudeDeg);

public enum AirspaceClass
{
    A, B, C, D, E, F, G
}

public enum AirspaceCategory
{
    Ctr,
    Cta,
    Matz,
    Atz,
    DangerArea,
    RestrictedArea,
    ProhibitedArea,
    Tmz,
    Rmz
}

public enum AltitudeReference
{
    Amsl,
    Agl,
    FlightLevel
}
