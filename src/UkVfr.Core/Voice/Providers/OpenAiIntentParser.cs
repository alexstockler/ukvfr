using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UkVfr.Core.Voice.Providers;

/// <summary>
/// LLM-based intent parser using OpenAI GPT as a fallback for ambiguous pilot transmissions.
/// Sends the transcript + conversation context and asks the model to return structured JSON.
/// </summary>
public sealed class OpenAiIntentParser : IIntentParser
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly string _model;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    public OpenAiIntentParser(HttpClient httpClient, string? apiKey, string model = "gpt-4o-mini")
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _model = model;
    }

    public bool IsAvailable => !string.IsNullOrEmpty(_apiKey);
    public string Name => "OpenAI-GPT";

    public async Task<PilotIntent> ParseAsync(string transcript, ConversationContext context, CancellationToken ct = default)
    {
        if (!IsAvailable)
            return PilotIntent.Unrecognised(transcript);

        var systemPrompt = BuildSystemPrompt();
        var userPrompt = BuildUserPrompt(transcript, context);

        var requestBody = new
        {
            model = _model,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.1,
            max_tokens = 300,
            response_format = new { type = "json_object" }
        };

        var json = JsonSerializer.Serialize(requestBody, JsonOptions);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            return PilotIntent.Unrecognised(transcript);

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        return ParseResponse(responseJson, transcript);
    }

    private static string BuildSystemPrompt() => """
        You are a UK ATC radio communications parser. Given a pilot's radio transmission (transcribed from speech),
        extract the structured intent. Respond with JSON only.

        Intent types: initial_call, pass_message, join_request, service_request, readback,
        frequency_change_request, acknowledgement, position_report, taxi_request,
        ready_for_departure, emergency, service_termination, unrecognised.

        UK RT conventions (CAP 413):
        - "Pass your message" means ATC wants the pilot to state their request
        - Callsigns start with "Golf" for G-registered aircraft
        - "Affirm" means yes, "Negative" means no, "Roger" means received
        - Position reports include circuit legs: downwind, base, final, crosswind
        - "Overhead" means overhead join
        - "Request joining instructions" = join_request
        - "Request basic/traffic/deconfliction service" = service_request

        Respond with this JSON structure:
        {
          "intent_type": "<one of the types above>",
          "callsign": "<pilot callsign if mentioned, null otherwise>",
          "target_unit": "<ATC unit being called, null if not mentioned>",
          "position": "<position/circuit leg if mentioned>",
          "altitude": "<altitude if mentioned>",
          "requested_service": "<service type if requesting>",
          "confidence": <0.0 to 1.0>
        }
        """;

    private static string BuildUserPrompt(string transcript, ConversationContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Transcript: \"{transcript}\"");
        sb.AppendLine($"Current conversation state: {context.ConversationState ?? "idle"}");
        if (context.ActiveAerodromeIcao is not null)
            sb.AppendLine($"Active aerodrome: {context.ActiveAerodromeIcao}");
        if (context.PilotCallsign is not null)
            sb.AppendLine($"Established pilot callsign: {context.PilotCallsign}");
        if (context.RecentTranscripts.Count > 0)
        {
            sb.AppendLine("Recent exchanges:");
            foreach (var t in context.RecentTranscripts.TakeLast(3))
                sb.AppendLine($"  - \"{t}\"");
        }
        return sb.ToString();
    }

    private static PilotIntent ParseResponse(string responseJson, string transcript)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (content is null)
                return PilotIntent.Unrecognised(transcript);

            using var intentDoc = JsonDocument.Parse(content);
            var root = intentDoc.RootElement;

            var intentTypeStr = root.GetProperty("intent_type").GetString() ?? "unrecognised";
            var intentType = MapIntentType(intentTypeStr);

            return new PilotIntent
            {
                Type = intentType,
                RawTranscript = transcript,
                Callsign = GetOptionalString(root, "callsign"),
                TargetUnit = GetOptionalString(root, "target_unit"),
                Position = GetOptionalString(root, "position"),
                Altitude = GetOptionalString(root, "altitude"),
                RequestedService = GetOptionalString(root, "requested_service"),
                Confidence = root.TryGetProperty("confidence", out var c) ? c.GetDouble() : 0.7,
                ParserName = "OpenAI-GPT"
            };
        }
        catch
        {
            return PilotIntent.Unrecognised(transcript);
        }
    }

    private static PilotIntentType MapIntentType(string type) => type switch
    {
        "initial_call" => PilotIntentType.InitialCall,
        "pass_message" => PilotIntentType.PassMessage,
        "join_request" => PilotIntentType.JoinRequest,
        "service_request" => PilotIntentType.ServiceRequest,
        "readback" => PilotIntentType.Readback,
        "frequency_change_request" => PilotIntentType.FrequencyChangeRequest,
        "acknowledgement" => PilotIntentType.Acknowledgement,
        "position_report" => PilotIntentType.PositionReport,
        "taxi_request" => PilotIntentType.TaxiRequest,
        "ready_for_departure" => PilotIntentType.ReadyForDeparture,
        "emergency" => PilotIntentType.Emergency,
        "service_termination" => PilotIntentType.ServiceTermination,
        _ => PilotIntentType.Unrecognised
    };

    private static string? GetOptionalString(JsonElement element, string property)
    {
        if (element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String)
            return value.GetString();
        return null;
    }
}
