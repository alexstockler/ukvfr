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
    private readonly CircuitLegDetector _circuitDetector;
    private readonly AirspaceDatabase _airspaceDb;
    private readonly AtcEngine _atcEngine;
    private readonly PhraseologyEngine _phraseology;
    private readonly AtisGenerator _atisGenerator;
    private readonly VoicePipeline _voicePipeline;
    private readonly VoiceSettings _voiceSettings;

    private AtcUnit? _activeUnit;
    private Aerodrome? _activeAerodrome;
    private WeatherData _latestWeather = new();
    private bool _isSubscribed;
    private bool _populatingAerodromes;

    private static readonly Brush PilotBrush = new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA));
    private static readonly Brush AtcBrush = new SolidColorBrush(Color.FromRgb(0xA6, 0xE3, 0xA1));
    private static readonly Brush AtisBrush = new SolidColorBrush(Color.FromRgb(0xF9, 0xE2, 0xAF));
    private static readonly Brush TextBrush = new SolidColorBrush(Color.FromRgb(0xCD, 0xD6, 0xF4));

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
        _circuitDetector = new CircuitLegDetector();
        _airspaceDb = new AirspaceDatabase();
        _phraseology = new PhraseologyEngine();
        _atcEngine = new AtcEngine(_phraseology);
        _atisGenerator = new AtisGenerator();

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
            return new SimConnectBridge(pollRateHz: settings.PollRateHz);
        }
        catch
        {
            return new SimulatedDataProvider();
        }
    }

    // ── Lifecycle ──────────────────────────────────────

    private async void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        // Load aerodrome data.
        var dataPath = Path.Combine(AppContext.BaseDirectory, "data", "aerodromes");
        await _airspaceDb.LoadAerodromesAsync(dataPath);

        // Populate aerodrome selector.
        PopulateAerodromeSelector();

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

    // ── Aerodrome selector ─────────────────────────────

    private void PopulateAerodromeSelector()
    {
        _populatingAerodromes = true;

        CmbAerodrome.Items.Clear();
        foreach (var ad in _airspaceDb.Aerodromes.Values.OrderBy(a => a.Name))
        {
            CmbAerodrome.Items.Add(new ComboBoxItem
            {
                Content = $"{ad.Icao} — {ad.Name}",
                Tag = ad.Icao
            });
        }

        // Default to Denham if available, else first in list.
        var defaultIcao = _airspaceDb.Aerodromes.ContainsKey("EGLD") ? "EGLD" : null;
        defaultIcao ??= _airspaceDb.Aerodromes.Keys.FirstOrDefault();

        for (var i = 0; i < CmbAerodrome.Items.Count; i++)
        {
            if (((ComboBoxItem)CmbAerodrome.Items[i]).Tag as string == defaultIcao)
            {
                CmbAerodrome.SelectedIndex = i;
                break;
            }
        }

        _populatingAerodromes = false;

        // Trigger initial selection.
        if (defaultIcao is not null)
            SelectAerodrome(defaultIcao);
    }

    private void OnAerodromeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_populatingAerodromes) return;
        if (CmbAerodrome.SelectedItem is ComboBoxItem item && item.Tag is string icao)
            SelectAerodrome(icao);
    }

    private void SelectAerodrome(string icao)
    {
        if (!_airspaceDb.Aerodromes.TryGetValue(icao, out var aerodrome))
            return;

        _activeAerodrome = aerodrome;
        aerodrome.ActiveRunway = aerodrome.Runways.FirstOrDefault();

        // Determine the primary ATC unit for this aerodrome.
        var (callsign, unitType, freqMhz) = DetermineUnit(aerodrome);

        _activeUnit = new AtcUnit
        {
            Callsign = callsign,
            Type = unitType,
            FrequencyMhz = freqMhz,
            ServicesAvailable = aerodrome.ServicesAvailable,
            Aerodrome = aerodrome
        };

        // Reset ATC state for new aerodrome.
        _atcEngine.Reset();
        _phaseDetector.Reset();

        // Update UI.
        var rwy = aerodrome.ActiveRunway;
        var rwyInfo = rwy is not null
            ? $"RWY {rwy.Designator} ({rwy.CircuitDirection} circuit, {rwy.CircuitHeightQfeFt}ft QFE)"
            : "No runway data";
        ActiveUnitText.Text = $"{callsign} — {freqMhz:F3} MHz\n{rwyInfo}";

        AddTranscriptEntry("SYSTEM",
            $"Selected {aerodrome.Name} ({aerodrome.Icao}). {callsign}: {freqMhz:F3} MHz",
            Brushes.Gray);
    }

    private static (string Callsign, AtcUnitType Type, double FreqMhz) DetermineUnit(Aerodrome ad)
    {
        // Prefer Tower → Approach → Radar → Radio, matching real-world priority.
        if (ad.UnitCallsigns.Tower is not null && ad.Frequencies.Tower.HasValue)
            return (ad.UnitCallsigns.Tower, AtcUnitType.Tower, ad.Frequencies.Tower.Value);

        if (ad.UnitCallsigns.Approach is not null && ad.Frequencies.Approach.HasValue)
            return (ad.UnitCallsigns.Approach, AtcUnitType.Approach, ad.Frequencies.Approach.Value);

        if (ad.UnitCallsigns.Radar is not null && ad.Frequencies.Radar.HasValue)
            return (ad.UnitCallsigns.Radar, AtcUnitType.Radar, ad.Frequencies.Radar.Value);

        if (ad.UnitCallsigns.Radio is not null && ad.Frequencies.Radio.HasValue)
            return (ad.UnitCallsigns.Radio, AtcUnitType.Tower, ad.Frequencies.Radio.Value);

        return ($"{ad.Name} Radio", AtcUnitType.Tower, 122.5);
    }

    // ── Sim snapshot handling ──────────────────────────

    private void OnSnapshotUpdated(object? sender, SimSnapshot snapshot)
    {
        var phase = _phaseDetector.Update(snapshot);

        // Detect circuit leg if in circuit phase.
        var circuitLeg = CircuitLeg.None;
        if (phase == FlightPhase.Circuit && _activeAerodrome?.ActiveRunway is not null)
            circuitLeg = _circuitDetector.Detect(snapshot, _activeAerodrome.ActiveRunway);

        // Update weather from sim data.
        _latestWeather = new WeatherData
        {
            WindDirectionDeg = (int)snapshot.WindDirectionDeg,
            WindSpeedKt = (int)snapshot.WindSpeedKt,
            VisibilityMetres = (int)snapshot.VisibilityMetres,
            TemperatureC = (int)snapshot.TemperatureC,
            DewpointC = (int)snapshot.DewpointC,
            QnhHpa = (int)snapshot.BarometerHpa
        };

        // Check if COM1 frequency matches a known unit.
        var freqMatch = _airspaceDb.FindByFrequency(snapshot.Com1ActiveHz);

        Dispatcher.Invoke(() =>
        {
            var circuitStr = circuitLeg != CircuitLeg.None ? $" ({circuitLeg})" : "";
            StateTextBox.Text =
                $"Phase   : {phase}{circuitStr}\r\n" +
                $"Lat/Lon : {snapshot.LatitudeDegrees:F4}, {snapshot.LongitudeDegrees:F4}\r\n" +
                $"Alt     : {snapshot.AltitudeFeet:F0} ft | VS: {snapshot.VerticalSpeedFpm:+0;-0} fpm\r\n" +
                $"Heading : {snapshot.HeadingDegrees:F0}\u00b0 | GS: {snapshot.GroundSpeedKnots:F1} kt\r\n" +
                $"COM1    : {snapshot.Com1ActiveHz / 1_000_000:F3} MHz" +
                    (freqMatch.HasValue ? $" [{freqMatch.Value.UnitType}]" : "") + "\r\n" +
                $"COM2    : {snapshot.Com2ActiveHz / 1_000_000:F3} MHz\r\n" +
                $"Squawk  : {snapshot.TransponderCode:D4}\r\n" +
                $"Wind    : {snapshot.WindDirectionDeg:000}/{snapshot.WindSpeedKt:F0}kt | QNH {snapshot.BarometerHpa:F0}";

            StatusText.Text = $"Connected | Phase: {phase}{circuitStr} | ATC: {_atcEngine.State}";
        });
    }

    // ── Menu-driven pilot input handlers ───────────────

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
        AddTranscriptEntry("PILOT", $"{_activeUnit.Callsign}, Golf Alpha Bravo Charlie Delta", PilotBrush);
        await _voicePipeline.ProcessMenuInputAsync(intent, _activeUnit);
    }

    private async void OnMenuPassMessage(object sender, RoutedEventArgs e)
    {
        if (_activeUnit is null) return;
        var callsign = _atcEngine.PilotCallsign ?? "Golf Alpha Bravo Charlie Delta";
        var intent = new PilotIntent
        {
            Type = PilotIntentType.PassMessage,
            Callsign = callsign,
            Position = "overhead",
            Altitude = "2000 feet",
            ParserName = "Menu"
        };
        AddTranscriptEntry("PILOT",
            $"{callsign}, VFR from the north, 2000 feet, request joining instructions", PilotBrush);
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
        AddTranscriptEntry("PILOT", "Request joining instructions", PilotBrush);
        await _voicePipeline.ProcessMenuInputAsync(intent, _activeUnit);
    }

    private async void OnMenuDownwind(object sender, RoutedEventArgs e)
    {
        if (_activeUnit is null) return;
        var cs = _atcEngine.PilotCallsign ?? "Golf Charlie Delta";
        var intent = new PilotIntent
        {
            Type = PilotIntentType.PositionReport,
            Position = "downwind",
            Callsign = _atcEngine.PilotCallsign,
            ParserName = "Menu"
        };
        AddTranscriptEntry("PILOT", $"{cs}, downwind", PilotBrush);
        await _voicePipeline.ProcessMenuInputAsync(intent, _activeUnit);
    }

    private async void OnMenuFinal(object sender, RoutedEventArgs e)
    {
        if (_activeUnit is null) return;
        var cs = _atcEngine.PilotCallsign ?? "Golf Charlie Delta";
        var intent = new PilotIntent
        {
            Type = PilotIntentType.PositionReport,
            Position = "final",
            Callsign = _atcEngine.PilotCallsign,
            ParserName = "Menu"
        };
        AddTranscriptEntry("PILOT", $"{cs}, final", PilotBrush);
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
        AddTranscriptEntry("PILOT", "Readback...", PilotBrush);
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
        AddTranscriptEntry("PILOT", "Roger", PilotBrush);
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
        AddTranscriptEntry("PILOT", "Request frequency change", PilotBrush);
        await _voicePipeline.ProcessMenuInputAsync(intent, _activeUnit);
    }

    private void OnMenuAtis(object sender, RoutedEventArgs e)
    {
        if (_activeAerodrome?.ActiveRunway is null) return;

        var atisText = _atisGenerator.Generate(_activeAerodrome, _latestWeather, _activeAerodrome.ActiveRunway);
        AddTranscriptEntry("ATIS", atisText, AtisBrush);
        _atisGenerator.AdvanceLetter();
    }

    // ── Pipeline event handlers ────────────────────────

    private void OnAtcResponded(object? sender, AtcResponse response)
    {
        if (string.IsNullOrEmpty(response.Text)) return;
        Dispatcher.Invoke(() => AddTranscriptEntry("ATC", response.Text, AtcBrush));
    }

    private void OnPilotTranscribed(object? sender, TranscriptionResult result)
    {
        if (result.IsEmpty) return;
        Dispatcher.Invoke(() => AddTranscriptEntry("PILOT (voice)", result.Text, PilotBrush));
    }

    // ── Transcript UI ──────────────────────────────────

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
        item.Inlines.Add(new Run(message) { Foreground = TextBrush });

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
