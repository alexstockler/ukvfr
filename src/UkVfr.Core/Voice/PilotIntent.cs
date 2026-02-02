namespace UkVfr.Core.Voice;

/// <summary>
/// A structured representation of what the pilot intends, parsed from their radio call.
/// </summary>
public sealed class PilotIntent
{
    public required PilotIntentType Type { get; init; }

    /// <summary>The pilot's callsign as spoken (e.g. "Golf Alpha Bravo Charlie Delta").</summary>
    public string? Callsign { get; init; }

    /// <summary>The ATC unit being called (e.g. "Farnborough Tower").</summary>
    public string? TargetUnit { get; init; }

    /// <summary>Additional details extracted from the transmission.</summary>
    public string? Position { get; init; }
    public string? Altitude { get; init; }
    public string? RequestedService { get; init; }
    public string? SquawkCode { get; init; }

    /// <summary>The raw transcript this intent was parsed from.</summary>
    public string? RawTranscript { get; init; }

    /// <summary>Confidence in the parse (0.0–1.0).</summary>
    public double Confidence { get; init; } = 1.0;

    /// <summary>Which parser produced this intent.</summary>
    public required string ParserName { get; init; }

    public static PilotIntent Unrecognised(string transcript) => new()
    {
        Type = PilotIntentType.Unrecognised,
        RawTranscript = transcript,
        Confidence = 0,
        ParserName = "none"
    };
}

public enum PilotIntentType
{
    /// <summary>Could not determine intent.</summary>
    Unrecognised,

    /// <summary>"[Unit], [Callsign]" — initial contact.</summary>
    InitialCall,

    /// <summary>Pilot passes their message after "pass your message".</summary>
    PassMessage,

    /// <summary>Request to join the circuit / join overhead.</summary>
    JoinRequest,

    /// <summary>Request for a specific ATC service (Basic, Traffic, etc.).</summary>
    ServiceRequest,

    /// <summary>Readback of ATC instructions.</summary>
    Readback,

    /// <summary>Request to change frequency.</summary>
    FrequencyChangeRequest,

    /// <summary>"Roger" or simple acknowledgement.</summary>
    Acknowledgement,

    /// <summary>Position report ("downwind", "final", etc.).</summary>
    PositionReport,

    /// <summary>Request taxi instructions.</summary>
    TaxiRequest,

    /// <summary>"Ready for departure".</summary>
    ReadyForDeparture,

    /// <summary>Emergency declaration (Mayday/Pan-Pan).</summary>
    Emergency,

    /// <summary>Cancelling a service or leaving frequency.</summary>
    ServiceTermination
}
