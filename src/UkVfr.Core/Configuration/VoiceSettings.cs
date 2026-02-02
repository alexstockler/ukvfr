namespace UkVfr.Core.Configuration;

/// <summary>
/// Configuration for voice providers. Loaded from appsettings.json.
/// Missing keys cause automatic fallback to local/offline providers.
/// </summary>
public sealed class VoiceSettings
{
    public SttSettings Stt { get; init; } = new();
    public TtsSettings Tts { get; init; } = new();
    public IntentParserSettings IntentParser { get; init; } = new();
}

public sealed class SttSettings
{
    /// <summary>Path to the Whisper GGML model file (e.g. "models/ggml-base.en.bin").</summary>
    public string WhisperModelPath { get; init; } = "models/ggml-base.en.bin";

    /// <summary>OpenAI API key for cloud Whisper fallback. Leave empty to use local only.</summary>
    public string? OpenAiApiKey { get; init; }
}

public sealed class TtsSettings
{
    /// <summary>Preferred TTS provider: "elevenlabs", "openai", or "system".</summary>
    public string PreferredProvider { get; init; } = "elevenlabs";

    /// <summary>ElevenLabs API key.</summary>
    public string? ElevenLabsApiKey { get; init; }

    /// <summary>Default ElevenLabs voice ID.</summary>
    public string ElevenLabsDefaultVoiceId { get; init; } = "21m00Tcm4TlvDq8ikWAM";

    /// <summary>ElevenLabs voice IDs per unit type.</summary>
    public Dictionary<string, string> ElevenLabsVoiceMap { get; init; } = new();

    /// <summary>OpenAI API key for TTS fallback.</summary>
    public string? OpenAiApiKey { get; init; }

    /// <summary>Whether to apply the radio band-pass filter to TTS output.</summary>
    public bool ApplyRadioFilter { get; init; } = true;
}

public sealed class IntentParserSettings
{
    /// <summary>OpenAI API key for GPT intent parsing fallback. Leave empty for rule-based only.</summary>
    public string? OpenAiApiKey { get; init; }

    /// <summary>OpenAI model to use for intent parsing.</summary>
    public string OpenAiModel { get; init; } = "gpt-4o-mini";

    /// <summary>Minimum confidence from rule-based parser before falling back to LLM.</summary>
    public double ConfidenceThreshold { get; init; } = 0.6;
}
