# Third-party notices

This project intentionally keeps external parser code out of the repository. The Windows package build downloads the current upstream source, builds the parser helper, and places the resulting executable in `parser/PalworldSaveParser.exe` next to `PalworldHelper.exe`.

## Palworld save parser

- **Purpose**: Read Palworld `.sav` files, including Oodle-compressed saves.
- **Runtime artifact**: `parser/PalworldSaveParser.exe` in release packages.
- **Build wrapper in this repository**: `tools/save_parser/palworld_save_parser.py`
- **Character ID mapping in this repository**: `tools/save_parser/palworld_character_names.json`
- **Passive skill catalog in this repository**: `tools/save_parser/palworld_passive_skills.json`
- **Upstream source repository**: https://github.com/deafdudecomputers/PalworldSaveTools
- **Mapping source files**:
  - `resources/game_data/breedingdata.json`
  - `resources/game_data/characters.json`
  - `resources/game_data/skills.json`
- **Upstream packages used by the build**:
  - `palsav-flex` from `src/palsav`
  - `palooz` from `src/palsav/palooz`
- **License declared by those package manifests**: `GPL-3.0-or-later`
- **License file present in the upstream repository at the time this notice was added**: MIT License, copyright 2026 Pylar
- **Last locally verified upstream commit**: `cbd98c44923b18d8010d94cc1ca10d1657d55c17`

Because the package manifests and repository-level license file do not currently declare the same license, treat the bundled parser helper as third-party software with separate licensing obligations. If the parser dependency is updated or replaced, update this notice in the same change.

## PyInstaller

- **Purpose**: Build the Python parser wrapper as a Windows executable.
- **Version used by package builds**: `6.14.2`
- **Source**: https://github.com/pyinstaller/pyinstaller
- **License**: GPL-2.0-or-later with a special exception for distributing generated executables.
