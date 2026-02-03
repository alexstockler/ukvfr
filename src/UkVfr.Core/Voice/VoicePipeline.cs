using UkVfr.Core.Atc;

namespace UkVfr.Core.Voice;

/// <summary>
/// Orchestrates the full voice interaction pipeline:
/// Audio → STT → Intent Parsing → ATC Engine → Phraseology → TTS → Radio Filter → Speaker.
/// </summary>
public sealed class VoicePipeline
{
    private readonly ISttProvider _sttProvider;
    private readonly IIntentParser _intentParser;
    private readonly ITtsProvider _ttsProvider;
    private readonly AtcEngine _atcEngine;
    private readonly RadioAudioFilter? _radioFilter;

    private readonly List<string> _recentTranscripts = [];

    public VoicePipeline(
        ISttProvider sttProvider,
        IIntentParser intentParser,
        ITtsProvider ttsProvider,
        AtcEngine atcEngine,
        RadioAudioFilter? radioFilter = null)
    {
        _sttProvider = sttProvider;
        _intentParser = intentParser;
        _ttsProvider = ttsProvider;
        _atcEngine = atcEngine;
        _radioFilter = radioFilter;
    }

    /// <summary>
    /// Raised when the pipeline produces a transcript from pilot speech.
    /// </summary>
    public event EventHandler<TranscriptionResult>? PilotTranscribed;

    /// <summary>
    /// Raised when the ATC engine produces a response.
    /// </summary>
    public event EventHandler<AtcResponse>? AtcResponded;

    /// <summary>
    /// Raised when TTS audio is ready for playback.
    /// </summary>
    public event EventHandler<Stream>? AudioReady;

    /// <summary>
    /// Process a pilot transmission from raw audio through the full pipeline.
    /// </summary>
    public async Task ProcessPilotAudioAsync(Stream audioStream, AtcUnit unit, CancellationToken ct = default)
    {
        // Step 1: Speech-to-Text
        var transcription = await _sttProvider.TranscribeAsync(audioStream, ct);
        PilotTranscribed?.Invoke(this, transcription);

        if (transcription.IsEmpty)
            return;

        _recentTranscripts.Add(transcription.Text);
        if (_recentTranscripts.Count > 10)
            _recentTranscripts.RemoveAt(0);

        // Step 2: Intent Parsing
        var context = new ConversationContext
        {
            ActiveAerodromeIcao = unit.Aerodrome.Icao,
            ActiveUnitType = unit.Type.ToString(),
            ConversationState = _atcEngine.State.ToString(),
            PilotCallsign = _atcEngine.PilotCallsign,
            RecentTranscripts = _recentTranscripts.AsReadOnly()
        };

        var intent = await _intentParser.ParseAsync(transcription.Text, context, ct);

        // Step 3: ATC Engine processes intent
        var response = _atcEngine.ProcessIntent(intent, unit);
        AtcResponded?.Invoke(this, response);

        if (string.IsNullOrEmpty(response.Text))
            return;

        // Step 4: TTS synthesis
        var audio = await _ttsProvider.SynthesiseAsync(response.Text, response.Voice, ct);

        // Step 5: Apply radio band-pass filter for VHF radio effect.
        var output = _radioFilter is not null ? _radioFilter.Apply(audio) : audio;

        AudioReady?.Invoke(this, output);
    }

    /// <summary>
    /// Process a pilot intent directly (from menu selection, bypassing STT).
    /// </summary>
    public async Task ProcessMenuInputAsync(PilotIntent intent, AtcUnit unit, CancellationToken ct = default)
    {
        var response = _atcEngine.ProcessIntent(intent, unit);
        AtcResponded?.Invoke(this, response);

        if (string.IsNullOrEmpty(response.Text))
            return;

        var audio = await _ttsProvider.SynthesiseAsync(response.Text, response.Voice, ct);

        var output = _radioFilter is not null ? _radioFilter.Apply(audio) : audio;

        AudioReady?.Invoke(this, output);
    }
}
