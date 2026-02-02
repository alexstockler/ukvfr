using UkVfr.Core.Tracking;
using Xunit;

namespace UkVfr.Core.Tests;

public class ProximityCalculatorTests
{
    [Fact]
    public void SamePoint_ReturnsZeroDistance()
    {
        var dist = ProximityCalculator.DistanceNm(51.28, -0.78, 51.28, -0.78);
        Assert.Equal(0, dist, precision: 5);
    }

    [Fact]
    public void OneDegreeLatitude_IsApprox60Nm()
    {
        var dist = ProximityCalculator.DistanceNm(51.0, 0, 52.0, 0);
        Assert.InRange(dist, 59.5, 60.5);
    }

    [Fact]
    public void ClockPosition_DirectlyAhead_Is12()
    {
        var clock = ProximityCalculator.ToClockPosition(0, 0);
        Assert.Equal(12, clock);
    }

    [Fact]
    public void ClockPosition_ToTheRight_Is3()
    {
        var clock = ProximityCalculator.ToClockPosition(0, 90);
        Assert.Equal(3, clock);
    }

    [Fact]
    public void ClockPosition_Behind_Is6()
    {
        var clock = ProximityCalculator.ToClockPosition(0, 180);
        Assert.Equal(6, clock);
    }
}
