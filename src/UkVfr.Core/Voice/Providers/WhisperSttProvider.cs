namespace UkVfr.Core.Voice.Providers;

/// <summary>
/// Local speech-to-text using Whisper.net (GGML runtime).
/// Runs OpenAI's Whisper model on-device for zero-cost, low-latency transcription.
///
/// Requires the Whisper.net NuGet package and a GGML model file (e.g. ggml-base.en.bin).
/// The model file path is configurable.
///
/// NOTE: Full implementation depends on Whisper.net package being installed.
/// This implementation provides the integration contract and will be wired up
/// when the package dependency is added.
/// </summary>
public sealed class WhisperSttProvider : ISttProvider
{
    private readonly string _modelPath;
    private bool _modelLoaded;

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

        // TODO: Wire up Whisper.net when package is added.
        // The implementation will:
        // 1. Load the GGML model on first call (_modelLoaded flag)
        // 2. Convert the input audio stream to 16kHz mono PCM (Whisper requirement)
        // 3. Run inference using WhisperProcessor
        // 4. Return the transcribed text with confidence score
        //
        // Approximate implementation shape:
        //
        // if (!_modelLoaded)
        // {
        //     _factory = WhisperFactory.FromPath(_modelPath);
        //     _processor = _factory.CreateBuilder()
        //         .WithLanguage("en")
        //         .Build();
        //     _modelLoaded = true;
        // }
        //
        // var samples = ConvertToFloat32Pcm16kHz(audioStream);
        // var segments = new List<string>();
        // await foreach (var segment in _processor.ProcessAsync(samples, ct))
        // {
        //     segments.Add(segment.Text);
        // }
        //
        // return new TranscriptionResult
        // {
        //     Text = string.Join(" ", segments).Trim(),
        //     ProviderName = Name,
        //     Confidence = 0.85
        // };

        await Task.CompletedTask;
        return new TranscriptionResult
        {
            Text = "[Whisper.net not yet wired — install Whisper.net package]",
            ProviderName = Name,
            Confidence = 0
        };
    }
}
