using NAudio.Wave;

namespace UkVfr.Core.Audio;

/// <summary>
/// Handles microphone capture and speaker playback for voice communications.
/// Uses NAudio for cross-platform audio I/O.
/// </summary>
public sealed class AudioService : IDisposable
{
    private WaveInEvent? _waveIn;
    private WaveOutEvent? _waveOut;
    private MemoryStream? _recordingBuffer;
    private WaveFileWriter? _waveWriter;
    private bool _isRecording;
    private bool _isDisposed;

    public event EventHandler<byte[]>? RecordingComplete;
    public event EventHandler? PlaybackComplete;

    public bool IsRecording => _isRecording;
    public bool IsPlaying => _waveOut?.PlaybackState == PlaybackState.Playing;

    /// <summary>
    /// Start recording from the default microphone.
    /// Audio is captured at 16kHz mono (optimal for Whisper STT).
    /// </summary>
    public void StartRecording()
    {
        if (_isRecording) return;

        _recordingBuffer = new MemoryStream();
        _waveWriter = new WaveFileWriter(_recordingBuffer, new WaveFormat(16000, 16, 1));

        _waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(16000, 16, 1),
            BufferMilliseconds = 100
        };

        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.RecordingStopped += OnRecordingStopped;

        _waveIn.StartRecording();
        _isRecording = true;
    }

    /// <summary>
    /// Stop recording and return the captured audio as WAV bytes.
    /// </summary>
    public byte[] StopRecording()
    {
        if (!_isRecording || _waveIn is null) return [];

        _waveIn.StopRecording();
        _isRecording = false;

        // Wait briefly for buffer to flush
        Thread.Sleep(100);

        _waveWriter?.Flush();
        var audioData = _recordingBuffer?.ToArray() ?? [];

        CleanupRecording();
        return audioData;
    }

    /// <summary>
    /// Play audio from a WAV stream through the default speaker.
    /// </summary>
    public void PlayAudio(Stream audioStream)
    {
        StopPlayback();

        try
        {
            audioStream.Position = 0;
            var reader = new WaveFileReader(audioStream);

            _waveOut = new WaveOutEvent();
            _waveOut.PlaybackStopped += OnPlaybackStopped;
            _waveOut.Init(reader);
            _waveOut.Play();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Audio playback error: {ex.Message}");
        }
    }

    /// <summary>
    /// Play audio from WAV bytes through the default speaker.
    /// </summary>
    public void PlayAudio(byte[] audioData)
    {
        if (audioData.Length == 0) return;
        PlayAudio(new MemoryStream(audioData));
    }

    /// <summary>
    /// Stop any current playback.
    /// </summary>
    public void StopPlayback()
    {
        if (_waveOut is not null)
        {
            _waveOut.Stop();
            _waveOut.Dispose();
            _waveOut = null;
        }
    }

    /// <summary>
    /// Get available recording devices.
    /// </summary>
    public static List<string> GetRecordingDevices()
    {
        var devices = new List<string>();
        for (var i = 0; i < WaveIn.DeviceCount; i++)
        {
            var caps = WaveIn.GetCapabilities(i);
            devices.Add(caps.ProductName);
        }
        return devices;
    }

    /// <summary>
    /// Get available playback devices.
    /// </summary>
    public static List<string> GetPlaybackDevices()
    {
        var devices = new List<string>();
        for (var i = 0; i < WaveOut.DeviceCount; i++)
        {
            var caps = WaveOut.GetCapabilities(i);
            devices.Add(caps.ProductName);
        }
        return devices;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        _waveWriter?.Write(e.Buffer, 0, e.BytesRecorded);
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (_recordingBuffer is not null && _waveWriter is not null)
        {
            _waveWriter.Flush();
            var audioData = _recordingBuffer.ToArray();
            RecordingComplete?.Invoke(this, audioData);
        }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        PlaybackComplete?.Invoke(this, EventArgs.Empty);
    }

    private void CleanupRecording()
    {
        _waveWriter?.Dispose();
        _waveWriter = null;
        _recordingBuffer?.Dispose();
        _recordingBuffer = null;
        _waveIn?.Dispose();
        _waveIn = null;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        StopPlayback();
        if (_isRecording)
            StopRecording();
        CleanupRecording();
    }
}
