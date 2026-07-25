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

**Native Windows desktop helper for Palworld saves and breeding routes**

</div>

## Project model

PalworldHelper is an explicitly AI-created project. Architecture, implementation, user interface, documentation, maintenance, and future development are produced by AI. The human project owner defines goals, provides real-world requirements and test data, reviews the generated work, and approves or rejects changes.

Human approval does not imply human authorship of the implementation.

## Current features

- **Server profiles**: Save multiple Palworld server connections and switch between them quickly.
- **Secure authentication**: Connect over SFTP with password or SSH key support; saved passwords are encrypted with Windows DPAPI for the current user.
- **Save discovery and download**: Search the server for `Level.sav` automatically, use a manual remote path when needed, and download the save into the local app folder.
- **Save parsing and inspection**: Select a local `Level.sav`, parse players, owned Pals, levels, genders, and passive skills, and keep the selected path for the next launch.
- **Bundled breeding data**: Ship the versioned repository dataset `palworld_breeding_results_v1.0_2026-07-24.json` next to the executable.
- **Custom breeding data**: Load compatible custom breeding JSON files when you want to test another dataset.
- **Breeding chain search**: Pick source and target Pals from searchable dropdowns and calculate the shortest continuous breeding route.
- **Maintenance tools**: Check for updates, install a downloaded release, and uninstall local app data from inside the desktop app.

## Next development step

The save parser is currently shipped as a bundled helper in the `parser` folder next to the executable. The long-term goal remains a native C# parser so the helper can eventually disappear.

## Install

Download the latest Windows ZIP from **Actions → Build Windows package** or from a tagged GitHub release, extract it, and start `PalworldHelper.exe`.

Keep the extracted files together. The default breeding dataset is loaded from the same folder as the executable, and save parsing uses the bundled `parser` folder.

## Breeding data

The release package includes `palworld_breeding_results_v1.0_2026-07-24.json` as the default dataset. Use **Breeding Chain → Select JSON** only when you want to load a custom compatible dataset.

## Build from source

Run the Windows package build:

```powershell
.\build-windows.ps1
```

The output is written to `publish\`.
