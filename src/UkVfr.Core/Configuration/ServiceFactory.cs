using UkVfr.Core.Atc;
using UkVfr.Core.Phraseology;
using UkVfr.Core.Voice;
using UkVfr.Core.Voice.Providers;

namespace UkVfr.Core.Configuration;

/// <summary>
/// Creates and wires up voice pipeline components based on configuration.
/// Handles provider fallback: cloud → local/offline automatically.
/// </summary>
public static class ServiceFactory
{
    public static ISttProvider CreateSttProvider(VoiceSettings settings)
    {
        // Try OpenAI Whisper API first if key is available.
        if (!string.IsNullOrEmpty(settings.Stt.OpenAiApiKey))
            return new OpenAiSttProvider(new HttpClient(), settings.Stt.OpenAiApiKey);

        // Fall back to local Whisper.net.
        return new WhisperSttProvider(settings.Stt.WhisperModelPath);
    }

    public static ITtsProvider CreateTtsProvider(VoiceSettings settings)
    {
        return settings.Tts.PreferredProvider.ToLowerInvariant() switch
        {
            "elevenlabs" when !string.IsNullOrEmpty(settings.Tts.ElevenLabsApiKey)
                => CreateElevenLabs(settings),

            "openai" when !string.IsNullOrEmpty(settings.Tts.OpenAiApiKey)
                => new OpenAiTtsProvider(new HttpClient(), settings.Tts.OpenAiApiKey),

            // Fall through to try ElevenLabs, then OpenAI, then System.Speech (Windows only).
            _ => TryCreateCloudTts(settings) ?? CreateFallbackTts()
        };
    }

    public static IIntentParser CreateIntentParser(VoiceSettings settings)
    {
        var ruleParser = new RuleBasedIntentParser();

        OpenAiIntentParser? llmParser = null;
        if (!string.IsNullOrEmpty(settings.IntentParser.OpenAiApiKey))
        {
            llmParser = new OpenAiIntentParser(
                new HttpClient(),
                settings.IntentParser.OpenAiApiKey,
                settings.IntentParser.OpenAiModel);
        }

        return new HybridIntentParser(ruleParser, llmParser, settings.IntentParser.ConfidenceThreshold);
    }

    public static VoicePipeline CreateVoicePipeline(VoiceSettings settings)
    {
        var stt = CreateSttProvider(settings);
        var tts = CreateTtsProvider(settings);
        var intentParser = CreateIntentParser(settings);
        var phraseology = new PhraseologyEngine();
        var atcEngine = new AtcEngine(phraseology);
        var radioFilter = settings.Tts.ApplyRadioFilter ? new RadioAudioFilter() : null;

        return new VoicePipeline(stt, intentParser, tts, atcEngine, radioFilter);
    }

    private static ElevenLabsTtsProvider CreateElevenLabs(VoiceSettings settings)
    {
        var voiceMap = new Dictionary<AtcUnitVoiceType, string>();
        foreach (var (key, value) in settings.Tts.ElevenLabsVoiceMap)
        {
            if (Enum.TryParse<AtcUnitVoiceType>(key, true, out var unitType))
                voiceMap[unitType] = value;
        }

        return new ElevenLabsTtsProvider(
            new HttpClient(),
            settings.Tts.ElevenLabsApiKey,
            settings.Tts.ElevenLabsDefaultVoiceId,
            voiceMap);
    }

    private static ITtsProvider? TryCreateCloudTts(VoiceSettings settings)
    {
        if (!string.IsNullOrEmpty(settings.Tts.ElevenLabsApiKey))
            return CreateElevenLabs(settings);

        if (!string.IsNullOrEmpty(settings.Tts.OpenAiApiKey))
            return new OpenAiTtsProvider(new HttpClient(), settings.Tts.OpenAiApiKey);

        return null;
    }

    private static ITtsProvider CreateFallbackTts()
    {
        if (OperatingSystem.IsWindows())
            return new SystemSpeechTtsProvider();

        throw new PlatformNotSupportedException(
            "No TTS provider available. Configure ElevenLabs or OpenAI API keys, or run on Windows for System.Speech fallback.");
    }
}
