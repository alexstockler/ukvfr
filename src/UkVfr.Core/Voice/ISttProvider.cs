namespace UkVfr.Core.Voice;

/// <summary>
/// Transcribes speech audio to text. Implementations include Whisper.net (local)
/// and OpenAI Whisper API (cloud).
/// </summary>
public interface ISttProvider
{
    /// <summary>
    /// Transcribe the given audio stream to text.
    /// </summary>
    Task<TranscriptionResult> TranscribeAsync(Stream audioStream, CancellationToken ct = default);

    /// <summary>
    /// Whether this provider is available (has required models/API keys).
    /// </summary>
    bool IsAvailable { get; }

    string Name { get; }
}
