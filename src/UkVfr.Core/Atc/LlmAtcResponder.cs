using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using UkVfr.Core.Airspace;

namespace UkVfr.Core.Atc;

/// <summary>
/// LLM-powered ATC responder that generates dynamic, contextually appropriate
/// responses using OpenAI. Falls back to template-based responses if unavailable.
/// </summary>
public sealed class LlmAtcResponder
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly string _model;
    private readonly AtcEngine _fallbackEngine;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public LlmAtcResponder(HttpClient httpClient, string? apiKey, AtcEngine fallbackEngine, string model = "gpt-4o-mini")
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _fallbackEngine = fallbackEngine;
        _model = model;
    }

    public bool IsAvailable => !string.IsNullOrEmpty(_apiKey);

    /// <summary>
    /// Generate an ATC response to a pilot transmission.
    /// </summary>
    public async Task<AtcResponse> RespondAsync(
        string pilotTransmission,
        AtcUnit unit,
        List<(string Speaker, string Text)> recentExchanges,
        CancellationToken ct = default)
    {
        if (!IsAvailable)
        {
            // Fall back to rule-based engine
            return FallbackResponse(pilotTransmission, unit);
        }

        try
        {
            var systemPrompt = BuildSystemPrompt(unit);
            var userPrompt = BuildUserPrompt(pilotTransmission, unit, recentExchanges);

            var requestBody = new
            {
                model = _model,
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.3,
                max_tokens = 200
            };

            var json = JsonSerializer.Serialize(requestBody, JsonOptions);
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return FallbackResponse(pilotTransmission, unit);

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            var atcText = ParseResponse(responseJson);

            if (string.IsNullOrWhiteSpace(atcText))
                return FallbackResponse(pilotTransmission, unit);

            return new AtcResponse
            {
                Text = atcText,
                Voice = VoiceProfileForUnit(unit),
                NewState = _fallbackEngine.State
            };
        }
        catch
        {
            return FallbackResponse(pilotTransmission, unit);
        }
    }

    private static string BuildSystemPrompt(AtcUnit unit)
    {
        var unitType = unit.Type == AtcUnitType.Tower ? "Tower/Radio" : "Radar";
        var runway = unit.Aerodrome.ActiveRunway;
        var rwyInfo = runway is not null
            ? $"Active runway: {runway.Designator}, {runway.CircuitDirection} hand circuit at {runway.CircuitHeightQfeFt}ft QFE."
            : "No runway information available.";

        return $"""
            You are {unit.Callsign}, a UK {unitType} ATC controller at {unit.Aerodrome.Name} ({unit.Aerodrome.Icao}).
            Frequency: {unit.FrequencyMhz:F3} MHz. {rwyInfo}

            CRITICAL RULES (CAP 413 UK RT Phraseology):
            - Respond ONLY with what the controller would say on the radio. No explanations, no brackets, no stage directions.
            - Use standard UK phraseology: "Golf Charlie Delta" not "GCD", "Affirm" not "Yes", "Negative" not "No"
            - For initial calls, respond with "[callsign], [your callsign], pass your message"
            - Keep responses concise and professional - real ATC is brief
            - Use phonetic alphabet for callsigns (Golf Alpha Bravo Charlie Delta)
            - Include QNH/QFE when giving joining instructions
            - For position reports in circuit, acknowledge with "Roger" or give clearance
            - On final, give "Cleared to land, runway [number]" or "Continue approach"
            - If transmission is unclear, say "[callsign], say again"

            Example exchanges:
            Pilot: "Denham Radio, Golf Alpha Bravo Charlie Delta"
            ATC: "Golf Alpha Bravo Charlie Delta, Denham Radio, pass your message"

            Pilot: "Golf Charlie Delta, student pilot, overhead at 2000 feet, request joining instructions"
            ATC: "Golf Charlie Delta, join right hand downwind runway 24, QFE 1013, report downwind"

            Pilot: "Downwind, Golf Charlie Delta"
            ATC: "Golf Charlie Delta, roger, report final"

            Pilot: "Final, Golf Charlie Delta"
            ATC: "Golf Charlie Delta, cleared to land runway 24, surface wind 240 degrees 8 knots"
            """;
    }

    private static string BuildUserPrompt(
        string pilotTransmission,
        AtcUnit unit,
        List<(string Speaker, string Text)> recentExchanges)
    {
        var sb = new StringBuilder();

        if (recentExchanges.Count > 0)
        {
            sb.AppendLine("Recent radio exchanges:");
            foreach (var (speaker, text) in recentExchanges.TakeLast(6))
            {
                sb.AppendLine($"{speaker}: {text}");
            }
            sb.AppendLine();
        }

        sb.AppendLine($"New pilot transmission: \"{pilotTransmission}\"");
        sb.AppendLine();
        sb.AppendLine($"Respond as {unit.Callsign}. Output ONLY the ATC response, nothing else.");

        return sb.ToString();
    }

    private static string? ParseResponse(string responseJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString()?.Trim();
        }
        catch
        {
            return null;
        }
    }

    private AtcResponse FallbackResponse(string pilotTransmission, AtcUnit unit)
    {
        // Use the rule-based intent parser and engine as fallback
        var intent = new Voice.PilotIntent
        {
            Type = Voice.PilotIntentType.Unrecognised,
            RawTranscript = pilotTransmission,
            ParserName = "Fallback"
        };

        // Try to detect basic intents from the text
        var lower = pilotTransmission.ToLowerInvariant();
        if (lower.Contains("radio") || lower.Contains("tower") || lower.Contains("approach"))
        {
            if (!lower.Contains("request") && !lower.Contains("overhead") && !lower.Contains("joining"))
            {
                intent.Type = Voice.PilotIntentType.InitialCall;
                intent.Callsign = ExtractCallsign(pilotTransmission);
            }
        }
        if (lower.Contains("request joining") || lower.Contains("joining instructions"))
            intent.Type = Voice.PilotIntentType.JoinRequest;
        if (lower.Contains("downwind"))
        {
            intent.Type = Voice.PilotIntentType.PositionReport;
            intent.Position = "downwind";
        }
        if (lower.Contains("final"))
        {
            intent.Type = Voice.PilotIntentType.PositionReport;
            intent.Position = "final";
        }
        if (lower.Contains("roger") || lower.Contains("wilco"))
            intent.Type = Voice.PilotIntentType.Acknowledgement;

        return _fallbackEngine.ProcessIntent(intent, unit);
    }

    private static string? ExtractCallsign(string text)
    {
        // Simple extraction: look for "Golf" followed by letters
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var callsignParts = new List<string>();
        var foundGolf = false;

        foreach (var word in words)
        {
            if (word.Equals("Golf", StringComparison.OrdinalIgnoreCase))
            {
                foundGolf = true;
                callsignParts.Add(word);
            }
            else if (foundGolf && IsPhoneticLetter(word))
            {
                callsignParts.Add(word);
            }
            else if (foundGolf && callsignParts.Count > 1)
            {
                break;
            }
        }

        return callsignParts.Count > 1 ? string.Join(" ", callsignParts) : null;
    }

    private static bool IsPhoneticLetter(string word)
    {
        var phonetics = new[] { "Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot",
            "Golf", "Hotel", "India", "Juliet", "Kilo", "Lima", "Mike", "November",
            "Oscar", "Papa", "Quebec", "Romeo", "Sierra", "Tango", "Uniform",
            "Victor", "Whiskey", "Xray", "Yankee", "Zulu" };
        return phonetics.Any(p => p.Equals(word, StringComparison.OrdinalIgnoreCase));
    }

    private static Voice.VoiceProfile VoiceProfileForUnit(AtcUnit unit) => unit.Type switch
    {
        AtcUnitType.Tower => Voice.VoiceProfile.Tower,
        AtcUnitType.Radar => Voice.VoiceProfile.Radar,
        _ => Voice.VoiceProfile.Tower
    };
}
