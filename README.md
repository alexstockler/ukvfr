# UK VFR ATC

Realistic UK VFR Air Traffic Control companion app for Microsoft Flight Simulator 2020.

Replaces the default US-centric ATC with accurate UK radiotelephony (CAP 413), airspace procedures, and circuit management.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (Windows)
- Microsoft Flight Simulator 2020 (for live SimConnect data)

## Build & Run

```bash
dotnet build UkVfr.slnx
dotnet run --project src/UkVfr.App
```

The app currently displays a live data view using simulated aircraft state. Real SimConnect integration is in progress.

## Project Structure

```
UkVfr.slnx                  Solution file
src/
  UkVfr.Core/               Business logic, SimConnect bridge, data models
  UkVfr.App/                WPF desktop application (presentation layer)
```

## Status

Early MVP — foundational infrastructure only. See [PLAN.md](PLAN.md) for the full roadmap.

## License

[MIT](LICENSE)
