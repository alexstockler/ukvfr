using UkVfr.Core.Voice;
using Xunit;

namespace UkVfr.Core.Tests;

public class RadioAudioFilterTests
{
    private readonly RadioAudioFilter _filter = new();

    /// <summary>
    /// Creates a minimal WAV stream with the given 16-bit PCM samples.
    /// Uses 16kHz mono format with a 44-byte WAV header.
    /// </summary>
    private static MemoryStream CreateWavStream(short[] samples)
    {
        var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, System.Text.Encoding.Default, leaveOpen: true);

        var dataSize = samples.Length * 2;
        var fileSize = 36 + dataSize;

        // WAV header (44 bytes).
        writer.Write("RIFF"u8);
        writer.Write(fileSize);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);          // Subchunk1Size
        writer.Write((short)1);    // PCM format
        writer.Write((short)1);    // Mono
        writer.Write(16000);       // Sample rate
        writer.Write(32000);       // Byte rate (16000 * 1 * 2)
        writer.Write((short)2);    // Block align
        writer.Write((short)16);   // Bits per sample
        writer.Write("data"u8);
        writer.Write(dataSize);

        foreach (var sample in samples)
            writer.Write(sample);

        ms.Position = 0;
        return ms;
    }

    [Fact]
    public void Apply_TooShortInput_ReturnsSameBytes()
    {
        var input = new MemoryStream(new byte[20]);
        var result = _filter.Apply(input);
        Assert.Equal(20, result.Length);
    }

    [Fact]
    public void Apply_SilentAudio_AddsOnlyNoise()
    {
        // All-zero samples should produce only noise floor output.
        var samples = new short[1000];
        using var input = CreateWavStream(samples);

        var result = _filter.Apply(input);
        result.Position = 0;

        Assert.True(result.Length > 44, "Output should include WAV header plus data");

        // Read output samples.
        using var reader = new BinaryReader(result);
        reader.ReadBytes(44); // Skip header
        var outputSamples = new float[samples.Length];
        for (var i = 0; i < samples.Length; i++)
        {
            outputSamples[i] = reader.ReadInt16() / 32768f;
        }

        // Samples should be very small (just noise).
        var maxAbs = outputSamples.Max(s => Math.Abs(s));
        Assert.True(maxAbs < 0.02f, $"Silent input should produce near-silent output, but max was {maxAbs}");
        Assert.True(maxAbs > 0f, "Noise floor should add some signal");
    }

    [Fact]
    public void Apply_PreservesOutputLength()
    {
        var samples = new short[500];
        for (var i = 0; i < samples.Length; i++)
            samples[i] = (short)(Math.Sin(2 * Math.PI * 1000.0 / 16000 * i) * 16000);

        using var input = CreateWavStream(samples);
        var inputLength = input.Length;

        var result = _filter.Apply(input);

        Assert.Equal(inputLength, result.Length);
    }

    [Fact]
    public void Apply_CompressesLoudSignal()
    {
        // Create a loud signal near clipping.
        var samples = new short[1000];
        for (var i = 0; i < samples.Length; i++)
            samples[i] = (short)(Math.Sin(2 * Math.PI * 1000.0 / 16000 * i) * 30000);

        using var input = CreateWavStream(samples);
        var result = _filter.Apply(input);
        result.Position = 0;

        using var reader = new BinaryReader(result);
        reader.ReadBytes(44);

        var maxOutput = 0f;
        for (var i = 0; i < samples.Length; i++)
        {
            var s = Math.Abs(reader.ReadInt16() / 32768f);
            if (s > maxOutput) maxOutput = s;
        }

        // The compressor should prevent the output from being as loud as the input.
        var maxInput = 30000f / 32768f;
        Assert.True(maxOutput < maxInput,
            $"Compression should reduce peak level: input peak {maxInput:F3}, output peak {maxOutput:F3}");
    }

    [Fact]
    public void Apply_IsDeterministic()
    {
        var samples = new short[200];
        for (var i = 0; i < samples.Length; i++)
            samples[i] = (short)(Math.Sin(2 * Math.PI * 800.0 / 16000 * i) * 10000);

        using var input1 = CreateWavStream(samples);
        var result1 = _filter.Apply(input1);
        var bytes1 = ((MemoryStream)result1).ToArray();

        using var input2 = CreateWavStream(samples);
        var result2 = _filter.Apply(input2);
        var bytes2 = ((MemoryStream)result2).ToArray();

        Assert.Equal(bytes1, bytes2);
    }

    [Fact]
    public void Apply_ReducesLowFrequencyContent()
    {
        // Generate a very low-frequency signal (50Hz, well below the 300Hz cutoff).
        var samples = new short[1600]; // 100ms at 16kHz
        for (var i = 0; i < samples.Length; i++)
            samples[i] = (short)(Math.Sin(2 * Math.PI * 50.0 / 16000 * i) * 16000);

        var inputRms = CalculateRms(samples);

        using var input = CreateWavStream(samples);
        var result = _filter.Apply(input);
        result.Position = 0;

        using var reader = new BinaryReader(result);
        reader.ReadBytes(44);
        var outputSamples = new short[samples.Length];
        for (var i = 0; i < samples.Length; i++)
            outputSamples[i] = reader.ReadInt16();

        var outputRms = CalculateRms(outputSamples);

        // The high-pass filter should attenuate the low-frequency signal.
        Assert.True(outputRms < inputRms * 0.5,
            $"Low frequency should be attenuated: input RMS {inputRms:F3}, output RMS {outputRms:F3}");
    }

    private static double CalculateRms(short[] samples)
    {
        var sumSquares = 0.0;
        foreach (var s in samples)
        {
            var f = s / 32768.0;
            sumSquares += f * f;
        }
        return Math.Sqrt(sumSquares / samples.Length);
    }
}
