using UkVfr.Core.Airspace;
using UkVfr.Core.SimConnect;

namespace UkVfr.Core.Tracking;

/// <summary>
/// Determines which leg of the circuit the aircraft is on, based on
/// its heading relative to the active runway and circuit direction.
/// </summary>
public sealed class CircuitLegDetector
{
    private const double HeadingToleranceDeg = 30;

    /// <summary>
    /// Detect the circuit leg based on aircraft heading relative to the runway.
    /// Only meaningful when the aircraft is within the circuit pattern.
    /// </summary>
    public CircuitLeg Detect(SimSnapshot snapshot, Runway runway)
    {
        var rwyHdg = runway.HeadingMag;
        var acftHdg = snapshot.HeadingDegrees;

        var reciprocal = NormaliseHeading(rwyHdg + 180);

        // Determine crosswind/base heading based on circuit direction.
        double crosswindHdg, baseHdg;
        if (runway.CircuitDirection == CircuitDirection.Left)
        {
            crosswindHdg = NormaliseHeading(rwyHdg - 90);
            baseHdg = NormaliseHeading(rwyHdg + 90);
        }
        else
        {
            crosswindHdg = NormaliseHeading(rwyHdg + 90);
            baseHdg = NormaliseHeading(rwyHdg - 90);
        }

        if (IsWithin(acftHdg, rwyHdg, HeadingToleranceDeg))
            return CircuitLeg.Upwind;

        if (IsWithin(acftHdg, crosswindHdg, HeadingToleranceDeg))
            return CircuitLeg.Crosswind;

        if (IsWithin(acftHdg, reciprocal, HeadingToleranceDeg))
            return CircuitLeg.Downwind;

        if (IsWithin(acftHdg, baseHdg, HeadingToleranceDeg))
            return CircuitLeg.Base;

        // If heading is close to runway heading and descending, likely final.
        if (IsWithin(acftHdg, rwyHdg, HeadingToleranceDeg + 10))
            return CircuitLeg.Final;

        return CircuitLeg.None;
    }

    private static double NormaliseHeading(double hdg)
    {
        return ((hdg % 360) + 360) % 360;
    }

    private static bool IsWithin(double heading, double target, double tolerance)
    {
        var diff = Math.Abs(NormaliseHeading(heading - target));
        return diff <= tolerance || diff >= 360 - tolerance;
    }
}
