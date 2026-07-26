# Contributing

## Principles

- Keep domain logic independent from UI and infrastructure.
- Prefer small, reviewable changes.
- Add tests for business rules and regressions.
- Do not commit credentials, save files, databases, or converter binaries.
- Record significant architecture choices as ADRs in `docs/adr`.

## Branches and commits

Use descriptive branch names and imperative commit messages.

Examples:

```text
feature/breeding-graph
fix/duplicate-pal-import
Add collection-aware route scoring
```

## Validation

Before submitting a pull request:

```powershell
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
```

For the packaged Windows folder, run:

```powershell
.\build-windows.ps1
```

## Pull requests

Explain what changed, why it changed, how it was tested, and whether data migrations or compatibility concerns exist.

## Releases

Every GitHub release should include a human-written feature list. Mention features added since the previous release, removed features, important behavior changes, known follow-up work, and the package users should download.
