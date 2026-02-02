namespace UkVfr.Core.Atc;

/// <summary>
/// The state of an ATC conversation with a pilot.
/// Models the standard UK RT exchange flow.
/// </summary>
public enum AtcConversationState
{
    /// <summary>No active conversation — waiting for initial call.</summary>
    Idle,

    /// <summary>Pilot has made initial call; ATC has responded "pass your message".</summary>
    AwaitingPassMessage,

    /// <summary>Pilot has passed their message; ATC is processing the request.</summary>
    ProcessingRequest,

    /// <summary>An ATC service is active (Basic, Traffic, etc.).</summary>
    ServiceActive,

    /// <summary>Pilot is in the circuit, receiving circuit-specific instructions.</summary>
    CircuitActive,

    /// <summary>Frequency change has been approved; pilot is leaving.</summary>
    FrequencyChangeApproved,

    /// <summary>Service has been terminated (pilot left coverage or requested termination).</summary>
    ServiceTerminated
}
