using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace UkVfr.Core.Voice.Providers;

/// <summary>
/// TTS provider using ElevenLabs streaming API.
/// Produces natural-sounding speech with distinct voice profiles per ATC unit type.
/// </summary>
public sealed class ElevenLabsTtsProvider : ITtsProvider
{
    private const string BaseUrl = "https://api.elevenlabs.io/v1";

    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly string _defaultVoiceId;

    /// <summary>
    /// Maps unit voice types to ElevenLabs voice IDs.
    /// Configure these in appsettings.json with your preferred voices.
    /// </summary>
    private readonly Dictionary<AtcUnitVoiceType, string> _voiceMap;

    public ElevenLabsTtsProvider(
        HttpClient httpClient,
        string? apiKey,
        string defaultVoiceId = "21m00Tcm4TlvDq8ikWAM", // Rachel (default ElevenLabs voice)
        Dictionary<AtcUnitVoiceType, string>? voiceMap = null)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _defaultVoiceId = defaultVoiceId;
        _voiceMap = voiceMap ?? new Dictionary<AtcUnitVoiceType, string>();
    }

    public bool IsAvailable => !string.IsNullOrEmpty(_apiKey);
    public string Name => "ElevenLabs";

    public async Task<Stream> SynthesiseAsync(string text, VoiceProfile profile, CancellationToken ct = default)
    {
        if (!IsAvailable)
            throw new InvalidOperationException("ElevenLabs API key not configured.");

        var voiceId = ResolveVoiceId(profile);
        var url = $"{BaseUrl}/text-to-speech/{voiceId}/stream";

        var requestBody = new
        {
            text,
            model_id = "eleven_monolingual_v1",
            voice_settings = new
            {
                stability = 0.75,
                similarity_boost = 0.75,
                style = 0.0,
                use_speaker_boost = true
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("xi-api-key", _apiKey);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        // Return the streaming audio content. Caller is responsible for disposal.
        var audioStream = new MemoryStream();
        await response.Content.CopyToAsync(audioStream, ct);
        audioStream.Position = 0;
        return audioStream;
    }

    private string ResolveVoiceId(VoiceProfile profile)
    {
        // Check provider-specific override first.
        if (!string.IsNullOrEmpty(profile.ProviderVoiceId))
            return profile.ProviderVoiceId;

        // Check the voice map for this unit type.
        if (_voiceMap.TryGetValue(profile.UnitType, out var mapped))
            return mapped;

        return _defaultVoiceId;
    }
}
