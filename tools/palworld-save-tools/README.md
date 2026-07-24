# Save converter integration

Place a compatible Windows converter here as either:

- `palworld-save-tools.exe`, or
- `convert.exe`

It must accept:

```text
converter.exe Level.sav --to-json --output Level.sav.json
```

Until a converter is bundled, the application still supports uploading an already converted `Level.sav.json` in **Server → Alternative JSON import**.

The adapter is isolated in `Services/SaveImportService.cs`, so a native C# parser or a differently packaged converter can be added later without changing the UI, database, SSH sync, or breeding engine.
