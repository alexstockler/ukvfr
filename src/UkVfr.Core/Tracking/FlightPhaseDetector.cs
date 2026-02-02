using UkVfr.Core.SimConnect;

namespace UkVfr.Core.Tracking;

/// <summary>
/// Determines the current flight phase from sim snapshot data.
/// Uses speed, altitude rate, ground contact, and proximity to aerodromes.
/// </summary>
public sealed class FlightPhaseDetector
{
    private const double ParkedSpeedKt = 5;
    private const double TakeoffSpeedKt = 40;
    private const double ClimbRateFpm = 200;
    private const double DescentRateFpm = -200;

    private FlightPhase _currentPhase = FlightPhase.Parked;
    private double _previousAltFt;
    private DateTime _lastUpdate = DateTime.MinValue;

    public FlightPhase CurrentPhase => _currentPhase;

    /// <summary>
    /// Update the detected phase from a new sim snapshot.
    /// Returns the new phase (may be unchanged).
    /// </summary>
    public FlightPhase Update(SimSnapshot snapshot)
    {
        var now = DateTime.UtcNow;
        var dt = _lastUpdate == DateTime.MinValue ? 0.2 : (now - _lastUpdate).TotalSeconds;
        var verticalRateFpm = dt > 0 ? (snapshot.AltitudeFeet - _previousAltFt) / dt * 60.0 : 0;

        _previousAltFt = snapshot.AltitudeFeet;
        _lastUpdate = now;

        var newPhase = _currentPhase switch
        {
            FlightPhase.Parked when snapshot.OnGround && snapshot.GroundSpeedKnots >= ParkedSpeedKt
                => FlightPhase.Taxiing,

            FlightPhase.Taxiing when snapshot.OnGround && snapshot.GroundSpeedKnots < ParkedSpeedKt
                => FlightPhase.Parked,
            FlightPhase.Taxiing when !snapshot.OnGround
                => FlightPhase.TakingOff,

            FlightPhase.TakingOff when verticalRateFpm > ClimbRateFpm
                => FlightPhase.Climbing,
            FlightPhase.TakingOff when snapshot.OnGround && snapshot.GroundSpeedKnots < ParkedSpeedKt
                => FlightPhase.Parked,

            FlightPhase.Climbing when verticalRateFpm < DescentRateFpm
                => FlightPhase.Descending,
            FlightPhase.Climbing when Math.Abs(verticalRateFpm) < ClimbRateFpm
                => FlightPhase.Cruise,

            FlightPhase.Cruise when verticalRateFpm > ClimbRateFpm
                => FlightPhase.Climbing,
            FlightPhase.Cruise when verticalRateFpm < DescentRateFpm
                => FlightPhase.Descending,

            FlightPhase.Descending when verticalRateFpm > ClimbRateFpm
                => FlightPhase.Climbing,
            FlightPhase.Descending when Math.Abs(verticalRateFpm) < ClimbRateFpm
                => FlightPhase.Cruise,
            FlightPhase.Descending when snapshot.OnGround
                => FlightPhase.Landed,

            FlightPhase.Landed when snapshot.OnGround && snapshot.GroundSpeedKnots < ParkedSpeedKt
                => FlightPhase.Parked,
            FlightPhase.Landed when !snapshot.OnGround
                => FlightPhase.GoAround,

            FlightPhase.GoAround when verticalRateFpm > ClimbRateFpm
                => FlightPhase.Climbing,

            _ => _currentPhase
        };

        _currentPhase = newPhase;
        return newPhase;
    }

    public void Reset()
    {
        _currentPhase = FlightPhase.Parked;
        _previousAltFt = 0;
        _lastUpdate = DateTime.MinValue;
    }
}
