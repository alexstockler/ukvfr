using NAudio.Wave;

namespace UkVfr.Core.Voice;

/// <summary>
/// Plays audio streams through the default output device using NAudio.
/// Used to output TTS-generated ATC speech (after radio filter processing).
/// </summary>
public sealed class AudioPlaybackService : IDisposable
{
    private WaveOutEvent? _waveOut;
    private bool _isPlaying;

    /// <summary>Raised when playback of a clip finishes.</summary>
    public event EventHandler? PlaybackFinished;

    public bool IsPlaying => _isPlaying;

    /// <summary>
    /// Play an audio stream (WAV format) through the default output device.
    /// Returns a task that completes when playback finishes.
    /// </summary>
    public async Task PlayAsync(Stream audioStream, CancellationToken ct = default)
    {
        if (audioStream.Length == 0) return;
        audioStream.Position = 0;

        var tcs = new TaskCompletionSource();

        _waveOut = new WaveOutEvent();

        try
        {
            var reader = new WaveFileReader(audioStream);
            _waveOut.Init(reader);

            _waveOut.PlaybackStopped += (_, _) =>
            {
                _isPlaying = false;
                PlaybackFinished?.Invoke(this, EventArgs.Empty);
                tcs.TrySetResult();
            };

            _isPlaying = true;
            _waveOut.Play();

            await using (ct.Register(() => tcs.TrySetCanceled()))
            {
                await tcs.Task;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _isPlaying = false;
            // If playback fails (e.g. invalid WAV, no audio device), degrade gracefully.
            tcs.TrySetResult();
        }
    }

    /// <summary>
    /// Play raw PCM data (non-WAV) with explicit format.
    /// Used for streams from cloud providers that return raw audio.
    /// </summary>
    public async Task PlayRawAsync(Stream pcmStream, int sampleRate = 24000, int bitsPerSample = 16, int channels = 1, CancellationToken ct = default)
    {
        if (pcmStream.Length == 0) return;
        pcmStream.Position = 0;

        var tcs = new TaskCompletionSource();

        _waveOut = new WaveOutEvent();
        var format = new WaveFormat(sampleRate, bitsPerSample, channels);
        var provider = new RawSourceWaveStream(pcmStream, format);

        _waveOut.Init(provider);
        _waveOut.PlaybackStopped += (_, _) =>
        {
            _isPlaying = false;
            PlaybackFinished?.Invoke(this, EventArgs.Empty);
            tcs.TrySetResult();
        };

        _isPlaying = true;
        _waveOut.Play();

        await using (ct.Register(() => tcs.TrySetCanceled()))
        {
            await tcs.Task;
        }
    }

    public void Stop()
    {
        if (_isPlaying)
        {
            _waveOut?.Stop();
        }
    }

    public void Dispose()
    {
        _waveOut?.Stop();
        _waveOut?.Dispose();
    }
}
