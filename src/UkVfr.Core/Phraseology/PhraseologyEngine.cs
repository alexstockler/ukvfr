using UkVfr.Core.Airspace;

namespace UkVfr.Core.Phraseology;

/// <summary>
/// Generates correctly formatted UK ATC messages per CAP 413.
/// All output is in spoken form ready for TTS.
/// </summary>
public sealed class PhraseologyEngine
{
    public string PassYourMessage(string callsign, string unitCallsign)
    {
        return $"{callsign}, {unitCallsign}, pass your message";
    }

    public string ServiceResponse(string callsign, string unitCallsign, string squawk, ServiceType service)
    {
        var serviceStr = service switch
        {
            ServiceType.Basic => "Basic Service",
            ServiceType.Traffic => "Traffic Service",
            ServiceType.Deconfliction => "Deconfliction Service",
            ServiceType.Procedural => "Procedural Service",
            _ => "Basic Service"
        };

        return $"{callsign}, {unitCallsign}, squawk {CallsignFormatter.SpokenDigits(squawk)}, {serviceStr}";
    }

    public string JoinInstruction(string callsign, string unitCallsign, Runway runway, string squawk)
    {
        var direction = runway.CircuitDirection == CircuitDirection.Left ? "left hand" : "right hand";
        var rwySpoken = CallsignFormatter.SpokenRunway(runway.Designator);
        var circuitHeight = runway.CircuitHeightQfeFt;

        return $"{callsign}, {unitCallsign}, join overhead runway {rwySpoken}, " +
               $"descend on the dead side to circuit height {circuitHeight} feet Q F E, " +
               $"{direction} circuit, squawk {CallsignFormatter.SpokenDigits(squawk)}";
    }

    public string ClearedToLand(string callsign, string unitCallsign, Runway runway)
    {
        var rwySpoken = CallsignFormatter.SpokenRunway(runway.Designator);
        return $"{callsign}, runway {rwySpoken}, cleared to land";
    }

    public string ReadbackCorrect(string callsign)
    {
        return $"{callsign}, readback correct";
    }

    public string Roger(string callsign)
    {
        return $"{callsign}, roger";
    }

    public string SayAgain(string callsign, string unitCallsign)
    {
        return $"{callsign}, {unitCallsign}, say again";
    }

    public string FrequencyChangeApproved(string callsign, string unitCallsign)
    {
        return $"{callsign}, frequency change approved, squawk seven thousand";
    }

    public string EmergencyAcknowledge(string callsign, string unitCallsign)
    {
        return $"{callsign}, {unitCallsign}, roger your Mayday, all received";
    }

    public string GenericResponse(string callsign, string unitCallsign, string message)
    {
        return $"{callsign}, {unitCallsign}, {message}";
    }

    public string LineUp(string callsign, Runway runway)
    {
        var rwySpoken = CallsignFormatter.SpokenRunway(runway.Designator);
        return $"{callsign}, line up runway {rwySpoken}";
    }

    public string ClearedTakeOff(string callsign, Runway runway)
    {
        var rwySpoken = CallsignFormatter.SpokenRunway(runway.Designator);
        return $"{callsign}, runway {rwySpoken}, cleared for take off";
    }
}
