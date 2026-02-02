using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Extensions.Configuration;
using UkVfr.Core.Airspace;
using UkVfr.Core.Atc;
using UkVfr.Core.Configuration;
using UkVfr.Core.Phraseology;
using UkVfr.Core.SimConnect;
using UkVfr.Core.Tracking;
using UkVfr.Core.Voice;

namespace UkVfr.App;

public partial class MainWindow : Window
{
    private readonly ISimDataProvider _sim;
    private readonly FlightPhaseDetector _phaseDetector;
    private readonly AirspaceDatabase _airspaceDb;
    private readonly AtcEngine _atcEngine;
    private readonly PhraseologyEngine _phraseology;
    private readonly VoicePipeline _voicePipeline;
    private readonly VoiceSettings _voiceSettings;

    private AtcUnit? _activeUnit;
    private bool _isSubscribed;

    public MainWindow()
    {
        InitializeComponent();

        // Load configuration.
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        _voiceSettings = new VoiceSettings();
        config.GetSection("Voice").Bind(_voiceSettings);

        var simSettings = new SimConnectSettings();
        config.GetSection("SimConnect").Bind(simSettings);

        // Initialise components.
        _sim = CreateSimDataProvider(simSettings);
        _phaseDetector = new FlightPhaseDetector();
        _airspaceDb = new AirspaceDatabase();
        _phraseology = new PhraseologyEngine();
        _atcEngine = new AtcEngine(_phraseology);

        // Build voice pipeline from configuration (includes radio filter if enabled).
        var stt = ServiceFactory.CreateSttProvider(_voiceSettings);
        var tts = ServiceFactory.CreateTtsProvider(_voiceSettings);
        var intentParser = ServiceFactory.CreateIntentParser(_voiceSettings);
        var radioFilter = _voiceSettings.Tts.ApplyRadioFilter ? new RadioAudioFilter() : null;
        _voicePipeline = new VoicePipeline(stt, intentParser, tts, _atcEngine, radioFilter);

        // Subscribe to pipeline events.
        _voicePipeline.AtcResponded += OnAtcResponded;
        _voicePipeline.PilotTranscribed += OnPilotTranscribed;

        Loaded += OnWindowLoaded;
        Unloaded += OnWindowUnloaded;

        UpdateProviderStatus(stt, tts, intentParser);
    }

    private static ISimDataProvider CreateSimDataProvider(SimConnectSettings settings)
    {
        if (settings.UseSimulatedData)
            return new SimulatedDataProvider();

        try
        {
            var bridge = new SimConnectBridge(pollRateHz: settings.PollRateHz);
            return bridge;
        }
        catch
        {
            // SimConnect SDK not available — fall back to simulated data.
            return new SimulatedDataProvider();
        }
    }

    private async void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        // Load aerodrome data.
        var dataPath = Path.Combine(AppContext.BaseDirectory, "data", "aerodromes");
        await _airspaceDb.LoadAerodromesAsync(dataPath);

        // Set up default unit (Farnborough Tower for MVP).
        if (_airspaceDb.Aerodromes.TryGetValue("EGLF", out var eglf))
        {
            eglf.ActiveRunway = eglf.Runways.FirstOrDefault();
            _activeUnit = new AtcUnit
            {
                Callsign = eglf.UnitCallsigns.Tower ?? "Farnborough Tower",
                Type = AtcUnitType.Tower,
                FrequencyMhz = eglf.Frequencies.Tower ?? 122.5,
                ServicesAvailable = eglf.ServicesAvailable,
                Aerodrome = eglf
            };
            AddTranscriptEntry("SYSTEM", $"Loaded {eglf.Name} ({eglf.Icao}). Tower: {_activeUnit.FrequencyMhz:F3} MHz",
                Brushes.Gray);
        }

        // Connect to sim data provider.
        try
        {
            await _sim.ConnectAsync();
            if (IsLoaded)
            {
                _sim.SnapshotUpdated += OnSnapshotUpdated;
                _isSubscribed = true;
                var providerType = _sim is SimulatedDataProvider ? "Simulated" : "SimConnect";
                StatusText.Text = $"Connected ({providerType})";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"SimConnect error: {ex.Message}";
        }
    }

