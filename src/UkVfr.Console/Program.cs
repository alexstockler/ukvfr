using UkVfr.Core.Airspace;
using UkVfr.Core.Atc;
using UkVfr.Core.Phraseology;
using UkVfr.Core.SimConnect;
using UkVfr.Core.Tracking;
using UkVfr.Core.Voice;
using UkVfr.Core.Voice.Providers;

Console.ForegroundColor = ConsoleColor.White;
Console.WriteLine("========================================");
Console.WriteLine("  UK VFR ATC — Console Test Harness");
Console.WriteLine("========================================");
Console.WriteLine();

// ──────────────────────────────────────
// 1. Load aerodrome data
// ──────────────────────────────────────
var db = new AirspaceDatabase();
var dataPath = Path.Combine(AppContext.BaseDirectory, "data", "aerodromes");
await db.LoadAerodromesAsync(dataPath);

if (!db.Aerodromes.TryGetValue("EGLF", out var eglf))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"ERROR: Could not load EGLF.json from {dataPath}");
    Console.WriteLine("Make sure the data/aerodromes/ folder is copied to the output directory.");
    return;
}

eglf.ActiveRunway = eglf.Runways.FirstOrDefault();

WriteSection("Aerodrome loaded");
Console.WriteLine($"  {eglf.Name} ({eglf.Icao})");
Console.WriteLine($"  Elevation: {eglf.ElevationFt} ft  |  Position: {eglf.LatitudeDeg:F4}N, {eglf.LongitudeDeg:F4}W");
Console.WriteLine($"  Tower: {eglf.Frequencies.Tower:F3} MHz");
Console.WriteLine($"  Active runway: {eglf.ActiveRunway?.Designator ?? "none"} " +
    $"({eglf.ActiveRunway?.CircuitDirection} circuit, {eglf.ActiveRunway?.CircuitHeightQfeFt} ft QFE)");

// ──────────────────────────────────────
// 2. Set up ATC unit and engine
// ──────────────────────────────────────
var unit = new AtcUnit
{
    Callsign = eglf.UnitCallsigns.Tower ?? "Farnborough Tower",
    Type = AtcUnitType.Tower,
    FrequencyMhz = eglf.Frequencies.Tower ?? 122.5,
    ServicesAvailable = eglf.ServicesAvailable,
    Aerodrome = eglf
};

var phraseology = new PhraseologyEngine();
var atcEngine = new AtcEngine(phraseology);

// ──────────────────────────────────────
// 3. Test callsign formatting
// ──────────────────────────────────────
WriteSection("Callsign Formatter");
var reg = "G-ABCD";
Console.WriteLine($"  Registration: {reg}");
Console.WriteLine($"  Full phonetic: {CallsignFormatter.ToFullPhonetic(reg)}");
Console.WriteLine($"  Abbreviated:   {CallsignFormatter.ToAbbreviated(reg)}");
Console.WriteLine($"  Squawk 4512:   {CallsignFormatter.SpokenDigits("4512")}");
Console.WriteLine($"  Runway 24:     {CallsignFormatter.SpokenRunway("24")}");

// ──────────────────────────────────────
// 4. Test intent parser on sample transmissions
// ──────────────────────────────────────
WriteSection("Intent Parser (Rule-Based)");
var parser = new RuleBasedIntentParser();
var idleCtx = new ConversationContext { ConversationState = "Idle" };
var awaitCtx = new ConversationContext
{
    ConversationState = "AwaitingPassMessage",
    PilotCallsign = "Golf Alpha Bravo Charlie Delta"
};

string[] testTranscripts =
[
    "Farnborough Tower, Golf Alpha Bravo Charlie Delta",
    "Golf Alpha Bravo Charlie Delta, PA28, VFR from Guildford, 2000 feet, request joining instructions",
    "Request joining instructions",
    "downwind",
    "final",
    "Roger",
    "Request frequency change",
    "Mayday mayday mayday",
    "Ready for departure",
    "Request traffic service",
    "asdfghjkl random noise"
];

foreach (var transcript in testTranscripts)
{
    var ctx = transcript.Contains("PA28") ? awaitCtx : idleCtx;
    var intent = await parser.ParseAsync(transcript, ctx);
    var conf = intent.Confidence.ToString("P0");
    Console.ForegroundColor = intent.Type == PilotIntentType.Unrecognised ? ConsoleColor.Red : ConsoleColor.Green;
    Console.Write($"  [{intent.Type,-25}]");
    Console.ForegroundColor = ConsoleColor.Gray;
    Console.Write($" ({conf}) ");
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine($"\"{transcript}\"");
}

// ──────────────────────────────────────
// 5. Walk through a full ATC conversation
// ──────────────────────────────────────
WriteSection("Full ATC Conversation");

