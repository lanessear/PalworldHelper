# Architecture

PalworldHelper uses a pragmatic clean architecture.

```mermaid
flowchart LR
  UI[PalworldHelper.App\nLocal API + Web UI] --> Core[PalworldHelper.Core\nDomain + Use-case contracts]
  UI --> Data[PalworldHelper.Data\nSQLite persistence]
  UI --> Import[PalworldHelper.SaveImport\nSSH + conversion]
  Data --> Core
  Import --> Core
  Plugins[External plugins] --> PluginApi[Plugins.Abstractions]
  UI --> PluginApi
```

## Dependency rules

- `Core` depends on no infrastructure project.
- `Data` implements persistence contracts defined by `Core`.
- `SaveImport` implements acquisition and conversion contracts defined by `Core`.
- `App` composes all components and hosts the local UI.
- Plugin contracts remain deliberately small and stable.

## Local-first application model

The executable starts an ASP.NET Core server bound to loopback, initializes SQLite, serves the frontend, and opens the default browser. It is not intended to expose its API to the network by default.

## Data separation

Reference data and user-owned data are distinct concepts:

- Reference data: Pal species, elements, passive skills, and breeding recipes, versioned by game-data release.
- User data: server profiles, players, imported Pal instances, synchronization history, and preferences.

This allows game-data upgrades without replacing the user's collection.

## Evolution

Database schema changes use migrations. Public plugin interfaces are versioned. Large architectural choices receive an ADR.
