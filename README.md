<div align="center">

# PalworldHelper

<table>
<tr>
<td align="center" bgcolor="#8B0000">

# ⚠️ THIS PROJECT IS CREATED BY AI ⚠️

## AI designs, writes, documents, tests, and maintains this project.

### The human owner reviews the results and gives final approval.

**This is not presented as a human-developed software project.**

</td>
</tr>
</table>

**Quickstart v0.2 · Native Windows desktop application**

</div>

## Project model

PalworldHelper is an explicitly AI-created project. Architecture, implementation, user interface, documentation, maintenance, and future development are produced by AI. The human project owner defines goals, provides real-world requirements and test data, reviews the generated work, and approves or rejects changes.

Human approval does not imply human authorship of the implementation.

## Current features

- Manage any number of Palworld server profiles
- Authenticate with a password or SSH private key
- Test SFTP connectivity
- Automatically search the server for `Level.sav`
- Optionally use a manually configured remote save path
- Download `Level.sav` over SFTP
- Encrypt saved passwords with Windows DPAPI for the current Windows user
- Load an existing `palworld_breeding_results.json` file
- Select source and target Pals with searchable dropdowns
- Calculate the shortest continuous breeding chain with breadth-first search

## Next development step

The downloaded `Level.sav` is not yet parsed into players, owned Pals, and passive skills. Save download and automatic discovery are already implemented so the next step can focus on importing and testing a real dedicated-server save.

## Download the Windows executable

Every commit to `main` automatically triggers a self-contained Windows build through GitHub Actions:

1. Open **Actions → Build Windows EXE**.
2. Select the newest successful workflow run.
3. Download the `PalworldHelper-win-x64` artifact.
4. Extract the ZIP file and start `PalworldHelper.exe`.

The workflow can also be started manually with **Run workflow**.

Neither Python nor the .NET SDK is required on the target computer.

## Breeding data

On first launch, open **Breeding Chain → Select JSON** and choose `palworld_breeding_results.json`. Alternatively, place the file next to `PalworldHelper.exe`.

## Local development

With the .NET 8 SDK installed:

```powershell
.\build-windows.ps1
```

The resulting executable is written to `publish\PalworldHelper.exe`.
