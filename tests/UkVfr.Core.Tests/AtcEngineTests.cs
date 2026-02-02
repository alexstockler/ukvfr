using UkVfr.Core.Airspace;
using UkVfr.Core.Atc;
using UkVfr.Core.Phraseology;
using UkVfr.Core.Voice;
using Xunit;

namespace UkVfr.Core.Tests;

public class AtcEngineTests
{
    private readonly AtcEngine _engine;
    private readonly AtcUnit _unit;

    public AtcEngineTests()
    {
        var phraseology = new PhraseologyEngine();
        _engine = new AtcEngine(phraseology);

        var aerodrome = new Aerodrome
        {
            Icao = "EGLF",
            Name = "Farnborough",
            LatitudeDeg = 51.2758,
            LongitudeDeg = -0.7764,
            ElevationFt = 238,
            Frequencies = new AerodromeFrequencies { Tower = 122.5 },
            Runways = [new Runway { Designator = "24", HeadingMag = 243, LengthM = 2440 }],
            Atz = new AirTrafficZone { Type = "circle", RadiusNm = 2.5, UpperLimitFtAal = 2000 },
            UnitCallsigns = new UnitCallsigns { Tower = "Farnborough Tower" },
            ServicesAvailable = [ServiceType.Basic, ServiceType.Traffic],
            ActiveRunway = null
        };
        aerodrome.ActiveRunway = aerodrome.Runways[0];

        _unit = new AtcUnit
        {
            Callsign = "Farnborough Tower",
            Type = AtcUnitType.Tower,
            FrequencyMhz = 122.5,
            ServicesAvailable = aerodrome.ServicesAvailable,
            Aerodrome = aerodrome
        };
    }

    [Fact]
    public void InitialState_IsIdle()
    {
        Assert.Equal(AtcConversationState.Idle, _engine.State);
    }

    [Fact]
    public void InitialCall_TransitionsToAwaitingPassMessage()
    {
        var intent = new PilotIntent
        {
            Type = PilotIntentType.InitialCall,
            Callsign = "Golf Alpha Bravo Charlie Delta",
            ParserName = "Test"
        };

        var response = _engine.ProcessIntent(intent, _unit);

        Assert.Equal(AtcConversationState.AwaitingPassMessage, _engine.State);
        Assert.Contains("pass your message", response.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Golf Alpha Bravo Charlie Delta", _engine.PilotCallsign);
    }

    [Fact]
    public void PassMessage_AssignsSquawkAndService()
    {
        // First do initial call.
        _engine.ProcessIntent(new PilotIntent
        {
            Type = PilotIntentType.InitialCall,
            Callsign = "Golf Alpha Bravo Charlie Delta",
            ParserName = "Test"
        }, _unit);

        // Then pass message.
        var response = _engine.ProcessIntent(new PilotIntent
        {
            Type = PilotIntentType.PassMessage,
            Callsign = "Golf Alpha Bravo Charlie Delta",
            ParserName = "Test"
        }, _unit);

        Assert.Equal(AtcConversationState.ProcessingRequest, _engine.State);
        Assert.NotNull(_engine.AssignedSquawk);
        Assert.Contains("squawk", response.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Service", response.Text);
    }

    [Fact]
    public void JoinRequest_GivesCircuitInstructions()
    {
        _engine.ProcessIntent(new PilotIntent
        {
            Type = PilotIntentType.InitialCall,
            Callsign = "Golf Alpha Bravo Charlie Delta",
            ParserName = "Test"
        }, _unit);

        var response = _engine.ProcessIntent(new PilotIntent
        {
            Type = PilotIntentType.JoinRequest,
            Callsign = "Golf Alpha Bravo Charlie Delta",
            ParserName = "Test"
        }, _unit);

        Assert.Equal(AtcConversationState.CircuitActive, _engine.State);
        Assert.Contains("join", response.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("runway", response.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FinalPositionReport_GivesClearedToLand()
    {
        // Walk through the conversation.
        _engine.ProcessIntent(new PilotIntent { Type = PilotIntentType.InitialCall, Callsign = "Golf CD", ParserName = "T" }, _unit);
        _engine.ProcessIntent(new PilotIntent { Type = PilotIntentType.JoinRequest, ParserName = "T" }, _unit);

        var response = _engine.ProcessIntent(new PilotIntent
        {
            Type = PilotIntentType.PositionReport,
            Position = "final",
            ParserName = "T"
        }, _unit);

        Assert.Contains("cleared to land", response.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        _engine.ProcessIntent(new PilotIntent
        {
            Type = PilotIntentType.InitialCall,
            Callsign = "Golf CD",
            ParserName = "Test"
        }, _unit);

        _engine.Reset();

        Assert.Equal(AtcConversationState.Idle, _engine.State);
        Assert.Null(_engine.PilotCallsign);
        Assert.Null(_engine.AssignedSquawk);
    }
}
