using UkVfr.Core.Airspace;

namespace UkVfr.Core.Tracking;

/// <summary>
/// Calculates distances and bearings between aircraft and aerodromes/airspace.
/// </summary>
public static class ProximityCalculator
{
    private const double NmPerDegLat = 60.0;

    /// <summary>
    /// Returns the distance in nautical miles between two geographic points.
    /// Uses equirectangular approximation (accurate enough for UK-scale distances).
    /// </summary>
    public static double DistanceNm(double lat1, double lon1, double lat2, double lon2)
    {
        var midLatRad = (lat1 + lat2) / 2.0 * Math.PI / 180.0;
        var dLat = (lat1 - lat2) * NmPerDegLat;
        var dLon = (lon1 - lon2) * NmPerDegLat * Math.Cos(midLatRad);
        return Math.Sqrt(dLat * dLat + dLon * dLon);
    }

    /// <summary>
    /// Returns the bearing in degrees from point 1 to point 2.
    /// </summary>
    public static double BearingDeg(double lat1, double lon1, double lat2, double lon2)
    {
        var lat1Rad = lat1 * Math.PI / 180.0;
        var lat2Rad = lat2 * Math.PI / 180.0;
        var dLonRad = (lon2 - lon1) * Math.PI / 180.0;

        var y = Math.Sin(dLonRad) * Math.Cos(lat2Rad);
        var x = Math.Cos(lat1Rad) * Math.Sin(lat2Rad) -
                Math.Sin(lat1Rad) * Math.Cos(lat2Rad) * Math.Cos(dLonRad);

        var bearing = Math.Atan2(y, x) * 180.0 / Math.PI;
        return (bearing + 360) % 360;
    }

    /// <summary>
    /// Converts a bearing to a clock position (1–12) relative to the aircraft heading.
    /// E.g. traffic at bearing 090 when aircraft heading is 000 → "3 o'clock".
    /// </summary>
    public static int ToClockPosition(double aircraftHeading, double bearingToTraffic)
    {
        var relative = ((bearingToTraffic - aircraftHeading) + 360) % 360;
        var clock = (int)Math.Round(relative / 30.0);
        return clock == 0 ? 12 : clock;
    }
}
