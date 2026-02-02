using System.Net.Http.Headers;

namespace UkVfr.Core.Voice.Providers;

/// <summary>
/// Cloud STT fallback using OpenAI's Whisper API.
/// Higher accuracy in noisy environments but requires internet and API key.
/// </summary>
public sealed class OpenAiSttProvider : ISttProvider
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;

    public OpenAiSttProvider(HttpClient httpClient, string? apiKey)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
    }

    public bool IsAvailable => !string.IsNullOrEmpty(_apiKey);
    public string Name => "OpenAI-Whisper";

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

        using var content = new MultipartFormDataContent();
        var audioContent = new StreamContent(audioStream);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        content.Add(audioContent, "file", "audio.wav");
        content.Add(new StringContent("whisper-1"), "model");
        content.Add(new StringContent("en"), "language");
        content.Add(new StringContent("verbose_json"), "response_format");

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/audio/transcriptions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Content = content;

        using var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return new TranscriptionResult
            {
                Text = "",
                ProviderName = Name,
                Confidence = 0
            };
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        // Parse the verbose_json response to extract text and segments.
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var text = doc.RootElement.GetProperty("text").GetString() ?? "";

        return new TranscriptionResult
        {
            Text = text.Trim(),
            ProviderName = Name,
            Confidence = 0.9
        };
    }
}
