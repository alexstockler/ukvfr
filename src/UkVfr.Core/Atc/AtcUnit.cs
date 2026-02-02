using UkVfr.Core.Airspace;

namespace UkVfr.Core.Atc;

/// <summary>
/// Represents a single ATC unit (e.g. "Farnborough Tower") with its
/// frequency, callsign, and available services.
/// </summary>
public sealed class AtcUnit
{
    public required string Callsign { get; init; }
    public required AtcUnitType Type { get; init; }
    public required double FrequencyMhz { get; init; }
    public required IReadOnlyList<ServiceType> ServicesAvailable { get; init; }
    public required Aerodrome Aerodrome { get; init; }
}

public enum AtcUnitType
{
    Tower,
    Approach,
    Radar,
    Radio,
    Information
}
