namespace UkVfr.Core.Voice;

/// <summary>
/// Parses a transcribed pilot transmission into a structured intent.
/// Implementations include rule-based pattern matching (fast, local) and
/// OpenAI GPT (cloud fallback for ambiguous input).
/// </summary>
public interface IIntentParser
{
    /// <summary>
    /// Parse the transcript into a structured pilot intent.
    /// </summary>
    Task<PilotIntent> ParseAsync(string transcript, ConversationContext context, CancellationToken ct = default);

    /// <summary>
    /// Whether this parser is available.
    /// </summary>
    bool IsAvailable { get; }

    string Name { get; }
}

/// <summary>
/// Conversation context provided to intent parsers to help disambiguate.
/// </summary>
public sealed class ConversationContext
{
    /// <summary>The aerodrome the pilot is currently interacting with, if any.</summary>
    public string? ActiveAerodromeIcao { get; init; }

    /// <summary>The ATC unit type currently being contacted (Tower, Approach, etc.).</summary>
    public string? ActiveUnitType { get; init; }

    /// <summary>The current state of the ATC conversation.</summary>
    public string? ConversationState { get; init; }

    /// <summary>The pilot's callsign, if already established.</summary>
    public string? PilotCallsign { get; init; }

    /// <summary>Recent transcript history for multi-turn context.</summary>
    public IReadOnlyList<string> RecentTranscripts { get; init; } = [];
}
