namespace UkVfr.Core.Voice.Providers;

/// <summary>
/// Combines rule-based parsing with LLM fallback. Tries the fast local parser first;
/// if the result is Unrecognised or low-confidence, falls back to OpenAI GPT.
/// </summary>
public sealed class HybridIntentParser : IIntentParser
{
    private readonly RuleBasedIntentParser _ruleParser;
    private readonly OpenAiIntentParser? _llmParser;
    private readonly double _confidenceThreshold;

    public HybridIntentParser(RuleBasedIntentParser ruleParser, OpenAiIntentParser? llmParser, double confidenceThreshold = 0.6)
    {
        _ruleParser = ruleParser;
        _llmParser = llmParser;
        _confidenceThreshold = confidenceThreshold;
    }

    public bool IsAvailable => true;
    public string Name => "Hybrid";

    public async Task<PilotIntent> ParseAsync(string transcript, ConversationContext context, CancellationToken ct = default)
    {
        // Try rule-based first (sub-millisecond).
        var result = await _ruleParser.ParseAsync(transcript, context, ct);

        if (result.Type != PilotIntentType.Unrecognised && result.Confidence >= _confidenceThreshold)
            return result;

        // Fall back to LLM if available.
        if (_llmParser is { IsAvailable: true })
        {
            var llmResult = await _llmParser.ParseAsync(transcript, context, ct);
            if (llmResult.Type != PilotIntentType.Unrecognised)
                return llmResult;
        }

        return result;
    }
}
