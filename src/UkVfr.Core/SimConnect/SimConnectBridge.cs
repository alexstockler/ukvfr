namespace UkVfr.Core.SimConnect;

/// <summary>
/// Real SimConnect bridge for Microsoft Flight Simulator.
/// Connects via the MSFS SDK managed assembly to read aircraft state,
/// COM frequencies, transponder, and weather data.
///
/// Requirements:
/// - MSFS 2020/2024 must be running
/// - Microsoft.FlightSimulator.SimConnect.dll must be accessible
///   (installed with the MSFS SDK, or referenced via the FsConnect NuGet)
/// - Windows only (SimConnect uses COM interop)
///
/// When the SimConnect DLL is not available (e.g. dev machine without MSFS),
/// use <see cref="SimulatedDataProvider"/> instead.
/// </summary>
public sealed class SimConnectBridge : ISimDataProvider
{
    private readonly string _appName;
    private readonly TimeSpan _pollInterval;
    private dynamic? _simConnect;
    private System.Timers.Timer? _pollTimer;
    private bool _isConnected;
    private bool _disposed;

    public event EventHandler<SimSnapshot>? SnapshotUpdated;
    public event EventHandler<bool>? ConnectionChanged;

    public bool IsConnected => _isConnected;

    public SimConnectBridge(string appName = "UkVfrAtc", int pollRateHz = 5)
    {
        _appName = appName;
        _pollInterval = TimeSpan.FromMilliseconds(1000.0 / pollRateHz);
    }

    public Task ConnectAsync(CancellationToken ct = default)
    {
        if (_isConnected) return Task.CompletedTask;

        // Attempt to load the SimConnect managed assembly dynamically.
        // This avoids a hard compile-time dependency on the MSFS SDK.
        var scType = LoadSimConnectType();
        if (scType is null)
            throw new InvalidOperationException(
                "SimConnect SDK not found. Install the MSFS SDK or use SimulatedDataProvider for testing.");

        try
        {
            // Create SimConnect instance: SimConnect(name, hWnd, userEventId, eventHandle, configIndex)
            _simConnect = Activator.CreateInstance(scType, _appName, IntPtr.Zero, 0u, null, 0u);

            RegisterDataDefinition();
            SetupPolling();

            _isConnected = true;
            ConnectionChanged?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to connect to SimConnect: {ex.Message}. Is MSFS running?", ex);
        }

        return Task.CompletedTask;
    }

    public void Disconnect()
    {
        if (!_isConnected) return;

        _pollTimer?.Stop();
        _pollTimer?.Dispose();
        _pollTimer = null;

        try { _simConnect?.Dispose(); }
        catch { /* Ignore cleanup errors. */ }

        _simConnect = null;
        _isConnected = false;
        ConnectionChanged?.Invoke(this, false);
    }

    private void RegisterDataDefinition()
    {
        if (_simConnect is null) return;

        // Register each sim variable we want to read.
        // Data types: FLOAT64 = 4, INT32 = 1 in SIMCONNECT_DATATYPE enum.
        AddDatum("PLANE LATITUDE", "degrees");
        AddDatum("PLANE LONGITUDE", "degrees");
        AddDatum("PLANE ALTITUDE", "feet");
        AddDatum("PLANE HEADING DEGREES MAGNETIC", "degrees");
        AddDatum("GROUND VELOCITY", "knots");
        AddDatum("VERTICAL SPEED", "feet per minute");
        AddDatum("SIM ON GROUND", "bool");
        AddDatum("COM ACTIVE FREQUENCY:1", "Hz");
        AddDatum("COM ACTIVE FREQUENCY:2", "Hz");
        AddDatum("TRANSPONDER CODE:1", "number");
        AddDatum("AMBIENT WIND DIRECTION", "degrees");
        AddDatum("AMBIENT WIND VELOCITY", "knots");
        AddDatum("AMBIENT VISIBILITY", "meters");
        AddDatum("AMBIENT TEMPERATURE", "celsius");
        AddDatum("AMBIENT DEWPOINT TEMPERATURE", "celsius");
        AddDatum("BAROMETER PRESSURE", "hectopascals");
    }

    private void AddDatum(string datumName, string units)
    {
        try
        {
            // Using dynamic invocation to avoid hard dependency on SimConnect types.
            _simConnect?.AddToDataDefinition(1, datumName, units, 4 /* FLOAT64 */, 0f, 0u);
        }
        catch
        {
            // If individual datum registration fails, continue with others.
        }
    }

    private void SetupPolling()
    {
        _pollTimer = new System.Timers.Timer(_pollInterval.TotalMilliseconds);
        _pollTimer.Elapsed += (_, _) => RequestData();
        _pollTimer.AutoReset = true;
        _pollTimer.Start();
    }

    private void RequestData()
    {
        if (!_isConnected || _simConnect is null) return;

        try
        {
            // RequestDataOnSimObject: requestId=1, defineId=1, objectId=0 (user aircraft), period=ONCE
            _simConnect.RequestDataOnSimObject(1, 1, 0u, 0 /* ONCE */, 0u, 0u, 0u, 0u);
            _simConnect.ReceiveMessage();
        }
        catch
        {
            // Connection may have been lost.
            _isConnected = false;
            ConnectionChanged?.Invoke(this, false);
        }
    }

    /// <summary>
    /// Attempts to load the SimConnect type from the MSFS SDK assembly.
    /// Returns null if the SDK is not installed.
    /// </summary>
    private static Type? LoadSimConnectType()
    {
        try
        {
            var asm = System.Reflection.Assembly.Load("Microsoft.FlightSimulator.SimConnect");
            return asm.GetType("Microsoft.FlightSimulator.SimConnect.SimConnect");
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Disconnect();
    }
}

/// <summary>
/// Configuration for SimConnect connection behaviour.
/// </summary>
public sealed class SimConnectSettings
{
    /// <summary>Use simulated data instead of real SimConnect (for testing without MSFS).</summary>
    public bool UseSimulatedData { get; init; }

    /// <summary>Data polling rate in Hz (default 5).</summary>
    public int PollRateHz { get; init; } = 5;
}
