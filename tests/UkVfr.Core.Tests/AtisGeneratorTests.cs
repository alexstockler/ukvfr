using UkVfr.Core.Airspace;
using UkVfr.Core.Atc;
using Xunit;

namespace UkVfr.Core.Tests;

public class AtisGeneratorTests
{
    private readonly AtisGenerator _generator = new();
    private readonly Aerodrome _aerodrome;
    private readonly Runway _runway;

    public AtisGeneratorTests()
    {
        _runway = new Runway { Designator = "24", HeadingMag = 243, LengthM = 2440 };
        _aerodrome = new Aerodrome
        {
            Icao = "EGLF",
            Name = "Farnborough",
            LatitudeDeg = 51.2758,
            LongitudeDeg = -0.7764,
            ElevationFt = 238,
            Frequencies = new AerodromeFrequencies { Tower = 122.5 },
            Runways = [_runway],
            Atz = new AirTrafficZone { Type = "circle", RadiusNm = 2.5, UpperLimitFtAal = 2000 },
            UnitCallsigns = new UnitCallsigns { Tower = "Farnborough Tower" },
            ServicesAvailable = [ServiceType.Basic],
            ActiveRunway = null
        };
        _aerodrome.ActiveRunway = _runway;
    }

    [Fact]
    public void Generate_ContainsAerodromeName()
    {
        var weather = new WeatherData { WindDirectionDeg = 240, WindSpeedKt = 10, QnhHpa = 1013 };
        var result = _generator.Generate(_aerodrome, weather, _runway);
        Assert.Contains("Farnborough", result);
    }

    [Fact]
    public void Generate_ContainsInformationLetter()
    {
        var weather = new WeatherData { WindDirectionDeg = 240, WindSpeedKt = 10, QnhHpa = 1013 };
        var result = _generator.Generate(_aerodrome, weather, _runway);
        Assert.Contains("information Alpha", result);
    }

    [Fact]
    public void Generate_ContainsRunway()
    {
        var weather = new WeatherData { WindDirectionDeg = 240, WindSpeedKt = 10, QnhHpa = 1013 };
        var result = _generator.Generate(_aerodrome, weather, _runway);
        Assert.Contains("Runway in use", result);
    }

    [Fact]
    public void Generate_ContainsQnh()
    {
        var weather = new WeatherData { WindDirectionDeg = 240, WindSpeedKt = 10, QnhHpa = 1013 };
        var result = _generator.Generate(_aerodrome, weather, _runway);
        Assert.Contains("Q N H", result);
    }

    [Fact]
    public void Generate_CalmWind_SaysWindCalm()
    {
        var weather = new WeatherData { WindDirectionDeg = 0, WindSpeedKt = 0, QnhHpa = 1013 };
        var result = _generator.Generate(_aerodrome, weather, _runway);
        Assert.Contains("Wind calm", result);
    }

    [Fact]
    public void Generate_WindWithGusts_IncludesGusting()
    {
        var weather = new WeatherData
        {
            WindDirectionDeg = 270,
            WindSpeedKt = 15,
            WindGustKt = 25,
            QnhHpa = 1013
        };
        var result = _generator.Generate(_aerodrome, weather, _runway);
        Assert.Contains("gusting 25", result);
    }

    [Fact]
    public void Generate_WindWithSmallGusts_OmitsGusting()
    {
        var weather = new WeatherData
        {
            WindDirectionDeg = 270,
            WindSpeedKt = 15,
            WindGustKt = 18, // Only 3kt above sustained, threshold is >5
            QnhHpa = 1013
        };
        var result = _generator.Generate(_aerodrome, weather, _runway);
        Assert.DoesNotContain("gusting", result);
    }

    [Fact]
    public void Generate_HighVisibility_SaysTenKmOrMore()
    {
        var weather = new WeatherData { WindSpeedKt = 10, VisibilityMetres = 10000, QnhHpa = 1013 };
        var result = _generator.Generate(_aerodrome, weather, _runway);
        Assert.Contains("one zero kilometres or more", result);
    }

    [Fact]
    public void Generate_LowVisibility_ShowsMetres()
    {
        var weather = new WeatherData { WindSpeedKt = 10, VisibilityMetres = 3000, QnhHpa = 1013 };
        var result = _generator.Generate(_aerodrome, weather, _runway);
        Assert.Contains("3000 metres", result);
    }

    [Fact]
    public void Generate_NoClouds_SaysSkyClear()
    {
        var weather = new WeatherData { WindSpeedKt = 10, QnhHpa = 1013, CloudLayers = [] };
        var result = _generator.Generate(_aerodrome, weather, _runway);
        Assert.Contains("Sky clear", result);
    }

    [Fact]
    public void Generate_WithCloudLayers_DescribesLayers()
    {
        var weather = new WeatherData
        {
            WindSpeedKt = 10,
            QnhHpa = 1013,
            CloudLayers = [
                new CloudLayer { Coverage = CloudCoverage.Scattered, BaseFt = 2500 },
                new CloudLayer { Coverage = CloudCoverage.Broken, BaseFt = 5000 }
            ]
        };
        var result = _generator.Generate(_aerodrome, weather, _runway);
        Assert.Contains("scattered 2500 feet", result);
        Assert.Contains("broken 5000 feet", result);
    }

    [Fact]
    public void Generate_ContainsTemperatureAndDewpoint()
    {
        var weather = new WeatherData { WindSpeedKt = 10, TemperatureC = 18, DewpointC = 12, QnhHpa = 1013 };
        var result = _generator.Generate(_aerodrome, weather, _runway);
        Assert.Contains("Temperature", result);
        Assert.Contains("dewpoint", result);
    }

    [Fact]
    public void Generate_EndsWithAcknowledgeReceipt()
    {
        var weather = new WeatherData { WindSpeedKt = 10, QnhHpa = 1013 };
        var result = _generator.Generate(_aerodrome, weather, _runway);
        Assert.Contains("Acknowledge receipt of information Alpha", result);
        Assert.EndsWith(".", result);
    }

    [Fact]
    public void AdvanceLetter_MovesToNextLetter()
    {
        Assert.Equal("Alpha", _generator.CurrentLetter);
        _generator.AdvanceLetter();
        Assert.Equal("Bravo", _generator.CurrentLetter);
        _generator.AdvanceLetter();
        Assert.Equal("Charlie", _generator.CurrentLetter);
    }

    [Fact]
    public void AdvanceLetter_WrapsAroundAfterZulu()
    {
        // Advance through all 26 letters.
        for (var i = 0; i < 26; i++)
            _generator.AdvanceLetter();

        Assert.Equal("Alpha", _generator.CurrentLetter);
    }

    [Fact]
    public void Generate_AfterAdvanceLetter_UsesNewLetter()
    {
        _generator.AdvanceLetter(); // Now Bravo
        var weather = new WeatherData { WindSpeedKt = 10, QnhHpa = 1013 };
        var result = _generator.Generate(_aerodrome, weather, _runway);
        Assert.Contains("information Bravo", result);
        Assert.Contains("Acknowledge receipt of information Bravo", result);
    }
}
