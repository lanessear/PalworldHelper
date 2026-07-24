> [!WARNING]
> # THIS PROJECT IS MANAGED BY AI
> Architecture, implementation, documentation, and maintenance are developed collaboratively with AI assistance. All changes should still be reviewed and tested before release.

<div align="center">

# PalworldHelper

**A local-first Palworld collection, save-import, and breeding-planning desktop application.**

[![CI](https://github.com/lanessear/PalworldHelper/actions/workflows/ci.yml/badge.svg)](https://github.com/lanessear/PalworldHelper/actions/workflows/ci.yml)
[![Windows Build](https://github.com/lanessear/PalworldHelper/actions/workflows/release-windows.yml/badge.svg)](https://github.com/lanessear/PalworldHelper/actions/workflows/release-windows.yml)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

</div>

## Vision

PalworldHelper is designed as more than a breeding calculator. It will combine a player's real collection with versioned breeding data to calculate practical breeding plans, optimize passive inheritance, import dedicated-server saves, and explain which intermediate Pals are worth producing.

The application is intended to become a self-contained Windows executable. It runs locally, stores data in SQLite, and exposes a browser-based interface hosted by the application itself.

## Planned capabilities

- Multiple dedicated-server profiles using SSH/SFTP
- Automatic and manual save import
- Player collection browser with searchable filters
- Versioned Pal and breeding database
- Graph-based breeding route planning
- Passive-skill optimizer using the actual collection
- Reusable intermediate breeding recommendations
- Graphical breeding trees
- Localized user interface and themes
- Plugin-ready architecture
- Self-contained Windows releases

## Repository structure

```text
PalworldHelper/
├── src/
│   ├── PalworldHelper.App/                  # Executable, local API and web UI
│   ├── PalworldHelper.Core/                 # Domain model and application contracts
│   ├── PalworldHelper.Data/                 # SQLite and persistence
│   ├── PalworldHelper.SaveImport/           # Save acquisition and conversion
│   └── PalworldHelper.Plugins.Abstractions/ # Stable plugin contracts
├── tests/
│   ├── PalworldHelper.Core.Tests/
│   └── PalworldHelper.Data.Tests/
├── docs/                                    # Architecture and project decisions
├── data/                                    # Importable reference data
├── tools/                                   # Development and converter tooling
└── .github/                                 # CI, templates, and automation
```

## Current status

This repository is the **v2 architecture foundation**. It intentionally provides stable boundaries, representative domain objects, SQLite wiring, health endpoints, a shell UI, test projects, and release automation before feature implementation begins.

The native `Level.sav` parser and complete breeding dataset are not included yet. Save-import integrations are represented by interfaces so they can be implemented and replaced without coupling the rest of the application to a specific converter.

## Development

Requirements:

- .NET 8 SDK

```powershell
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
```

Run locally:

```powershell
dotnet run --project src/PalworldHelper.App
```

Then open the address printed in the console. The application normally opens the browser automatically.

## Windows publish

```powershell
dotnet publish src/PalworldHelper.App/PalworldHelper.App.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true
```

GitHub Actions also creates a downloadable Windows artifact automatically.

## Roadmap

See [ROADMAP.md](ROADMAP.md). Architectural decisions are documented in [`docs/adr`](docs/adr).

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). The project uses small, reviewable changes, tests for business logic, and documented architecture decisions.

## License

MIT — see [LICENSE](LICENSE).
