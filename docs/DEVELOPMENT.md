# Development guide

## Start the application

```powershell
dotnet run --project src/PalworldHelper.App
```

## API endpoints in the foundation

- `GET /api/health`
- `GET /api/about`
- `GET /api/stats`

## Database location

By default, application data is stored beneath the user's local application-data directory in `PalworldHelper/palworldhelper.db`.

## Adding a domain feature

1. Model rules in `PalworldHelper.Core`.
2. Add an abstraction if infrastructure is required.
3. Implement infrastructure in `Data` or `SaveImport`.
4. Compose it in `App`.
5. Add tests.
6. Update docs and changelog.
