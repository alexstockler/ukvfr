using UkVfr.Core.SimConnect;
using UkVfr.Core.Tracking;
using Xunit;

namespace UkVfr.Core.Tests;

public class FlightPhaseDetectorTests
{
    private static SimSnapshot MakeSnapshot(
        double groundSpeedKt = 0,
        double altFt = 0,
        bool onGround = true,
        double heading = 0) => new(
        LatitudeDegrees: 51.28,
        LongitudeDegrees: -0.78,
        AltitudeFeet: altFt,
        HeadingDegrees: heading,
        GroundSpeedKnots: groundSpeedKt,
        OnGround: onGround);

    [Fact]
    public void InitialState_IsParked()
    {
        var detector = new FlightPhaseDetector();
        Assert.Equal(FlightPhase.Parked, detector.CurrentPhase);
    }

    [Fact]
    public void MovingOnGround_TransitionsToTaxiing()
    {
        var detector = new FlightPhaseDetector();
        detector.Update(MakeSnapshot(groundSpeedKt: 15, onGround: true));
        Assert.Equal(FlightPhase.Taxiing, detector.CurrentPhase);
    }

    [Fact]
    public void Airborne_TransitionsToTakingOff()
    {
        var detector = new FlightPhaseDetector();
        detector.Update(MakeSnapshot(groundSpeedKt: 15, onGround: true));
        Assert.Equal(FlightPhase.Taxiing, detector.CurrentPhase);

        detector.Update(MakeSnapshot(groundSpeedKt: 60, onGround: false, altFt: 50));
        Assert.Equal(FlightPhase.TakingOff, detector.CurrentPhase);
    }

    [Fact]
    public void Reset_ReturnsToParked()
    {
        var detector = new FlightPhaseDetector();
        detector.Update(MakeSnapshot(groundSpeedKt: 15, onGround: true));
        detector.Reset();
        Assert.Equal(FlightPhase.Parked, detector.CurrentPhase);
    }
}
