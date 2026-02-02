namespace UkVfr.Core.Voice;

/// <summary>
/// Synthesises speech audio from text. Implementations include ElevenLabs,
/// OpenAI TTS, and System.Speech (offline fallback).
/// </summary>
public interface ITtsProvider
{
    /// <summary>
    /// Synthesise the given text to an audio stream (PCM/WAV).
    /// </summary>
    Task<Stream> SynthesiseAsync(string text, VoiceProfile profile, CancellationToken ct = default);

    /// <summary>
    /// Whether this provider is available (has required API keys, models, etc.).
    /// </summary>
    bool IsAvailable { get; }

    string Name { get; }
}
