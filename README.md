<div align="center">

# <span style="color:red">🔴 THIS PROJECT IS MANAGED BY AI 🔴</span>

> [!CAUTION]
> **THIS PROJECT IS MANAGED BY AI.** Code, architecture, documentation, and changes may be created or modified primarily through AI-assisted development. Review releases and source code before trusting them with server credentials or save files.

# Palworld Helper

A portable Windows breeding and collection assistant for Palworld server players.

</div>

## Current foundation

- Self-contained Windows `.exe` built with .NET 8
- Local browser UI at `http://127.0.0.1:8765`
- SQLite storage in `%LOCALAPPDATA%\PalworldHelper`
- Multiple server profiles
- SSH/SFTP download of `Level.sav`
- Collection, passive skills, gender, level, rank and talent values
- Searchable target-Pal and passive-skill dropdowns
- Breeding routes ranked by missing parents and route length
- Optional "owned Pals only" search
- Manual import of a converted `Level.sav.json`

## Important status

This repository is an **MVP foundation**, not a finished 1.0 release.

The UI, database, SSH sync, JSON import, collection view and breeding engine are implemented. The repository does **not** yet bundle a native Palworld `Level.sav` decoder. Add a compatible converter under `tools/palworld-save-tools`, as documented there, or use the JSON upload in the app.

The included `data/breeding.json` contains only a schema example. Replace it with the project's curated and cleaned breeding dataset before using route calculations.

## Build the EXE without installing anything locally

1. Open this repository on GitHub.
2. Select **Actions**.
3. Select **Build Windows EXE**.
4. Click **Run workflow**.
5. Download the `PalworldHelper-Windows-x64` artifact.
6. Extract it and run `PalworldHelper.exe`.

The build is self-contained. The target PC does not need Python, Node.js, or a separately installed .NET runtime.

## Local development

Requires the .NET 8 SDK:

```powershell
dotnet restore
dotnet run --project src/PalworldHelper
```

## Server setup

Open **Server** and enter:

- profile name
- server hostname or IP
- SSH port
- SSH username
- full remote path to `Level.sav`
- Palworld player name, defaulting to `Lanessear`
- either an SSH password for the current sync or a local private-key path

Passwords are sent only to the local backend for the current request and are not stored in SQLite.

## Project structure

```text
src/PalworldHelper/
  Data/       SQLite persistence
  Models/     Domain records
  Services/   SSH, save import, breeding data and solver
  wwwroot/    Browser UI
data/          Curated breeding data
tools/         External save-converter integration
.github/       Windows EXE build workflow
```

## Security

The web service listens only on `127.0.0.1`. Keep the application and its data directory private. Prefer SSH keys with restricted server permissions and read-only access to the Palworld save directory.

## Roadmap

- Bundle or implement a native C# Palworld save decoder
- Replace the example breeding dataset with the cleaned project dataset
- Localized Pal and passive-skill aliases
- Better passive inheritance scoring
- Visual breeding tree layout
- Automatic scheduled synchronization
- Multiple players and guild views
- Favorites, saved plans and history
- Tests against real current-version server saves
