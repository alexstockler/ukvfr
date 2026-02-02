using System.Windows;
using System.Windows.Threading;
using UkVfr.Core.SimConnect;

namespace UkVfr.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly SimConnectBridge _simConnectBridge;
    private bool _isSubscribed;

    public MainWindow()
    {
        InitializeComponent();

        _simConnectBridge = new SimConnectBridge();

        Loaded += async (_, _) =>
        {
            try
            {
                await _simConnectBridge.ConnectAsync();
                
                // Only subscribe if window is still loaded and not disposed
                if (IsLoaded)
                {
                    _simConnectBridge.SnapshotUpdated += OnSnapshotUpdated;
                    _isSubscribed = true;
                    StateTextBox.Text = "Connected to MSFS via SimConnect. Waiting for data...";
                }
            }
            catch (Exception ex)
            {
                StateTextBox.Text =
                    $"Could not connect to MSFS.\r\nIs the simulator running?\r\n\r\nDetails: {ex.Message}";
            }
        };

        Unloaded += (_, _) =>
        {
            // Only unsubscribe if we actually subscribed
            if (_isSubscribed)
            {
                _simConnectBridge.SnapshotUpdated -= OnSnapshotUpdated;
                _isSubscribed = false;
            }
            _simConnectBridge.Dispose();
        };
    }

    private void OnSnapshotUpdated(object? sender, SimSnapshot snapshot)
    {
        // Ensure we update the UI on the WPF UI thread.
        Dispatcher.Invoke(() =>
        {
            StateTextBox.Text =
                $"Time: {DateTime.Now:HH:mm:ss}\r\n" +
                $"Lat/Lon : {snapshot.LatitudeDegrees:F4}, {snapshot.LongitudeDegrees:F4}\r\n" +
                $"Alt     : {snapshot.AltitudeFeet:F0} ft\r\n" +
                $"Heading : {snapshot.HeadingDegrees:F0}°\r\n" +
                $"GS      : {snapshot.GroundSpeedKnots:F1} kt\r\n" +
                $"OnGround: {snapshot.OnGround}\r\n" +
                $"COM1    : {snapshot.Com1ActiveHz / 1_000_000:F3} MHz\r\n" +
                $"COM2    : {snapshot.Com2ActiveHz / 1_000_000:F3} MHz";
        });
    }
}