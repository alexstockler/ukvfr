using UkVfr.Core.Voice;
using UkVfr.Core.Voice.Providers;
using Xunit;

namespace UkVfr.Core.Tests;

public class RuleBasedIntentParserTests
{
    private readonly RuleBasedIntentParser _parser = new();
    private readonly ConversationContext _idle = new() { ConversationState = "Idle" };
    private readonly ConversationContext _awaitingPass = new()
    {
        ConversationState = "AwaitingPassMessage",
        PilotCallsign = "Golf Alpha Bravo Charlie Delta"
    };

    [Fact]
    public async Task Mayday_IsEmergency()
    {
        var result = await _parser.ParseAsync("Mayday mayday mayday, Golf Alpha Bravo Charlie Delta", _idle);
        Assert.Equal(PilotIntentType.Emergency, result.Type);
    }

    [Fact]
    public async Task PanPan_IsEmergency()
    {
        var result = await _parser.ParseAsync("Pan pan pan pan pan pan", _idle);
        Assert.Equal(PilotIntentType.Emergency, result.Type);
    }

    [Fact]
    public async Task Roger_IsAcknowledgement()
    {
        var result = await _parser.ParseAsync("Roger", _idle);
        Assert.Equal(PilotIntentType.Acknowledgement, result.Type);
    }

    [Fact]
    public async Task Wilco_IsAcknowledgement()
    {
        var result = await _parser.ParseAsync("Wilco", _idle);
        Assert.Equal(PilotIntentType.Acknowledgement, result.Type);
    }

    [Theory]
    [InlineData("downwind")]
    [InlineData("final")]
    [InlineData("base")]
    [InlineData("crosswind")]
    public async Task CircuitLeg_IsPositionReport(string position)
    {
        var result = await _parser.ParseAsync(position, _idle);
        Assert.Equal(PilotIntentType.PositionReport, result.Type);
        Assert.Contains(position, result.Position!);
    }

    [Fact]
    public async Task RequestJoining_IsJoinRequest()
    {
        var result = await _parser.ParseAsync("Request joining instructions", _idle);
        Assert.Equal(PilotIntentType.JoinRequest, result.Type);
    }

    [Fact]
    public async Task RequestOverhead_IsJoinRequest()
    {
        var result = await _parser.ParseAsync("Request overhead join", _idle);
        Assert.Equal(PilotIntentType.JoinRequest, result.Type);
    }

    [Fact]
    public async Task FrequencyChange_Detected()
    {
        var result = await _parser.ParseAsync("Request frequency change", _idle);
        Assert.Equal(PilotIntentType.FrequencyChangeRequest, result.Type);
    }

    [Fact]
    public async Task ReadyForDeparture_Detected()
    {
        var result = await _parser.ParseAsync("Ready for departure", _idle);
        Assert.Equal(PilotIntentType.ReadyForDeparture, result.Type);
    }

    [Fact]
    public async Task InitialCall_Detected()
    {
        var result = await _parser.ParseAsync(
            "Farnborough Tower, Golf Alpha Bravo Charlie Delta", _idle);
        Assert.Equal(PilotIntentType.InitialCall, result.Type);
        Assert.Contains("tower", result.TargetUnit!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PassMessage_InAwaitingState()
    {
        var result = await _parser.ParseAsync(
            "Golf Alpha Bravo Charlie Delta, PA28, VFR from Guildford, 2000 feet, request joining instructions",
            _awaitingPass);
        Assert.Equal(PilotIntentType.PassMessage, result.Type);
    }

    [Fact]
    public async Task RequestTrafficService_Detected()
    {
        var result = await _parser.ParseAsync("Request traffic service", _idle);
        Assert.Equal(PilotIntentType.ServiceRequest, result.Type);
    }

    [Fact]
    public async Task Gibberish_IsUnrecognised()
    {
        var result = await _parser.ParseAsync("asdfghjkl", _idle);
        Assert.Equal(PilotIntentType.Unrecognised, result.Type);
    }
}
