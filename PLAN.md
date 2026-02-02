# UK VFR ATC Add-on for Microsoft Flight Simulator 2020

## Development Plan

### 1. Project Overview

An external add-on for MSFS 2020 that replaces the default (US-centric) ATC with accurate UK VFR radiotelephony, airspace services, and procedures. The simulator's built-in ATC does not expose a usable API, so this add-on runs as an **out-of-process application** communicating with the sim via **SimConnect**.

**Core goal:** A pilot flying VFR in the UK within MSFS should be able to request realistic services from ATC using correct CAP 413 phraseology, perform standard UK procedures (overhead joins, UK circuit calls, LARS requests), and receive appropriate responses.

---

### 2. Architecture

```
┌─────────────────────────────────────────────────────┐
│                   MSFS 2020                         │
│                                                     │
│  SimConnect Server ◄──────────► AI Traffic Objects  │
│        ▲                                            │
└────────┼────────────────────────────────────────────┘
         │ SimConnect (TCP/Named Pipe)
         ▼
┌─────────────────────────────────────────────────────┐
│              UK VFR ATC Application                 │
│                  (.NET 8 / C#)                      │
│                                                     │
│  ┌─────────────┐  ┌──────────────┐  ┌───────────┐  │
│  │ SimConnect   │  │  ATC Engine  │  │  Voice    │  │
│  │ Bridge       │──│  (State      │──│  Module   │  │
│  │              │  │   Machine)   │  │  (TTS/SR) │  │
│  └─────────────┘  └──────────────┘  └───────────┘  │
│         │                │                │         │
│  ┌─────────────┐  ┌──────────────┐  ┌───────────┐  │
│  │ Aircraft     │  │  Airspace &  │  │ Phraseo-  │  │
│  │ State Tracker│  │  Procedure   │  │ logy      │  │
│  │              │  │  Database    │  │ Engine    │  │
│  └─────────────┘  └──────────────┘  └───────────┘  │
│                        │                            │
│                ┌───────────────┐                    │
│                │  UK Aerodrome │                    │
│                │  & Airspace   │                    │
│                │  Data (JSON)  │                    │
│                └───────────────┘                    │
└─────────────────────────────────────────────────────┘
```

**Why C# / .NET 8:**
- First-class SimConnect support via managed client (`Microsoft.FlightSimulator.SimConnect`)
- Strong ecosystem for TTS (System.Speech / Azure Cognitive Services)
- Cross-references: OpenSquawk, FsConnect, and most commercial ATC add-ons use C#/.NET
- Good performance for real-time state tracking

**Optional in-sim panel:** A lightweight HTML/JS panel installed to the Community folder can show a radio stack or frequency selector, communicating with the main application via a local WebSocket.

---

### 3. Key Components

#### 3.1 SimConnect Bridge
Handles all communication with the simulator.

**Reads from sim (polled every ~200ms):**
- Aircraft position (lat, lon, altitude, heading, ground speed, vertical speed)
- COM1/COM2 active and standby frequencies
- Transponder code and mode
- On-ground/in-air state, flap/gear positions
- Current simulation time (UTC)
- Weather data (wind, visibility, pressure) for ATIS generation

**Writes to sim:**
- AI traffic creation and position updates (`SimConnect_AICreateNonATCAircraft`)
- Transponder squawk assignments via client events
- Optional: COM frequency tuning assistance

