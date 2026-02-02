using UkVfr.Core.Phraseology;
using Xunit;

namespace UkVfr.Core.Tests;

public class CallsignFormatterTests
{
    [Theory]
    [InlineData("G-ABCD", "Golf Alpha Bravo Charlie Delta")]
    [InlineData("G-OFLY", "Golf Oscar Foxtrot Lima Yankee")]
    public void ToFullPhonetic_FormatsCorrectly(string registration, string expected)
    {
        var result = CallsignFormatter.ToFullPhonetic(registration);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("G-ABCD", "Golf Charlie Delta")]
    [InlineData("G-OFLY", "Golf Lima Yankee")]
    public void ToAbbreviated_UsesFirstAndLastTwo(string registration, string expected)
    {
        var result = CallsignFormatter.ToAbbreviated(registration);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("4512", "four five one two")]
    [InlineData("7000", "seven zero zero zero")]
    [InlineData("1013", "one zero one three")]
    public void SpokenDigits_FormatsCorrectly(string number, string expected)
    {
        var result = CallsignFormatter.SpokenDigits(number);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("24", "two four")]
    [InlineData("06", "zero six")]
    public void SpokenRunway_FormatsCorrectly(string designator, string expected)
    {
        var result = CallsignFormatter.SpokenRunway(designator);
        Assert.Equal(expected, result);
    }
}
