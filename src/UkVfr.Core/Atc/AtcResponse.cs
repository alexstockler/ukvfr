using UkVfr.Core.Voice;

namespace UkVfr.Core.Atc;

/// <summary>
/// An ATC response to be spoken via TTS and displayed in the transcript.
/// </summary>
public sealed class AtcResponse
{
    /// <summary>The text to be spoken (phraseology-correct).</summary>
    public required string Text { get; init; }

    /// <summary>The voice profile to use for this response.</summary>
    public required VoiceProfile Voice { get; init; }

    /// <summary>The new conversation state after this response.</summary>
    public required AtcConversationState NewState { get; init; }

    /// <summary>Squawk code assigned, if any.</summary>
    public string? AssignedSquawk { get; init; }
}
