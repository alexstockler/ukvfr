namespace UkVfr.Core.SimConnect;

/// <summary>
/// Abstraction over sim data sources. Allows the real SimConnect bridge
/// and a simulated data provider to be used interchangeably.
/// </summary>
public interface ISimDataProvider : IDisposable
{
    /// <summary>Raised whenever new aircraft state data is available.</summary>
    event EventHandler<SimSnapshot>? SnapshotUpdated;

    /// <summary>Raised when the connection state changes.</summary>
    event EventHandler<bool>? ConnectionChanged;

    bool IsConnected { get; }

    Task ConnectAsync(CancellationToken ct = default);
    void Disconnect();
}
