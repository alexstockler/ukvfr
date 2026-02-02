#if WINDOWS
using System.Speech.Synthesis;
#endif

namespace UkVfr.Core.Voice.Providers;

/// <summary>
/// Offline TTS fallback using System.Speech.Synthesis (Windows only).
/// Robotic but functional when no cloud API keys are configured.
/// </summary>
public sealed class SystemSpeechTtsProvider : ITtsProvider
{
    public bool IsAvailable =>
#if WINDOWS
        true;
#else
        false;
#endif

    public string Name => "System.Speech";

    public Task<Stream> SynthesiseAsync(string text, VoiceProfile profile, CancellationToken ct = default)
    {
#if WINDOWS
        var synth = new SpeechSynthesizer();
        var stream = new MemoryStream();

        synth.SetOutputToWaveStream(stream);
        synth.Rate = profile.SpeechRate switch
        {
            < 0.8 => -2,
            < 0.95 => -1,
            > 1.1 => 2,
            > 1.0 => 1,
            _ => 0
        };

        synth.Speak(text);
        synth.Dispose();

        stream.Position = 0;
        return Task.FromResult<Stream>(stream);
#else
        throw new PlatformNotSupportedException("System.Speech is only available on Windows.");
#endif
    }
}
