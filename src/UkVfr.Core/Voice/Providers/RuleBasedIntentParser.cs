using System.Text.RegularExpressions;

namespace UkVfr.Core.Voice.Providers;

/// <summary>
/// Fast, local intent parser using regex patterns against standard CAP 413 phraseology.
/// Handles well-formed radio calls with sub-millisecond latency.
/// Falls through to Unrecognised for ambiguous input (which can then be routed to the LLM fallback).
/// </summary>
public sealed partial class RuleBasedIntentParser : IIntentParser
{
    public bool IsAvailable => true;
    public string Name => "RuleBased";

    public Task<PilotIntent> ParseAsync(string transcript, ConversationContext context, CancellationToken ct = default)
    {
        var text = transcript.Trim();
        var intent = Parse(text, context);
        return Task.FromResult(intent);
    }

    private static PilotIntent Parse(string text, ConversationContext context)
    {
        // Normalise for matching: lowercase, collapse whitespace.
        var normalised = CollapseWhitespace(text.ToLowerInvariant());

        // --- Emergency (highest priority) ---
        if (normalised.Contains("mayday") || normalised.Contains("pan pan"))
        {
            return new PilotIntent
            {
                Type = PilotIntentType.Emergency,
                RawTranscript = text,
                Callsign = ExtractCallsign(text),
                ParserName = "RuleBased",
                Confidence = 0.95
            };
        }

        // --- Acknowledgements ---
        if (IsAcknowledgement(normalised))
        {
            return new PilotIntent
            {
                Type = PilotIntentType.Acknowledgement,
                RawTranscript = text,
                Callsign = context.PilotCallsign,
                ParserName = "RuleBased",
                Confidence = 0.9
            };
        }

        // --- Position reports ---
        var positionMatch = PositionReportPattern().Match(normalised);
        if (positionMatch.Success)
        {
            return new PilotIntent
            {
                Type = PilotIntentType.PositionReport,
                RawTranscript = text,
                Position = positionMatch.Groups["position"].Value,
                Callsign = context.PilotCallsign,
                ParserName = "RuleBased",
                Confidence = 0.85
            };
        }

        // --- Join / overhead requests ---
        if (JoinPattern().IsMatch(normalised))
        {
            return new PilotIntent
            {
                Type = PilotIntentType.JoinRequest,
                RawTranscript = text,
                Callsign = ExtractCallsign(text) ?? context.PilotCallsign,
                ParserName = "RuleBased",
                Confidence = 0.85
            };
        }

        // --- Frequency change ---
        if (FrequencyChangePattern().IsMatch(normalised))
        {
            return new PilotIntent
            {
                Type = PilotIntentType.FrequencyChangeRequest,
                RawTranscript = text,
                Callsign = context.PilotCallsign,
                ParserName = "RuleBased",
                Confidence = 0.9
            };
        }

        // --- Ready for departure ---
        if (normalised.Contains("ready for departure") || normalised.Contains("ready to depart"))
        {
            return new PilotIntent
            {
                Type = PilotIntentType.ReadyForDeparture,
                RawTranscript = text,
                Callsign = context.PilotCallsign,
                ParserName = "RuleBased",
                Confidence = 0.9
            };
        }

        // --- Taxi request ---
        if (normalised.Contains("request taxi") || normalised.Contains("taxi to"))
        {
            return new PilotIntent
            {
                Type = PilotIntentType.TaxiRequest,
                RawTranscript = text,
                Callsign = context.PilotCallsign,
                ParserName = "RuleBased",
                Confidence = 0.85
            };
        }

        // --- Service request ---
        if (ServiceRequestPattern().IsMatch(normalised))
        {
            var serviceMatch = ServiceRequestPattern().Match(normalised);
            return new PilotIntent
            {
                Type = PilotIntentType.ServiceRequest,
                RawTranscript = text,
                RequestedService = serviceMatch.Groups["service"].Value,
                Callsign = ExtractCallsign(text) ?? context.PilotCallsign,
                ParserName = "RuleBased",
                Confidence = 0.85
            };
        }

        // --- Readback (contains squawk digits, runway, QNH etc.) ---
        if (ReadbackPattern().IsMatch(normalised))
        {
            return new PilotIntent
            {
                Type = PilotIntentType.Readback,
                RawTranscript = text,
                Callsign = context.PilotCallsign,
                ParserName = "RuleBased",
                Confidence = 0.8
            };
        }

        // --- Pass message (contains position/altitude/intentions info after initial call) ---
        if (context.ConversationState == "AwaitingPassMessage" && normalised.Length > 20)
        {
            return new PilotIntent
            {
                Type = PilotIntentType.PassMessage,
                RawTranscript = text,
                Callsign = ExtractCallsign(text) ?? context.PilotCallsign,
                Position = ExtractPosition(normalised),
                Altitude = ExtractAltitude(normalised),
                ParserName = "RuleBased",
                Confidence = 0.75
            };
        }

        // --- Initial call: "[Unit name], [Callsign]" ---
        if (InitialCallPattern().IsMatch(normalised))
        {
            var match = InitialCallPattern().Match(normalised);
            return new PilotIntent
            {
                Type = PilotIntentType.InitialCall,
                RawTranscript = text,
                TargetUnit = match.Groups["unit"].Value.Trim(),
                Callsign = match.Groups["callsign"].Value.Trim(),
                ParserName = "RuleBased",
                Confidence = 0.8
            };
        }

        // --- Fallback: if in AwaitingPassMessage state, treat as pass message ---
        if (context.ConversationState == "AwaitingPassMessage")
        {
            return new PilotIntent
            {
                Type = PilotIntentType.PassMessage,
                RawTranscript = text,
                Callsign = context.PilotCallsign,
                ParserName = "RuleBased",
                Confidence = 0.5
            };
        }

        return PilotIntent.Unrecognised(text);
    }