    private void OnWindowUnloaded(object sender, RoutedEventArgs e)
    {
        if (_isSubscribed)
        {
            _sim.SnapshotUpdated -= OnSnapshotUpdated;
            _isSubscribed = false;
        }
        _sim.Dispose();
    }

    private void OnSnapshotUpdated(object? sender, SimSnapshot snapshot)
    {
        var phase = _phaseDetector.Update(snapshot);

        Dispatcher.Invoke(() =>
        {
            StateTextBox.Text =
                $"Phase   : {phase}\r\n" +
                $"Lat/Lon : {snapshot.LatitudeDegrees:F4}, {snapshot.LongitudeDegrees:F4}\r\n" +
                $"Alt     : {snapshot.AltitudeFeet:F0} ft | VS: {snapshot.VerticalSpeedFpm:+0;-0} fpm\r\n" +
                $"Heading : {snapshot.HeadingDegrees:F0}\u00b0 | GS: {snapshot.GroundSpeedKnots:F1} kt\r\n" +
                $"COM1    : {snapshot.Com1ActiveHz / 1_000_000:F3} MHz\r\n" +
                $"COM2    : {snapshot.Com2ActiveHz / 1_000_000:F3} MHz\r\n" +
                $"Squawk  : {snapshot.TransponderCode:D4}\r\n" +
                $"Wind    : {snapshot.WindDirectionDeg:000}/{snapshot.WindSpeedKt:F0}kt | QNH {snapshot.BarometerHpa:F0}";

            StatusText.Text = $"Connected | Phase: {phase} | ATC: {_atcEngine.State}";
        });
    }

    // --- Menu-driven pilot input handlers ---

