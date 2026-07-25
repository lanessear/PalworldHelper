# Development guide

## Start the application

```powershell
dotnet run --project src/PalworldHelper
```

## Build the Windows package

```powershell
.\build-windows.ps1
```

## Application data

Profiles and local settings are stored beneath the current Windows user's application-data directory.

## Adding a domain feature

1. Model rules in `PalworldHelper.Core`.
2. Add an abstraction if infrastructure is required.
3. Implement infrastructure in the current WPF application or a focused infrastructure project.
4. Compose it in `PalworldHelper`.
5. Add tests.
6. Update docs and changelog.
