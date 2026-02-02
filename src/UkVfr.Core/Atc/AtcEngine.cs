using UkVfr.Core.Airspace;
using UkVfr.Core.Phraseology;
using UkVfr.Core.Voice;

namespace UkVfr.Core.Atc;

/// <summary>
/// Core ATC state machine. Receives pilot intents and produces ATC responses
/// using the phraseology engine.
/// </summary>
public sealed class AtcEngine
{
    private readonly PhraseologyEngine _phraseology;
    private readonly Random _squawkRandom = new();

    public AtcConversationState State { get; private set; } = AtcConversationState.Idle;
    public AtcUnit? ActiveUnit { get; private set; }
    public string? PilotCallsign { get; private set; }
    public string? AssignedSquawk { get; private set; }
    public ServiceType? ActiveService { get; private set; }

    public AtcEngine(PhraseologyEngine phraseology)
    {
        _phraseology = phraseology;
    }

    /// <summary>
    /// Process a pilot intent and return the ATC response.
    /// </summary>
    public AtcResponse ProcessIntent(PilotIntent intent, AtcUnit unit)
    {
        ActiveUnit = unit;

        return (State, intent.Type) switch
        {
            (AtcConversationState.Idle, PilotIntentType.InitialCall)
                => HandleInitialCall(intent, unit),

            (AtcConversationState.AwaitingPassMessage, PilotIntentType.PassMessage)
                => HandlePassMessage(intent, unit),

            (AtcConversationState.AwaitingPassMessage, PilotIntentType.JoinRequest)
                => HandleJoinRequest(intent, unit),

            (AtcConversationState.ProcessingRequest, PilotIntentType.Readback)
                => HandleReadback(intent, unit),

            (AtcConversationState.ServiceActive, PilotIntentType.PositionReport)
                => HandlePositionReport(intent, unit),

            (AtcConversationState.ServiceActive, PilotIntentType.FrequencyChangeRequest)
                => HandleFrequencyChange(intent, unit),

            (AtcConversationState.CircuitActive, PilotIntentType.PositionReport)
                => HandleCircuitPositionReport(intent, unit),

            (_, PilotIntentType.Emergency)
                => HandleEmergency(intent, unit),

            (_, PilotIntentType.Acknowledgement)
                => HandleAcknowledgement(unit),

            _ => HandleUnrecognised(unit)
        };
    }

    private AtcResponse HandleInitialCall(PilotIntent intent, AtcUnit unit)
    {
        PilotCallsign = intent.Callsign;
        var text = _phraseology.PassYourMessage(PilotCallsign ?? "station", unit.Callsign);
        State = AtcConversationState.AwaitingPassMessage;

        return new AtcResponse
        {
            Text = text,
            Voice = VoiceProfileForUnit(unit),
            NewState = State
        };
    }

    private AtcResponse HandlePassMessage(PilotIntent intent, AtcUnit unit)
    {
        AssignedSquawk = GenerateSquawk();
        var service = ServiceType.Basic;
        if (unit.ServicesAvailable.Contains(ServiceType.Traffic))
            service = ServiceType.Traffic;
        ActiveService = service;

        var text = _phraseology.ServiceResponse(
            PilotCallsign ?? "station",
            unit.Callsign,
            AssignedSquawk,
            service);

        State = AtcConversationState.ProcessingRequest;

        return new AtcResponse
        {
            Text = text,
            Voice = VoiceProfileForUnit(unit),
            NewState = State,
            AssignedSquawk = AssignedSquawk
        };
    }

    private AtcResponse HandleJoinRequest(PilotIntent intent, AtcUnit unit)
    {
        AssignedSquawk ??= GenerateSquawk();
        var runway = unit.Aerodrome.ActiveRunway ?? unit.Aerodrome.Runways.FirstOrDefault();

        var text = runway is not null
            ? _phraseology.JoinInstruction(
                PilotCallsign ?? "station",
                unit.Callsign,
                runway,
                AssignedSquawk)
            : _phraseology.GenericResponse(PilotCallsign ?? "station", unit.Callsign, "roger, stand by");

        State = AtcConversationState.CircuitActive;

        return new AtcResponse
        {
            Text = text,
            Voice = VoiceProfileForUnit(unit),
            NewState = State,
            AssignedSquawk = AssignedSquawk
        };
    }

    private AtcResponse HandleReadback(PilotIntent intent, AtcUnit unit)
    {
        State = AtcConversationState.ServiceActive;
        return new AtcResponse
        {
            Text = _phraseology.ReadbackCorrect(PilotCallsign ?? "station"),
            Voice = VoiceProfileForUnit(unit),
            NewState = State
        };
    }

    private AtcResponse HandlePositionReport(PilotIntent intent, AtcUnit unit)
    {
        return new AtcResponse
        {
            Text = _phraseology.Roger(PilotCallsign ?? "station"),
            Voice = VoiceProfileForUnit(unit),
            NewState = State
        };
    }

    private AtcResponse HandleFrequencyChange(PilotIntent intent, AtcUnit unit)
    {
        State = AtcConversationState.FrequencyChangeApproved;
        return new AtcResponse
        {
            Text = _phraseology.FrequencyChangeApproved(PilotCallsign ?? "station", unit.Callsign),
            Voice = VoiceProfileForUnit(unit),
            NewState = State
        };
    }

    private AtcResponse HandleCircuitPositionReport(PilotIntent intent, AtcUnit unit)
    {
        var runway = unit.Aerodrome.ActiveRunway ?? unit.Aerodrome.Runways.FirstOrDefault();
        var position = intent.Position ?? "circuit";

        string text;
        if (position.Contains("final", StringComparison.OrdinalIgnoreCase) && runway is not null)
            text = _phraseology.ClearedToLand(PilotCallsign ?? "station", unit.Callsign, runway);
        else
            text = _phraseology.Roger(PilotCallsign ?? "station");

        return new AtcResponse
        {
            Text = text,
            Voice = VoiceProfileForUnit(unit),
            NewState = State
        };
    }

    private AtcResponse HandleEmergency(PilotIntent intent, AtcUnit unit)
    {
        return new AtcResponse
        {
            Text = _phraseology.EmergencyAcknowledge(PilotCallsign ?? "station", unit.Callsign),
            Voice = VoiceProfileForUnit(unit),
            NewState = State
        };
    }

    private AtcResponse HandleAcknowledgement(AtcUnit unit)
    {
        return new AtcResponse
        {
            Text = "", // No response needed for a simple acknowledgement.
            Voice = VoiceProfileForUnit(unit),
            NewState = State
        };
    }

    private AtcResponse HandleUnrecognised(AtcUnit unit)
    {
        return new AtcResponse
        {
            Text = _phraseology.SayAgain(PilotCallsign ?? "station", unit.Callsign),
            Voice = VoiceProfileForUnit(unit),
            NewState = State
        };
    }

    private string GenerateSquawk()
    {
        // Generate a squawk in the realistic conspicuity range (0401–0477, 4501–4577).
        var code = _squawkRandom.Next(4501, 4578);
        return code.ToString("D4");
    }

    private static VoiceProfile VoiceProfileForUnit(AtcUnit unit) => unit.Type switch
    {
        AtcUnitType.Tower => VoiceProfile.Tower,
        AtcUnitType.Radar => VoiceProfile.Radar,
        _ => VoiceProfile.Tower
    };

    public void Reset()
    {
        State = AtcConversationState.Idle;
        ActiveUnit = null;
        PilotCallsign = null;
        AssignedSquawk = null;
        ActiveService = null;
    }
}
