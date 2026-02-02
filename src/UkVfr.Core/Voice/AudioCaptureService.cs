using NAudio.Wave;

namespace UkVfr.Core.Voice;

/// <summary>
/// Captures audio from the microphone using NAudio.
/// Supports push-to-talk (PTT) style recording: call StartRecording(),
/// speak, then call StopRecording() to get the captured audio.
/// </summary>
public sealed class AudioCaptureService : IDisposable
{
    private WaveInEvent? _waveIn;
    private MemoryStream? _captureStream;
    private WaveFileWriter? _waveWriter;
    private bool _isRecording;

    /// <summary>Recording format: 16kHz 16-bit mono (Whisper-friendly).</summary>
    private static readonly WaveFormat CaptureFormat = new(16000, 16, 1);

    /// <summary>Raised when recording level changes (0.0–1.0). Useful for VU meter UI.</summary>
    public event EventHandler<float>? LevelChanged;

    public bool IsRecording => _isRecording;

    /// <summary>
    /// Start capturing audio from the default microphone.
    /// </summary>
    public void StartRecording()
    {
        if (_isRecording) return;

        _captureStream = new MemoryStream();
        _waveWriter = new WaveFileWriter(_captureStream, CaptureFormat);

        _waveIn = new WaveInEvent
        {
            WaveFormat = CaptureFormat,
            BufferMilliseconds = 100
        };
        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.StartRecording();
        _isRecording = true;
    }

    /// <summary>
    /// Stop recording and return the captured audio as a WAV stream.
    /// </summary>
    public Stream? StopRecording()
    {
        if (!_isRecording || _waveIn is null) return null;

        _waveIn.StopRecording();
        _waveIn.DataAvailable -= OnDataAvailable;
        _waveIn.Dispose();
        _waveIn = null;
        _isRecording = false;

        // Flush and finalize the WAV file.
        _waveWriter?.Flush();
        _waveWriter?.Dispose();
        _waveWriter = null;

        if (_captureStream is null || _captureStream.Length == 0)
            return null;

        _captureStream.Position = 0;
        var result = new MemoryStream(_captureStream.ToArray());
        _captureStream.Dispose();
        _captureStream = null;

        return result;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        _waveWriter?.Write(e.Buffer, 0, e.BytesRecorded);

        // Calculate level for VU meter.
        if (e.BytesRecorded > 0)
        {
            var max = 0f;
            for (var i = 0; i < e.BytesRecorded; i += 2)
            {
                var sample = Math.Abs(BitConverter.ToInt16(e.Buffer, i) / 32768f);
                if (sample > max) max = sample;
            }
            LevelChanged?.Invoke(this, max);
        }
    }

    public void Dispose()
    {
        if (_isRecording) StopRecording();
        _waveIn?.Dispose();
        _waveWriter?.Dispose();
        _captureStream?.Dispose();
    }
}
