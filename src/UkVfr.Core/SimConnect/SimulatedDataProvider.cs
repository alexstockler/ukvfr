namespace UkVfr.Core.SimConnect;

/// <summary>
/// Generates simulated aircraft data for testing without MSFS.
/// Produces slowly-changing position/altitude data at 5 Hz so
/// the UI and data flow can be exercised on any machine.
/// </summary>
public sealed class SimulatedDataProvider : ISimDataProvider
{
    private readonly System.Timers.Timer _timer;
    private bool _isConnected;

    private double _lat = 51.2758;   // Start near EGLF
    private double _lon = -0.7764;
    private double _altFeet = 800;
    private double _heading = 243;   // Runway 24 heading
    private double _gsKnots = 90;
    private double _vsFpm = 0;
    private bool _onGround = true;  // Start on ground

    public event EventHandler<SimSnapshot>? SnapshotUpdated;
    public event EventHandler<bool>? ConnectionChanged;

    public bool IsConnected => _isConnected;

    public SimulatedDataProvider()
    {
        _timer = new System.Timers.Timer(200); // 5 Hz
        _timer.Elapsed += (_, _) => GenerateSnapshot();
    }

    public Task ConnectAsync(CancellationToken ct = default)
    {
        if (_isConnected) return Task.CompletedTask;

        _isConnected = true;
        _timer.Start();
        ConnectionChanged?.Invoke(this, true);
        return Task.CompletedTask;
    }

    public void Disconnect()
    {
        if (!_isConnected) return;
        _timer.Stop();
        _isConnected = false;
        ConnectionChanged?.Invoke(this, false);
    }

    private void GenerateSnapshot()
    {
        if (!_isConnected) return;

        // Simulate gentle flight near Farnborough.
        _lat += 0.00005;
        _lon += 0.00005;
        _altFeet += 2;
        _heading = (_heading + 0.5) % 360;

        var snapshot = new SimSnapshot(
            LatitudeDegrees: _lat,
            LongitudeDegrees: _lon,
            AltitudeFeet: _altFeet,
            HeadingDegrees: _heading,
            GroundSpeedKnots: _gsKnots,
            VerticalSpeedFpm: _vsFpm,
            OnGround: _onGround,
            Com1ActiveHz: 122_500_000,  // Farnborough Tower
            Com2ActiveHz: 134_350_000,  // Farnborough Approach
            TransponderCode: 7000,
            WindDirectionDeg: 240,
            WindSpeedKt: 10,
            VisibilityMetres: 10000,
            TemperatureC: 15,
            DewpointC: 10,
            BarometerHpa: 1013
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
/// Snapshot of aircraft and environment state from the simulator.
/// </summary>
public record SimSnapshot(
    double LatitudeDegrees,
    double LongitudeDegrees,
    double AltitudeFeet,
    double HeadingDegrees,
    double GroundSpeedKnots,
    double VerticalSpeedFpm = 0,
    bool OnGround = false,
    double Com1ActiveHz = 122_500_000,
    double Com2ActiveHz = 134_350_000,
    int TransponderCode = 7000,
    double WindDirectionDeg = 0,
    double WindSpeedKt = 0,
    double VisibilityMetres = 10000,
    double TemperatureC = 15,
    double DewpointC = 10,
    double BarometerHpa = 1013
);
