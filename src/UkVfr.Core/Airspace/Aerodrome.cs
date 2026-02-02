namespace UkVfr.Core.Airspace;

/// <summary>
/// Represents a UK aerodrome with its frequencies, runways, and ATC services.
/// Deserialised from JSON data files in data/aerodromes/.
/// </summary>
public sealed class Aerodrome
{
    public required string Icao { get; init; }
    public required string Name { get; init; }
    public required double LatitudeDeg { get; init; }
    public required double LongitudeDeg { get; init; }
    public required double ElevationFt { get; init; }
    public double TransitionAltitudeFt { get; init; } = 3000;

    public required AerodromeFrequencies Frequencies { get; init; }
    public required IReadOnlyList<Runway> Runways { get; init; }
    public required AirTrafficZone Atz { get; init; }
    public required UnitCallsigns UnitCallsigns { get; init; }
    public required IReadOnlyList<ServiceType> ServicesAvailable { get; init; }

    public string? Notes { get; init; }

    /// <summary>
    /// Returns the runway whose frequency best matches the given COM frequency, or null.
    /// </summary>
    public Runway? ActiveRunway { get; set; }
}

public sealed class AerodromeFrequencies
{
    public double? Tower { get; init; }
    public double? Approach { get; init; }
    public double? Radar { get; init; }
    public double? Atis { get; init; }
    public double? Radio { get; init; }

    /// <summary>
    /// Returns all non-null frequencies as (label, MHz) pairs.
    /// </summary>
    public IEnumerable<(string Label, double FreqMhz)> All()
    {
        if (Tower.HasValue) yield return ("Tower", Tower.Value);
        if (Approach.HasValue) yield return ("Approach", Approach.Value);
        if (Radar.HasValue) yield return ("Radar", Radar.Value);
        if (Atis.HasValue) yield return ("ATIS", Atis.Value);
        if (Radio.HasValue) yield return ("Radio", Radio.Value);
    }
}

public sealed class UnitCallsigns
{
    public string? Tower { get; init; }
    public string? Approach { get; init; }
    public string? Radar { get; init; }
    public string? Radio { get; init; }
}

public sealed class AirTrafficZone
{
    public required string Type { get; init; } // "circle" or "polygon"
    public double RadiusNm { get; init; }
    public double UpperLimitFtAal { get; init; }
}

public enum ServiceType
{
    Basic,
    Traffic,
    Deconfliction,
    Procedural
}