**Library:** Use [FsConnect](https://github.com/c-true/FsConnect) as a lightweight C# wrapper, or the raw `Microsoft.FlightSimulator.SimConnect` managed assembly.

#### 3.2 Aircraft State Tracker
Maintains a model of the player aircraft's state and infers intent.

**Tracked state:**
- Current phase of flight: parked, taxiing, holding, taking off, climbing, cruise, descending, circuit (crosswind/downwind/base/final), landed
- Tuned frequency (determines which ATC unit is being contacted)
- Proximity to aerodromes, ATZs, MATZs, CTRs
- Altitude relative to circuit height (QFE), transition altitude
- Whether the pilot has made required calls

**Phase detection logic:**
- Ground speed < 5kt + on ground = parked
- Ground speed > 5kt + on ground = taxiing
- Positive VS + altitude increasing = climbing
- Within 2nm of aerodrome + altitude near circuit height + turning = circuit
- Lateral track analysis to determine circuit leg (crosswind/downwind/base/final)

#### 3.3 ATC Engine (State Machine)
The core logic engine that models ATC behaviour.

**Service types implemented (per CAP 774):**

| Service | Radar Required | Traffic Info | Deconfliction | Available To |
|---------|---------------|-------------|---------------|-------------|
| Basic | No | Weather/NOTAMs only | No | All |
| Traffic | Yes | Yes (conflicting traffic) | No | All |
| Deconfliction | Yes | Yes | Yes (headings/levels) | IFR only |
| Procedural | No | Against known traffic | Procedural separation | IFR only |

**ATC conversation state machine (simplified):**

```
[Idle] ──(pilot initial call)──► [Awaiting Pass Message]
  ──(pilot passes message)──► [Processing Request]
  ──(valid request)──► [Service Active]
  ──(pilot requests frequency change)──► [Frequency Change Approved]
  ──(pilot leaves coverage)──► [Service Terminated]
```

**Key ATC behaviours to model:**
- Respond to initial calls with "pass your message"
- Assign squawk codes (from a realistic range)
- Provide Traffic Service: issue traffic information when conflicts detected within 3nm/3000ft
- Issue circuit joining instructions (overhead join, downwind join, etc.)
- Provide aerodrome information (runway in use, QFE/QNH, wind, traffic)
- Sequence circuit traffic (extend downwind, orbit, go-around)
- Approve frequency changes ("frequency change approved, squawk 7000")
- Handle emergency declarations (Mayday, Pan-Pan) with appropriate responses

#### 3.4 Phraseology Engine (CAP 413)
Generates correctly formatted UK radiotelephony messages.

**Template-based system:**
```csharp
// Example template
"[callsign], [unit_name], squawk [code], [service_type]"
// Rendered
"Golf Alpha Bravo Charlie Delta, Farnborough Radar, squawk 4512, Traffic Service"
```

**Phraseology rules encoded:**
- Callsign format: registration (G-ABCD) spoken as "Golf Alpha Bravo Charlie Delta", abbreviated to "Golf Charlie Delta" after initial exchange
- "Line up runway [number]" (not "line up and wait")
- "Take off" reserved exclusively for take-off clearance
- "Behind [traffic] on [position], [instruction] behind" for conditional clearances
- "Affirm" / "Negative" (not "affirmative" / "no")
- "Roger" for acknowledgement
- QNH in hectopascals, QFE where appropriate
- Altitudes in feet, visibility in metres
- Mandatory readback items: runway, QNH, altimeter, flight level, heading, speed, squawk

**Message categories:**
- Aerodrome (taxi, line up, take off, circuit, landing clearance)
- Approach/departure (joining instructions, departure information)
- En-route (traffic information, service provision, frequency changes)
- Emergency (Mayday/Pan-Pan acknowledgement and priority handling)
- ATIS (auto-generated from sim weather data)

#### 3.5 UK Aerodrome & Airspace Database
Static data files (JSON) defining UK aviation infrastructure.

**Aerodrome data (per airfield):**
```json
{
  "icao": "EGLF",
  "name": "Farnborough",
  "elevation_ft": 238,
  "transition_altitude_ft": 3000,
  "frequencies": {
    "tower": 122.500,
    "approach": 134.350,
    "radar": 125.250,
    "atis": 128.400
  },
  "runways": [
    {
      "designator": "24",
      "heading_mag": 243,
      "length_m": 2440,
      "circuit_direction": "left",
      "circuit_height_qfe_ft": 1000
    }
  ],
  "atz": {
    "type": "circle",
    "radius_nm": 2.5,
    "upper_limit_ft_aal": 2000
  },
  "unit_callsigns": {
    "tower": "Farnborough Tower",
    "approach": "Farnborough Approach",
    "radar": "Farnborough Radar",
    "radio": null
  },
  "services_available": ["basic", "traffic", "deconfliction"],
  "notes": "PPR required. No circuits weekdays."
}
```

**Airspace data:**
- Class A–G boundaries (polygons with vertical limits)
- CTR/CTA definitions
- MATZ locations and dimensions
- Danger/Restricted/Prohibited areas
- LARS unit locations and coverage

**Data sources:**
- UK AIP (via NATS)
- OpenAIP (open-source aviation data)
- Community-maintained databases

#### 3.6 Voice Module

**Text-to-Speech (ATC output):**
- System.Speech.Synthesis (built-in, basic quality) for MVP
- Azure Cognitive Services Speech SDK for higher quality (multiple voice profiles for different controllers)
- Apply audio processing: slight compression, band-pass filter (300Hz–3.4kHz) to simulate radio effect
- Different voice profiles per unit type (tower vs radar vs ATIS)

**Speech Recognition (pilot input) — optional/phase 2:**
- Azure Speech SDK or Whisper (local) for transcription
- Intent parser to map spoken phrases to structured commands
- Fallback: clickable menu system (like the default MSFS ATC) for MVP

---

### 4. Development Phases

#### Phase 1: Foundation (MVP)
**Goal:** Basic working ATC at a single UK aerodrome with menu-driven pilot input.

| Task | Description |
|------|-------------|
| Project setup | .NET 8 solution, SimConnect integration, basic UI window |
| SimConnect bridge | Read aircraft position, altitude, heading, speed, COM frequencies, weather |
| Aircraft state tracker | Detect flight phase (parked, taxi, airborne, circuit legs) |
| Single aerodrome | Hard-code one aerodrome (e.g., EGLF Farnborough) with frequencies, runways, ATZ |
| Basic ATC state machine | Handle: initial call → pass message → service provision → frequency change |
| Phraseology engine v1 | Template-based CAP 413 message generation for core interactions |
| Circuit management | Detect overhead join, issue joining instructions, sequence one aircraft in the circuit |
| Menu-driven input | Clickable options for pilot responses (initial call, readback, request service, etc.) |
| Basic TTS | System.Speech for ATC voice output with radio filter |
| ATIS generation | Auto-generate ATIS from sim weather data in UK format |

**Deliverable:** A pilot can fly a VFR circuit at Farnborough, interact with Tower via menus, and hear correctly phrased UK ATC responses.

#### Phase 2: Expanded Coverage
**Goal:** Support multiple aerodromes, en-route services, and realistic transitions.

| Task | Description |
|------|-------------|
| Aerodrome database | JSON data for 20+ UK aerodromes (mix of controlled, uncontrolled, military) |
| Airspace database | UK Class A–G boundaries, CTRs, MATZs |
| En-route services | Basic Service, Traffic Service via LARS units |
| Frequency handoff | Proper handoff between units as pilot transits |
| Traffic information | Generate realistic traffic calls ("traffic, 2 o'clock, 3 miles, crossing left to right, indicating 200 feet below") |
| Multiple join types | Overhead, downwind, crosswind, base leg, straight-in approaches |
| Improved TTS | Azure Speech with multiple controller voice profiles |
| Airfield variations | Handle Radio-only (air/ground), Information (FISO), Tower, Approach, Radar units |
| Settings UI | Configuration panel for voice volume, speech rate, realism options |

**Deliverable:** A pilot can fly a cross-country VFR flight across southern England, transitioning between LARS units, and arriving via overhead join at a different aerodrome.

#### Phase 3: Advanced Features
**Goal:** Speech recognition, AI traffic, and comprehensive UK coverage.

| Task | Description |
|------|-------------|
| Speech recognition | Voice input using Azure Speech or local Whisper model |
| Intent parsing | NLP to extract pilot intentions from spoken phrases |
| AI traffic injection | Create and manage AI VFR traffic in circuits and en-route via SimConnect |
| Traffic sequencing | Sequence multiple aircraft (player + AI) in circuits |
| Full UK coverage | Complete aerodrome database, all LARS units, all controlled airspace |
| Special VFR | SVFR clearances through CTRs |
| Emergency handling | Full Mayday/Pan-Pan response procedures |
| MATZ penetration | Military ATZ procedures and crossing clearances |
| In-sim panel | Optional HTML/JS radio panel installed to Community folder |
| Scenario system | Pre-built training scenarios (first solo, cross-country nav, busy circuit) |

**Deliverable:** Full UK VFR ATC experience with voice interaction, AI traffic, and nationwide coverage.

---

### 5. Data & Reference Sources

| Source | Usage |
|--------|-------|
| **CAP 413** (CAA Radiotelephony Manual) | Phraseology rules and templates |
| **CAP 774** (UK Flight Information Services) | Service type definitions and procedures |
| **CAP 493** (MATS Part 1) | ATC operational procedures |
| **UK AIP** (NATS AIS) | Aerodrome data, frequencies, airspace definitions |
| **OpenAIP** | Machine-readable airspace and aerodrome data |
| **MSFS SimConnect SDK docs** | API reference for sim integration |
| **OpenSquawk source** | Architecture reference for ATC add-on structure |
| **FsConnect library** | SimConnect C# wrapper |

---

### 6. Technical Decisions & Trade-offs

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Runtime | .NET 8 (C#) | Best SimConnect support, good TTS ecosystem, strong typing |
| Process model | Out-of-process (external .exe) | Stability, easier debugging, no WASM complexity |
| Pilot input (MVP) | Menu-driven | Reliable, no speech recognition dependency |
| Pilot input (later) | Speech recognition | More immersive, but requires tuning |
| ATC voice (MVP) | System.Speech.Synthesis | Free, zero-config, good enough for MVP |
| ATC voice (later) | Azure Cognitive Services | Multiple voices, better quality, small cost |
| Data format | JSON files | Human-readable, easy to edit, community-contributable |
| In-sim UI | Optional WebSocket panel | Non-essential; the add-on works without any Community folder install |
| AI traffic | Phase 3 | Significant complexity; core ATC works without it |

---

### 7. Project Structure

```
ukvfr/
├── src/
│   ├── UkVfr.Core/                  # Core library (no UI dependency)
│   │   ├── SimConnect/              # SimConnect bridge and data types
│   │   │   ├── SimConnectBridge.cs
│   │   │   ├── AircraftState.cs
│   │   │   └── SimDataDefinitions.cs
│   │   ├── Atc/                     # ATC engine
│   │   │   ├── AtcEngine.cs         # Main state machine
│   │   │   ├── AtcUnit.cs           # Represents an ATC unit (tower, approach, etc.)
│   │   │   ├── ServiceType.cs       # Basic, Traffic, Deconfliction, Procedural
│   │   │   └── CircuitManager.cs    # Circuit sequencing logic
│   │   ├── Phraseology/             # CAP 413 message generation
│   │   │   ├── PhraseologyEngine.cs
│   │   │   ├── Templates/           # Message templates by category
│   │   │   └── CallsignFormatter.cs
│   │   ├── Airspace/                # Airspace and aerodrome models
│   │   │   ├── Aerodrome.cs
│   │   │   ├── AirspaceZone.cs
│   │   │   ├── Runway.cs
│   │   │   └── AirspaceDatabase.cs
│   │   ├── Tracking/                # Aircraft phase and position tracking
│   │   │   ├── FlightPhaseDetector.cs
│   │   │   ├── CircuitLegDetector.cs
│   │   │   └── ProximityCalculator.cs
│   │   └── Voice/                   # TTS and speech recognition
│   │       ├── TextToSpeechService.cs
│   │       ├── RadioAudioFilter.cs
│   │       └── SpeechRecognitionService.cs
│   ├── UkVfr.App/                   # Desktop application (WPF or Avalonia)
│   │   ├── MainWindow.xaml
│   │   ├── ViewModels/
│   │   └── Views/
│   │       ├── RadioPanel.xaml      # Frequency display and pilot menu
│   │       ├── TranscriptView.xaml  # Scrolling ATC transcript
│   │       └── SettingsView.xaml
│   └── UkVfr.InSimPanel/           # Optional MSFS Community folder package
│       ├── manifest.json
│       ├── layout.json
│       └── html_ui/
│           └── Pages/
│               └── UkVfrPanel/
│                   ├── UkVfrPanel.html
│                   ├── UkVfrPanel.js
│                   └── UkVfrPanel.css
├── data/
│   ├── aerodromes/                  # One JSON file per aerodrome
│   │   ├── EGLF.json
│   │   ├── EGLL.json
│   │   └── ...
│   ├── airspace/                    # Airspace boundary definitions
│   │   ├── controlled.json
│   │   ├── class_g.json
│   │   └── matz.json
│   └── phraseology/                 # Phraseology templates
│       ├── aerodrome.json
│       ├── enroute.json
│       └── emergency.json
├── tests/
│   ├── UkVfr.Core.Tests/
│   │   ├── AtcEngineTests.cs
│   │   ├── PhraseologyTests.cs
│   │   ├── FlightPhaseDetectorTests.cs
│   │   └── CircuitLegDetectorTests.cs
│   └── UkVfr.Integration.Tests/
├── docs/
│   └── (reference material links)
├── PLAN.md                          # This file
├── .gitignore
└── UkVfr.sln
```

---

### 8. Getting Started Checklist

1. **Install prerequisites:**
   - Visual Studio 2022 or JetBrains Rider
   - .NET 8 SDK
   - MSFS 2020 + MSFS SDK (for SimConnect DLLs)
2. **Create solution and projects** per the structure above
3. **Add SimConnect reference** (`Microsoft.FlightSimulator.SimConnect` DLL from the SDK, or use the FsConnect NuGet package)
4. **Implement SimConnect bridge** — confirm you can read aircraft position and COM frequencies
5. **Build aircraft state tracker** — log flight phase transitions
6. **Build phraseology engine** — generate sample ATC messages and verify against CAP 413
7. **Build ATC state machine** — handle a single aerodrome interaction end-to-end
8. **Add TTS output** — hear ATC responses through speakers
9. **Add menu-driven pilot input** — clickable options in the app window
10. **Test a full circuit** at one aerodrome
