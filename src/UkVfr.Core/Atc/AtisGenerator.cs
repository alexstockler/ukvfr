using UkVfr.Core.Airspace;
using UkVfr.Core.Phraseology;

namespace UkVfr.Core.Atc;

/// <summary>
/// Generates ATIS (Automatic Terminal Information Service) broadcasts
/// in UK format from sim weather data.
/// </summary>
public sealed class AtisGenerator
{
    private static readonly string[] InformationLetters =
        ["Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot", "Golf",
         "Hotel", "India", "Juliet", "Kilo", "Lima", "Mike", "November",
         "Oscar", "Papa", "Quebec", "Romeo", "Sierra", "Tango", "Uniform",
         "Victor", "Whiskey", "X-ray", "Yankee", "Zulu"];

    private int _currentLetterIndex;

    /// <summary>
    /// Generate an ATIS broadcast string for the given aerodrome and weather conditions.
    /// </summary>
    public string Generate(Aerodrome aerodrome, WeatherData weather, Runway activeRunway)
    {
        var letter = InformationLetters[_currentLetterIndex % InformationLetters.Length];
        var rwySpoken = CallsignFormatter.SpokenRunway(activeRunway.Designator);
        var windDir = $"{weather.WindDirectionDeg:000}";
        var windSpeed = weather.WindSpeedKt;
        var qnh = weather.QnhHpa;

        var parts = new List<string>
        {
            $"This is {aerodrome.Name} information {letter}",
            $"Runway in use {rwySpoken}",
            FormatWind(weather),
            FormatVisibility(weather),
            FormatCloud(weather),
            $"Temperature {weather.TemperatureC:+0;-0} dewpoint {weather.DewpointC:+0;-0}",
            $"Q N H {CallsignFormatter.SpokenDigits(qnh.ToString())}",
            $"Acknowledge receipt of information {letter}"
        };

        return string.Join(". ", parts) + ".";
    }

    /// <summary>
    /// Advance to the next ATIS information letter.
    /// Call this when weather data changes significantly.
    /// </summary>
    public void AdvanceLetter()
    {
        _currentLetterIndex = (_currentLetterIndex + 1) % InformationLetters.Length;
    }

    public string CurrentLetter => InformationLetters[_currentLetterIndex % InformationLetters.Length];

    private static string FormatWind(WeatherData weather)
    {
        var dir = $"{weather.WindDirectionDeg:000}";
        var speed = weather.WindSpeedKt;

        if (speed < 1)
            return "Wind calm";

        var result = $"Wind {CallsignFormatter.SpokenDigits(dir)} degrees {speed} knots";

        if (weather.WindGustKt.HasValue && weather.WindGustKt.Value > speed + 5)
            result += $" gusting {weather.WindGustKt.Value}";

        return result;
    }

    private static string FormatVisibility(WeatherData weather)
    {
        if (weather.VisibilityMetres >= 10000)
            return "Visibility one zero kilometres or more";

        return $"Visibility {weather.VisibilityMetres} metres";
    }

    private static string FormatCloud(WeatherData weather)
    {
        if (weather.CloudLayers.Count == 0)
            return "Sky clear";

        var layers = weather.CloudLayers.Select(layer =>
        {
            var coverStr = layer.Coverage switch
            {
                CloudCoverage.Few => "few",
                CloudCoverage.Scattered => "scattered",
                CloudCoverage.Broken => "broken",
                CloudCoverage.Overcast => "overcast",
                _ => "few"
            };
            return $"{coverStr} {layer.BaseFt} feet";
        });

        return $"Cloud {string.Join(", ", layers)}";
    }
}

/// <summary>
/// Weather data from the simulator, used for ATIS generation.
/// </summary>
public sealed class WeatherData
{
    public int WindDirectionDeg { get; init; }
    public int WindSpeedKt { get; init; }
    public int? WindGustKt { get; init; }
    public int VisibilityMetres { get; init; } = 10000;
    public int TemperatureC { get; init; } = 15;
    public int DewpointC { get; init; } = 10;
    public int QnhHpa { get; init; } = 1013;
    public IReadOnlyList<CloudLayer> CloudLayers { get; init; } = [];
}

public sealed class CloudLayer
{
    public required CloudCoverage Coverage { get; init; }
    public required int BaseFt { get; init; }
}

public enum CloudCoverage
{
    Few,
    Scattered,
    Broken,
    Overcast
}