    private static bool IsAcknowledgement(string text)
    {
        var trimmed = text.Trim(' ', '.', ',');
        return trimmed is "roger" or "affirm" or "wilco" or "roger wilco"
            || trimmed.StartsWith("roger,")
            || trimmed.StartsWith("wilco,");
    }

    private static string? ExtractCallsign(string text)
    {
        // Look for "Golf [Alpha-Zulu] [Alpha-Zulu] [Alpha-Zulu] [Alpha-Zulu]" pattern.
        var match = CallsignPattern().Match(text);
        return match.Success ? match.Value.Trim() : null;
    }

    private static string? ExtractPosition(string text)
    {
        var positions = new[] { "overhead", "downwind", "base", "final", "crosswind", "dead side", "live side" };
        foreach (var pos in positions)
        {
            if (text.Contains(pos))
                return pos;
        }
        return null;
    }

    private static string? ExtractAltitude(string text)
    {
        var match = AltitudePattern().Match(text);
        return match.Success ? match.Value : null;
    }

    private static string CollapseWhitespace(string text) =>
        MultiSpacePattern().Replace(text, " ");

    // --- Compiled regex patterns ---

    [GeneratedRegex(@"\b(?<position>downwind|final|base|crosswind|overhead|dead side|live side|upwind|long final)\b")]
    private static partial Regex PositionReportPattern();

    [GeneratedRegex(@"(?:request|requesting)\s+(?:join|joining|overhead|circuit)|join\s+(?:overhead|downwind|circuit)|inbound.*(?:join|landing)")]
    private static partial Regex JoinPattern();

    [GeneratedRegex(@"(?:request|requesting)\s+frequency\s+change|frequency\s+change")]
    private static partial Regex FrequencyChangePattern();

    [GeneratedRegex(@"(?:request|requesting)\s+(?<service>basic\s+service|traffic\s+service|deconfliction\s+service)")]
    private static partial Regex ServiceRequestPattern();

    [GeneratedRegex(@"(?:squawk|runway|q\s*n\s*h|q\s*f\s*e)\s+\w+")]
    private static partial Regex ReadbackPattern();

    [GeneratedRegex(@"(?<unit>[\w\s]+?(?:tower|approach|radar|radio|information))\s*,\s*(?<callsign>.+)", RegexOptions.IgnoreCase)]
    private static partial Regex InitialCallPattern();

    [GeneratedRegex(@"golf\s+(?:alpha|bravo|charlie|delta|echo|foxtrot|hotel|india|juliet|kilo|lima|mike|november|oscar|papa|quebec|romeo|sierra|tango|uniform|victor|whiskey|x-?ray|yankee|zulu)(?:\s+(?:alpha|bravo|charlie|delta|echo|foxtrot|hotel|india|juliet|kilo|lima|mike|november|oscar|papa|quebec|romeo|sierra|tango|uniform|victor|whiskey|x-?ray|yankee|zulu)){1,4}", RegexOptions.IgnoreCase)]
    private static partial Regex CallsignPattern();

    [GeneratedRegex(@"\d{3,5}\s*(?:feet|ft)")]
    private static partial Regex AltitudePattern();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex MultiSpacePattern();
}
