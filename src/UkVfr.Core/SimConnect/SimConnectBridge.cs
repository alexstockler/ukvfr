using System;
using System.Timers;

namespace UkVfr.Core.SimConnect;

/// <summary>
/// Minimal placeholder \"SimConnect\" bridge.
/// For now this generates fake aircraft data on a timer so we can
/// exercise the UI and data flow without needing the MSFS SDK set up.
/// Later we will replace the internals with real SimConnect calls.
/// </summary>
public class SimConnectBridge : IDisposable
{
    private readonly System.Timers.Timer _timer;
    private bool _isConnected;

    private double _lat = 51.0000;
    private double _lon = -0.5000;
    private double _altFeet = 800;
    private double _heading = 90;
    private double _gsKnots = 90;
    private bool _onGround = false;

    /// <summary>
    /// Raised whenever new (currently simulated) data is available.
    /// </summary>
    public event EventHandler<SimSnapshot>? SnapshotUpdated;

    public SimConnectBridge()
    {
        _timer = new System.Timers.Timer(200); // 5 Hz
        _timer.Elapsed += (_, _) => GenerateSnapshot();
    }

    /// <summary>
    /// \"Connect\" to the simulator.
    /// Right now this just starts the timer; once the MSFS SDK is wired in,
    /// this method will perform the real SimConnect connection.
    /// </summary>
    public Task ConnectAsync()
    {
        if (_isConnected)
        {
            return Task.CompletedTask;
        }

        _isConnected = true;
        _timer.Start();
        return Task.CompletedTask;
    }

    private void GenerateSnapshot()
    {
        if (!_isConnected)
        {
            return;
        }

        // Very simple fake movement: slowly change lat/lon/alt/heading.
        _lat += 0.0001;
        _lon += 0.0001;
        _altFeet += 5;
        _heading = (_heading + 1) % 360;

        var snapshot = new SimSnapshot(
            LatitudeDegrees: _lat,
            LongitudeDegrees: _lon,
            AltitudeFeet: _altFeet,
            HeadingDegrees: _heading,
            GroundSpeedKnots: _gsKnots,
            OnGround: _onGround,
            Com1ActiveHz: 118_200_000, // 118.200 MHz
            Com2ActiveHz: 122_500_000  // 122.500 MHz
        );

        SnapshotUpdated?.Invoke(this, snapshot);
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
        _isConnected = false;
    }
}

/// <summary>
/// Simple DTO representing the basic sim snapshot we care about for Phase 1.
/// </summary>
public record SimSnapshot(
    double LatitudeDegrees,
    double LongitudeDegrees,
    double AltitudeFeet,
    double HeadingDegrees,
    double GroundSpeedKnots,
    bool OnGround,
    double Com1ActiveHz,
    double Com2ActiveHz
);

