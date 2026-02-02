namespace UkVfr.Core.Voice;

/// <summary>
/// Result of a speech-to-text transcription.
/// </summary>
public sealed class TranscriptionResult
{
    /// <summary>The transcribed text.</summary>
    public required string Text { get; init; }

    /// <summary>Confidence score from the STT provider (0.0–1.0). Null if not available.</summary>
    public double? Confidence { get; init; }

    /// <summary>Duration of the audio that was transcribed.</summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>The provider that produced this transcription.</summary>
    public required string ProviderName { get; init; }

    public bool IsEmpty => string.IsNullOrWhiteSpace(Text);
}
