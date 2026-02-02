namespace UkVfr.Core.Tracking;

public enum FlightPhase
{
    Parked,
    Taxiing,
    HoldingPoint,
    TakingOff,
    Climbing,
    Cruise,
    Descending,
    Circuit,
    GoAround,
    Landed
}

public enum CircuitLeg
{
    None,
    Upwind,
    Crosswind,
    Downwind,
    Base,
    Final
}
