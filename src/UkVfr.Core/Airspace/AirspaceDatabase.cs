using System.Text.Json;
using System.Text.Json.Serialization;

namespace UkVfr.Core.Airspace;

/// <summary>
/// Loads and queries aerodrome and airspace data from JSON files.
/// </summary>
public sealed class AirspaceDatabase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
    };

    private readonly Dictionary<string, Aerodrome> _aerodromes = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<AirspaceZone> _zones = [];

    public IReadOnlyDictionary<string, Aerodrome> Aerodromes => _aerodromes;
    public IReadOnlyList<AirspaceZone> Zones => _zones;

    /// <summary>
    /// Loads all aerodrome JSON files from the given directory.
    /// Each file should be named {ICAO}.json.
    /// </summary>
    public async Task LoadAerodromesAsync(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return;

        foreach (var file in Directory.EnumerateFiles(directoryPath, "*.json"))
        {
            await using var stream = File.OpenRead(file);
            var aerodrome = await JsonSerializer.DeserializeAsync<Aerodrome>(stream, JsonOptions);
            if (aerodrome is not null)
            {
                _aerodromes[aerodrome.Icao] = aerodrome;
            }
        }
    }

    /// <summary>
    /// Finds the nearest aerodrome to the given position within maxRangeNm.
    /// </summary>
    public Aerodrome? FindNearest(double latDeg, double lonDeg, double maxRangeNm = 30)
    {
        Aerodrome? nearest = null;
        var bestDist = double.MaxValue;

        foreach (var ad in _aerodromes.Values)
        {
            var dist = DistanceNm(latDeg, lonDeg, ad);
            if (dist < bestDist && dist <= maxRangeNm)
            {
                bestDist = dist;
                nearest = ad;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Checks if a position is inside the ATZ of the given aerodrome.
    /// </summary>
    public bool IsInsideAtz(double latDeg, double lonDeg, double altFt, Aerodrome aerodrome)
    {
        var dist = DistanceNm(latDeg, lonDeg, aerodrome);
        var altAal = altFt - aerodrome.ElevationFt;
        return dist <= aerodrome.Atz.RadiusNm && altAal <= aerodrome.Atz.UpperLimitFtAal;
    }

    /// <summary>
    /// Finds the aerodrome whose frequency matches the given COM frequency (in Hz).
    /// </summary>
    public (Aerodrome Aerodrome, string UnitType)? FindByFrequency(double comFreqHz)
    {
        var freqMhz = comFreqHz / 1_000_000.0;

        foreach (var ad in _aerodromes.Values)
        {
            foreach (var (label, freq) in ad.Frequencies.All())
            {
                if (Math.Abs(freq - freqMhz) < 0.005) // 5 kHz tolerance
                    return (ad, label);
            }
        }

        return null;
    }

    /// <summary>
    /// Equirectangular distance approximation. Sufficient for ATZ/local distances.
    /// </summary>
    internal static double DistanceNm(double latDeg, double lonDeg, Aerodrome aerodrome)
    {
        const double NmPerDegLat = 60.0;
        var midLatRad = (latDeg + aerodrome.LatitudeDeg) / 2.0 * Math.PI / 180.0;
        var dLat = (latDeg - aerodrome.LatitudeDeg) * NmPerDegLat;
        var dLon = (lonDeg - aerodrome.LongitudeDeg) * NmPerDegLat * Math.Cos(midLatRad);
        return Math.Sqrt(dLat * dLat + dLon * dLon);
    }
}