var steps = new (PilotIntentType Type, string Callsign, string? Position, string Display)[]
{
    (PilotIntentType.InitialCall, "Golf Alpha Bravo Charlie Delta", null,
        "Farnborough Tower, Golf Alpha Bravo Charlie Delta"),
    (PilotIntentType.PassMessage, "Golf Alpha Bravo Charlie Delta", "overhead",
        "Golf Alpha Bravo Charlie Delta, PA28, VFR from Guildford, overhead at 2000 feet, request joining instructions"),
    (PilotIntentType.Readback, "Golf Alpha Bravo Charlie Delta", null,
        "Squawk [readback], join overhead runway two four, Golf Alpha Bravo Charlie Delta"),
    (PilotIntentType.PositionReport, "Golf Alpha Bravo Charlie Delta", "downwind",
        "Golf Charlie Delta, downwind"),
    (PilotIntentType.PositionReport, "Golf Alpha Bravo Charlie Delta", "final",
        "Golf Charlie Delta, final"),
    (PilotIntentType.Acknowledgement, "Golf Alpha Bravo Charlie Delta", null,
        "Golf Charlie Delta"),
    (PilotIntentType.FrequencyChangeRequest, "Golf Alpha Bravo Charlie Delta", null,
        "Golf Charlie Delta, request frequency change"),
};

foreach (var step in steps)
{
    // Show pilot transmission.
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.Write("  PILOT: ");
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine(step.Display);

    // Process through ATC engine.
    var intent = new PilotIntent
    {
        Type = step.Type,
        Callsign = step.Callsign,
        Position = step.Position,
        ParserName = "Console"
    };
    var response = atcEngine.ProcessIntent(intent, unit);

    if (!string.IsNullOrEmpty(response.Text))
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("  ATC:   ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(response.Text);
    }

    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"         [State: {atcEngine.State}" +
        (atcEngine.AssignedSquawk is not null ? $" | Squawk: {atcEngine.AssignedSquawk}" : "") +
        (atcEngine.ActiveService is not null ? $" | Service: {atcEngine.ActiveService}" : "") + "]");
    Console.WriteLine();
}

// ──────────────────────────────────────
// 6. Test ATIS generation
// ──────────────────────────────────────
WriteSection("ATIS Generation");
var atis = new AtisGenerator();
var weather = new WeatherData
{
    WindDirectionDeg = 240,
    WindSpeedKt = 12,
    WindGustKt = 20,
    VisibilityMetres = 8000,
    TemperatureC = 14,
    DewpointC = 9,
    QnhHpa = 1022,
    CloudLayers =
    [
        new CloudLayer { Coverage = CloudCoverage.Scattered, BaseFt = 2500 },
        new CloudLayer { Coverage = CloudCoverage.Broken, BaseFt = 5000 }
    ]
};

var atisText = atis.Generate(eglf, weather, eglf.ActiveRunway!);
Console.ForegroundColor = ConsoleColor.Yellow;
Console.Write("  ATIS:  ");
Console.ForegroundColor = ConsoleColor.White;
Console.WriteLine(atisText);
Console.WriteLine();

// ──────────────────────────────────────
// 7. Test flight phase detection
// ──────────────────────────────────────
WriteSection("Flight Phase Detection");
var detector = new FlightPhaseDetector();

(string Label, SimSnapshot Snap)[] flightStages =
[
    ("Parked at stand",
        new SimSnapshot(51.2758, -0.7764, 238, 243, 0, true, 122_500_000, 118_000_000)),
    ("Taxiing to runway",
        new SimSnapshot(51.2760, -0.7760, 238, 243, 12, true, 122_500_000, 118_000_000)),
    ("Airborne after takeoff",
        new SimSnapshot(51.2780, -0.7740, 400, 243, 75, false, 122_500_000, 118_000_000)),
    ("Climbing through 800ft",
        new SimSnapshot(51.2800, -0.7720, 800, 243, 85, false, 122_500_000, 118_000_000)),
    ("Level at circuit height",
        new SimSnapshot(51.2820, -0.7700, 1238, 153, 90, false, 122_500_000, 118_000_000)),
    ("Descending on final",
        new SimSnapshot(51.2770, -0.7764, 600, 243, 70, false, 122_500_000, 118_000_000)),
    ("Landed on runway",
        new SimSnapshot(51.2758, -0.7764, 238, 243, 40, true, 122_500_000, 118_000_000)),
    ("Vacated and parked",
        new SimSnapshot(51.2756, -0.7768, 238, 180, 0, true, 122_500_000, 118_000_000)),
];

foreach (var (label, snap) in flightStages)
{
    var phase = detector.Update(snap);
    Console.ForegroundColor = ConsoleColor.Gray;
    Console.Write($"  {label,-30} → ");
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine(phase);
}
Console.WriteLine();

// ──────────────────────────────────────
// 8. Test proximity calculator
// ──────────────────────────────────────
WriteSection("Proximity Calculator");
var dist = ProximityCalculator.DistanceNm(51.30, -0.78, eglf.LatitudeDeg, eglf.LongitudeDeg);
var bearing = ProximityCalculator.BearingDeg(51.30, -0.78, eglf.LatitudeDeg, eglf.LongitudeDeg);
var clock = ProximityCalculator.ToClockPosition(243, bearing);
Console.WriteLine($"  From 51.30N 0.78W to {eglf.Name}:");
Console.WriteLine($"    Distance: {dist:F1} nm");
Console.WriteLine($"    Bearing:  {bearing:F0} deg");
Console.WriteLine($"    Clock:    {clock} o'clock (from runway heading {eglf.ActiveRunway?.HeadingMag:F0})");
Console.WriteLine();

// ──────────────────────────────────────
// Done
// ──────────────────────────────────────
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("========================================");
Console.WriteLine("  All systems nominal.");
Console.WriteLine("========================================");
Console.ResetColor();

return;

static void WriteSection(string title)
{
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Magenta;
    Console.WriteLine($"── {title} ──");
    Console.ForegroundColor = ConsoleColor.White;
}