    private async void OnMenuInitialCall(object sender, RoutedEventArgs e)
    {
        if (_activeUnit is null) return;
        var intent = new PilotIntent
        {
            Type = PilotIntentType.InitialCall,
            Callsign = "Golf Alpha Bravo Charlie Delta",
            TargetUnit = _activeUnit.Callsign,
            ParserName = "Menu"
        };
        AddTranscriptEntry("PILOT", $"{_activeUnit.Callsign}, Golf Alpha Bravo Charlie Delta",
            new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA)));
        await _voicePipeline.ProcessMenuInputAsync(intent, _activeUnit);
    }

    private async void OnMenuPassMessage(object sender, RoutedEventArgs e)
    {
        if (_activeUnit is null) return;
        var intent = new PilotIntent
        {
            Type = PilotIntentType.PassMessage,
            Callsign = _atcEngine.PilotCallsign ?? "Golf Alpha Bravo Charlie Delta",
            Position = "overhead",
            Altitude = "2000 feet",
            ParserName = "Menu"
        };
        AddTranscriptEntry("PILOT",
            $"{_atcEngine.PilotCallsign ?? "Golf Alpha Bravo Charlie Delta"}, " +
            "VFR from the north, 2000 feet, request joining instructions",
            new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA)));
        await _voicePipeline.ProcessMenuInputAsync(intent, _activeUnit);
    }

    private async void OnMenuRequestJoin(object sender, RoutedEventArgs e)
    {
        if (_activeUnit is null) return;
        var intent = new PilotIntent
        {
            Type = PilotIntentType.JoinRequest,
            Callsign = _atcEngine.PilotCallsign,
            ParserName = "Menu"
        };
        AddTranscriptEntry("PILOT", "Request joining instructions",
            new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA)));
        await _voicePipeline.ProcessMenuInputAsync(intent, _activeUnit);
    }

    private async void OnMenuDownwind(object sender, RoutedEventArgs e)
    {
        if (_activeUnit is null) return;
        var intent = new PilotIntent
        {
            Type = PilotIntentType.PositionReport,
            Position = "downwind",
            Callsign = _atcEngine.PilotCallsign,
            ParserName = "Menu"
        };
        AddTranscriptEntry("PILOT",
            $"{_atcEngine.PilotCallsign ?? "Golf Charlie Delta"}, downwind",
            new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA)));
        await _voicePipeline.ProcessMenuInputAsync(intent, _activeUnit);
    }

    private async void OnMenuFinal(object sender, RoutedEventArgs e)
    {
        if (_activeUnit is null) return;
        var intent = new PilotIntent
        {
            Type = PilotIntentType.PositionReport,
            Position = "final",
            Callsign = _atcEngine.PilotCallsign,
            ParserName = "Menu"
        };
        AddTranscriptEntry("PILOT",
            $"{_atcEngine.PilotCallsign ?? "Golf Charlie Delta"}, final",
            new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA)));
        await _voicePipeline.ProcessMenuInputAsync(intent, _activeUnit);
    }

    private async void OnMenuReadback(object sender, RoutedEventArgs e)
    {
        if (_activeUnit is null) return;
        var intent = new PilotIntent
        {
            Type = PilotIntentType.Readback,
            Callsign = _atcEngine.PilotCallsign,
            ParserName = "Menu"
        };
        AddTranscriptEntry("PILOT", "Readback...",
            new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA)));
        await _voicePipeline.ProcessMenuInputAsync(intent, _activeUnit);
    }

    private async void OnMenuRoger(object sender, RoutedEventArgs e)
    {
        if (_activeUnit is null) return;
        var intent = new PilotIntent
        {
            Type = PilotIntentType.Acknowledgement,
            Callsign = _atcEngine.PilotCallsign,
            ParserName = "Menu"
        };
        AddTranscriptEntry("PILOT", "Roger",
            new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA)));
        await _voicePipeline.ProcessMenuInputAsync(intent, _activeUnit);
    }

    private async void OnMenuFreqChange(object sender, RoutedEventArgs e)
    {
        if (_activeUnit is null) return;
        var intent = new PilotIntent
        {
            Type = PilotIntentType.FrequencyChangeRequest,
            Callsign = _atcEngine.PilotCallsign,
            ParserName = "Menu"
        };
        AddTranscriptEntry("PILOT", "Request frequency change",
            new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA)));
        await _voicePipeline.ProcessMenuInputAsync(intent, _activeUnit);
    }

    // --- Pipeline event handlers ---

    private void OnAtcResponded(object? sender, AtcResponse response)
    {
        if (string.IsNullOrEmpty(response.Text)) return;
        Dispatcher.Invoke(() =>
        {
            AddTranscriptEntry("ATC", response.Text,
                new SolidColorBrush(Color.FromRgb(0xA6, 0xE3, 0xA1)));
        });
    }

    private void OnPilotTranscribed(object? sender, TranscriptionResult result)
    {
        if (result.IsEmpty) return;
        Dispatcher.Invoke(() =>
        {
            AddTranscriptEntry("PILOT (voice)", result.Text,
                new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA)));
        });
    }

    // --- Transcript UI ---

    private void AddTranscriptEntry(string sender, string message, Brush colour)
    {
        var time = DateTime.Now.ToString("HH:mm:ss");
        var item = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 2)
        };
        item.Inlines.Add(new Run($"[{time}] ") { Foreground = Brushes.Gray, FontSize = 10 });
        item.Inlines.Add(new Run($"{sender}: ") { Foreground = colour, FontWeight = FontWeights.SemiBold });
        item.Inlines.Add(new Run(message) { Foreground = new SolidColorBrush(Color.FromRgb(0xCD, 0xD6, 0xF4)) });

        TranscriptList.Items.Add(item);
        TranscriptList.ScrollIntoView(item);
    }

    private void UpdateProviderStatus(ISttProvider stt, ITtsProvider tts, IIntentParser intent)
    {
        ProviderStatusText.Text =
            $"STT: {stt.Name} ({(stt.IsAvailable ? "ready" : "unavailable")})\n" +
            $"TTS: {tts.Name} ({(tts.IsAvailable ? "ready" : "unavailable")})\n" +
            $"Intent: {intent.Name}";
    }

    private void OnOpenSettings(object sender, RoutedEventArgs e)
    {
        var settings = new SettingsWindow { Owner = this };
        settings.ShowDialog();
    }
}
