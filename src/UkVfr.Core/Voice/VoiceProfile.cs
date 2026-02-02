namespace UkVfr.Core.Voice;

/// <summary>
/// Defines the voice characteristics for a particular ATC unit.
/// Maps to a specific voice in the TTS provider (e.g. ElevenLabs voice ID).
/// </summary>
public sealed class VoiceProfile
{
    public required string Name { get; init; }
    public required AtcUnitVoiceType UnitType { get; init; }

    /// <summary>Provider-specific voice identifier (e.g. ElevenLabs voice_id).</summary>
    public string? ProviderVoiceId { get; init; }

    /// <summary>Speech rate multiplier. 1.0 = normal, 0.8 = slower (ATIS), 1.1 = slightly faster.</summary>
    public double SpeechRate { get; init; } = 1.0;

    public static VoiceProfile Tower => new()
    {
        Name = "Tower",
        UnitType = AtcUnitVoiceType.Tower,
        SpeechRate = 1.0
    };

    public static VoiceProfile Radar => new()
    {
        Name = "Radar",
        UnitType = AtcUnitVoiceType.Radar,
        SpeechRate = 0.95
    };

    public static VoiceProfile Atis => new()
    {
        Name = "ATIS",
        UnitType = AtcUnitVoiceType.Atis,
        SpeechRate = 0.85
    };
}

public enum AtcUnitVoiceType
{
    Tower,
    Approach,
    Radar,
    Radio,
    Atis
}
