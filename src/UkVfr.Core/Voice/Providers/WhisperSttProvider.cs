using NAudio.Wave;
using Whisper.net;

namespace UkVfr.Core.Voice.Providers;

/// <summary>
/// Local speech-to-text using Whisper.net (GGML runtime).
/// Runs OpenAI's Whisper model on-device for zero-cost, low-latency transcription.
///
/// Requires a GGML model file (e.g. ggml-base.en.bin) downloaded separately.
/// Download from: https://huggingface.co/ggerganov/whisper.cpp/tree/main
/// Recommended for MVP: ggml-base.en.bin (~148 MB, English-only, fast)
/// </summary>
public sealed class WhisperSttProvider : ISttProvider, IDisposable
{
    private readonly string _modelPath;
    private WhisperFactory? _factory;
    private WhisperProcessor? _processor;
    private bool _initialised;

    public WhisperSttProvider(string modelPath)
    {
        _modelPath = modelPath;
    }

    public bool IsAvailable => File.Exists(_modelPath);
    public string Name => "Whisper.net";

    public async Task<TranscriptionResult> TranscribeAsync(Stream audioStream, CancellationToken ct = default)
    {
        if (!IsAvailable)
        {
            return new TranscriptionResult
            {
                Text = "",
                ProviderName = Name,
                Confidence = 0
            };
        }

        EnsureInitialised();

        // Convert the input audio to float32 PCM at 16kHz mono (Whisper's required format).
        var samples = await ConvertToFloat32Pcm16kHzAsync(audioStream, ct);

        if (samples.Length == 0)
        {
            return new TranscriptionResult
            {
                Text = "",
                ProviderName = Name,
                Confidence = 0
            };
        }

        // Run Whisper inference.
        var segments = new List<string>();
        await foreach (var segment in _processor!.ProcessAsync(samples, ct))
        {
            segments.Add(segment.Text);
        }

        var text = string.Join(" ", segments).Trim();

        return new TranscriptionResult
        {
            Text = text,
            ProviderName = Name,
            Confidence = string.IsNullOrWhiteSpace(text) ? 0 : 0.85,
            Duration = TimeSpan.FromSeconds(samples.Length / 16000.0)
        };
    }

    private void EnsureInitialised()
    {
        if (_initialised) return;

        _factory = WhisperFactory.FromPath(_modelPath);
        _processor = _factory.CreateBuilder()
            .WithLanguage("en")
            .WithThreads(Environment.ProcessorCount > 4 ? 4 : Environment.ProcessorCount)
            .Build();
        _initialised = true;
    }

    /// <summary>
    /// Converts an audio stream (WAV/PCM) to float32 samples at 16kHz mono,
    /// which is the format Whisper requires.
    /// </summary>
    private static async Task<float[]> ConvertToFloat32Pcm16kHzAsync(Stream audioStream, CancellationToken ct)
    {
        // Copy to a seekable MemoryStream if needed.
        MemoryStream ms;
        if (!audioStream.CanSeek)
        {
            ms = new MemoryStream();
            await audioStream.CopyToAsync(ms, ct);
            ms.Position = 0;
        }
        else
        {
            ms = (audioStream as MemoryStream) ?? new MemoryStream();
            if (ms != audioStream)
            {
                await audioStream.CopyToAsync(ms, ct);
                ms.Position = 0;
            }
        }

        if (ms.Length == 0)
            return [];

        try
        {
            using var reader = new WaveFileReader(ms);
            // Resample to 16kHz mono if needed.
            var targetFormat = new WaveFormat(16000, 16, 1);

            using var resampler = new MediaFoundationResampler(reader, targetFormat);
            resampler.ResamplerQuality = 60;

            var outputMs = new MemoryStream();
            var buffer = new byte[4096];
            int bytesRead;
            while ((bytesRead = resampler.Read(buffer, 0, buffer.Length)) > 0)
            {
                outputMs.Write(buffer, 0, bytesRead);
            }

            // Convert 16-bit PCM bytes to float32 samples.
            var pcmBytes = outputMs.ToArray();
            var sampleCount = pcmBytes.Length / 2;
            var samples = new float[sampleCount];
            for (var i = 0; i < sampleCount; i++)
            {
                var int16 = BitConverter.ToInt16(pcmBytes, i * 2);
                samples[i] = int16 / 32768f;
            }

            return samples;
        }
        catch
        {
            // If WAV parsing fails, try to treat as raw 16-bit PCM at 16kHz.
            ms.Position = 0;
            var bytes = ms.ToArray();
            var sampleCount = bytes.Length / 2;
            if (sampleCount == 0) return [];

            var samples = new float[sampleCount];
            for (var i = 0; i < sampleCount; i++)
            {
                var int16 = BitConverter.ToInt16(bytes, i * 2);
                samples[i] = int16 / 32768f;
            }
            return samples;
        }
    }

    public void Dispose()
    {
        _processor?.Dispose();
        _factory?.Dispose();
    }
}
