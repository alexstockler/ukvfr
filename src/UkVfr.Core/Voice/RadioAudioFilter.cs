namespace UkVfr.Core.Voice;

/// <summary>
/// Applies VHF radio simulation effects to TTS audio output.
/// Processes raw PCM/WAV audio through a band-pass filter (300Hz–3.4kHz),
/// adds slight compression and a low-level noise floor.
///
/// NOTE: Full DSP implementation requires NAudio package.
/// This provides the processing contract and a basic implementation.
/// </summary>
public sealed class RadioAudioFilter
{
    private const int LowCutoffHz = 300;
    private const int HighCutoffHz = 3400;
    private const float NoiseLevel = 0.005f;
    private const float CompressionThreshold = 0.7f;
    private const float CompressionRatio = 3.0f;

    /// <summary>
    /// Apply radio effects to a WAV audio stream.
    /// Returns a new stream with the filtered audio.
    /// </summary>
    public Stream Apply(Stream inputAudio)
    {
        // Read the input into a byte array.
        using var reader = new BinaryReader(inputAudio, System.Text.Encoding.Default, leaveOpen: false);
        var inputBytes = reader.ReadBytes((int)inputAudio.Length);

        // For WAV files, skip the 44-byte header and process raw PCM samples.
        // This is a simplified implementation; production code should parse the WAV header properly.
        if (inputBytes.Length < 44)
            return new MemoryStream(inputBytes);

        var headerSize = 44;
        var sampleCount = (inputBytes.Length - headerSize) / 2; // 16-bit samples
        var samples = new float[sampleCount];

        // Convert bytes to float samples.
        for (var i = 0; i < sampleCount; i++)
        {
            var sample = BitConverter.ToInt16(inputBytes, headerSize + i * 2);
            samples[i] = sample / 32768f;
        }

        // Apply effects.
        ApplyBandPassApproximation(samples);
        ApplyCompression(samples);
        AddNoiseFloor(samples);

        // Convert back to bytes.
        var output = new byte[inputBytes.Length];
        Array.Copy(inputBytes, output, headerSize); // Preserve WAV header.

        for (var i = 0; i < sampleCount; i++)
        {
            var clamped = Math.Clamp(samples[i], -1f, 1f);
            var int16 = (short)(clamped * 32767f);
            BitConverter.GetBytes(int16).CopyTo(output, headerSize + i * 2);
        }

        return new MemoryStream(output);
    }

    /// <summary>
    /// Simple band-pass approximation using a running average (low-pass) and
    /// first-order difference (high-pass). Not a proper IIR filter but
    /// gives a reasonable radio effect for TTS output.
    /// </summary>
    private static void ApplyBandPassApproximation(float[] samples)
    {
        if (samples.Length == 0) return;

        // Simple high-pass: remove very low frequencies.
        var alpha = 0.95f; // Higher = less low-frequency removal
        var prev = samples[0];
        var prevOut = samples[0];
        for (var i = 1; i < samples.Length; i++)
        {
            prevOut = alpha * (prevOut + samples[i] - prev);
            prev = samples[i];
            samples[i] = prevOut;
        }

        // Simple low-pass: smooth out high frequencies.
        // Using a basic exponential moving average.
        var lpAlpha = 0.3f; // Lower = more smoothing (more high-freq removal)
        for (var i = 1; i < samples.Length; i++)
        {
            samples[i] = lpAlpha * samples[i] + (1f - lpAlpha) * samples[i - 1];
        }
    }

    private static void ApplyCompression(float[] samples)
    {
        for (var i = 0; i < samples.Length; i++)
        {
            var abs = Math.Abs(samples[i]);
            if (abs > CompressionThreshold)
            {
                var excess = abs - CompressionThreshold;
                var compressed = CompressionThreshold + excess / CompressionRatio;
                samples[i] = samples[i] > 0 ? compressed : -compressed;
            }
        }
    }

    private static void AddNoiseFloor(float[] samples)
    {
        var rng = new Random(42); // Deterministic for consistent output in tests.
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] += (float)(rng.NextDouble() * 2 - 1) * NoiseLevel;
        }
    }
}
