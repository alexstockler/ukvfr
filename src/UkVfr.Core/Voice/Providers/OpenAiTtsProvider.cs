using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace UkVfr.Core.Voice.Providers;

/// <summary>
/// TTS provider using OpenAI's TTS API as a fallback to ElevenLabs.
/// </summary>
public sealed class OpenAiTtsProvider : ITtsProvider
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly string _model;
    private readonly string _defaultVoice;

    private static readonly Dictionary<AtcUnitVoiceType, string> VoiceMap = new()
    {
        [AtcUnitVoiceType.Tower] = "onyx",
        [AtcUnitVoiceType.Approach] = "echo",
        [AtcUnitVoiceType.Radar] = "echo",
        [AtcUnitVoiceType.Radio] = "fable",
        [AtcUnitVoiceType.Atis] = "nova"
    };

    public OpenAiTtsProvider(HttpClient httpClient, string? apiKey, string model = "tts-1", string defaultVoice = "onyx")
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _model = model;
        _defaultVoice = defaultVoice;
    }

    public bool IsAvailable => !string.IsNullOrEmpty(_apiKey);
    public string Name => "OpenAI-TTS";

    public async Task<Stream> SynthesiseAsync(string text, VoiceProfile profile, CancellationToken ct = default)
    {
        if (!IsAvailable)
            throw new InvalidOperationException("OpenAI API key not configured.");

        var voice = profile.ProviderVoiceId
            ?? (VoiceMap.TryGetValue(profile.UnitType, out var mapped) ? mapped : _defaultVoice);

        var requestBody = new
        {
            model = _model,
            input = text,
            voice,
            speed = profile.SpeechRate,
            response_format = "pcm"
        };

        var json = JsonSerializer.Serialize(requestBody);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/audio/speech");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var audioStream = new MemoryStream();
        await response.Content.CopyToAsync(audioStream, ct);
        audioStream.Position = 0;
        return audioStream;
    }
}
