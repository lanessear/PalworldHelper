#!/usr/bin/env python3
"""
Palworld Pal Exporter
Read-only exporter for player-owned Pals from a Palworld Level.sav.

Requires:
    pip install palworld-save-tools
"""

from __future__ import annotations

import argparse
import csv
import json
import shutil
import subprocess
import sys
import tempfile
from collections import Counter
from pathlib import Path
from typing import Any, Iterable


WRAPPER_KEYS = ("value", "Value", "struct_value", "Struct", "RawData")


def norm_guid(value: Any) -> str:
    if value is None:
        return ""
    if isinstance(value, dict):
        # Common GUID representations.
        for key in ("value", "Value", "guid", "Guid", "ID", "Id"):
            if key in value:
                return norm_guid(value[key])
        # Four-int Unreal GUID.
        ints = []
        for key in ("A", "B", "C", "D"):
            if key in value and isinstance(value[key], int):
                ints.append(value[key] & 0xFFFFFFFF)
        if len(ints) == 4:
            return "".join(f"{x:08x}" for x in ints)
    text = str(value).strip().lower()
    return "".join(ch for ch in text if ch.isalnum())


def unwrap(value: Any, max_depth: int = 12) -> Any:
    """Remove common Unreal-property wrapper objects."""
    current = value
    for _ in range(max_depth):
        if not isinstance(current, dict):
            break
        # Only unwrap when the wrapper is plausibly metadata + one payload.
        found = None
        for key in WRAPPER_KEYS:
            if key in current:
                found = current[key]
                break
        if found is None:
            break
        metadata_keys = {
            "id", "type", "struct_type", "array_type", "key_type", "value_type",
            "custom_type", "type_name", "name", "property_type", "skip_type"
        }
        other = {str(k).lower() for k in current if k not in WRAPPER_KEYS}
        if other and not other.issubset(metadata_keys):
            break
        current = found
    return current


def scalar(value: Any) -> Any:
    value = unwrap(value)
    if isinstance(value, dict):
        for key in ("value", "Value", "Name", "name", "ID", "Id"):
            if key in value and not isinstance(value[key], (dict, list)):
                return value[key]
    return value


def get_ci(mapping: dict[str, Any], *names: str) -> Any:
    wanted = {n.lower() for n in names}
    for key, value in mapping.items():
        if str(key).lower() in wanted:
            return value
    return None


def walk(node: Any, path: tuple[str, ...] = ()) -> Iterable[tuple[tuple[str, ...], Any]]:
    yield path, node
    if isinstance(node, dict):
        for key, value in node.items():
            yield from walk(value, path + (str(key),))
    elif isinstance(node, list):
        for idx, value in enumerate(node):
            yield from walk(value, path + (str(idx),))


def candidate_character_records(data: Any) -> list[dict[str, Any]]:
    """
    Locate character records without assuming a single exact converter schema.
    Deduplicates by object identity and then by instance/character signature.
    """
    found: list[dict[str, Any]] = []
    seen_objects: set[int] = set()

    for path, node in walk(data):
        if not isinstance(node, dict):
            continue

        # Character data may itself be wrapped or nested under SaveParameter.
        variants = [node]
        for key in (
            "SaveParameter", "save_parameter", "CharacterSaveParameter",
            "character_save_parameter", "RawData", "raw_data"
        ):
            child = get_ci(node, key)
            child = unwrap(child)
            if isinstance(child, dict):
                variants.append(child)

        for record in variants:
            if id(record) in seen_objects:
                continue
            keys = {str(k).lower() for k in record}
            has_character = any(k in keys for k in (
                "characterid", "character_id", "nickname", "nick_name"
            ))
            has_identity = any(k in keys for k in (
                "playeruid", "player_uid", "ownerplayeruid", "owner_player_uid",
                "instanceid", "instance_id", "isplayer", "is_player"
            ))
            if has_character and has_identity:
                seen_objects.add(id(record))
                found.append(record)

    # Deduplicate records that appeared through multiple wrappers.
    unique: dict[tuple[str, str, str, str], dict[str, Any]] = {}
    for record in found:
        sig = (
            norm_guid(scalar(get_ci(record, "InstanceId", "InstanceID", "instance_id"))),
            norm_guid(scalar(get_ci(record, "PlayerUId", "PlayerUID", "player_uid"))),
            str(scalar(get_ci(record, "CharacterID", "character_id")) or ""),
            str(scalar(get_ci(record, "NickName", "Nickname", "nick_name")) or ""),
        )
        unique.setdefault(sig, record)
    return list(unique.values())


def bool_value(value: Any) -> bool:
    value = scalar(value)
    if isinstance(value, bool):
        return value
    return str(value).lower() in {"true", "1", "yes"}


def list_value(value: Any) -> list[Any]:
    value = unwrap(value)
    if value is None:
        return []
    if isinstance(value, list):
        return [scalar(x) for x in value]
    if isinstance(value, dict):
        for key in ("values", "Values", "array", "Array"):
            if key in value and isinstance(value[key], list):
                return [scalar(x) for x in value[key]]
    return [scalar(value)]


def record_to_player(record: dict[str, Any]) -> dict[str, Any] | None:
    is_player = bool_value(get_ci(record, "IsPlayer", "is_player"))
    player_uid = norm_guid(scalar(get_ci(record, "PlayerUId", "PlayerUID", "player_uid")))
    owner_uid = norm_guid(scalar(get_ci(record, "OwnerPlayerUId", "OwnerPlayerUID", "owner_player_uid")))
    character_id = str(scalar(get_ci(record, "CharacterID", "character_id")) or "")
    if not is_player and "player" not in character_id.lower():
        return None
    uid = player_uid or owner_uid
    if not uid:
        return None
    return {
        "name": str(scalar(get_ci(record, "NickName", "Nickname", "nick_name")) or ""),
        "uid": uid,
        "level": scalar(get_ci(record, "Level", "level")),
        "character_id": character_id,
    }


def extract_players(records: list[dict[str, Any]]) -> list[dict[str, Any]]:
    players: dict[str, dict[str, Any]] = {}
    for record in records:
        player = record_to_player(record)
        if player:
            old = players.get(player["uid"])
            if old is None or (not old["name"] and player["name"]):
                players[player["uid"]] = player
    return sorted(players.values(), key=lambda p: (p["name"].lower(), p["uid"]))


def pal_from_record(record: dict[str, Any], player_uid: str) -> dict[str, Any] | None:
    if bool_value(get_ci(record, "IsPlayer", "is_player")):
        return None

    owner_uid = norm_guid(scalar(get_ci(
        record, "OwnerPlayerUId", "OwnerPlayerUID", "owner_player_uid"
    )))
    if not owner_uid or owner_uid != player_uid:
        return None

    character_id = str(scalar(get_ci(record, "CharacterID", "character_id")) or "")
    if not character_id or "player" in character_id.lower():
        return None

    gender_raw = scalar(get_ci(record, "Gender", "gender", "Sex", "sex"))
    gender = str(gender_raw or "")
    gender_map = {
        "epalgender::male": "Male", "male": "Male", "1": "Male",
        "epalgender::female": "Female", "female": "Female", "2": "Female",
    }
    gender = gender_map.get(gender.lower(), gender)

    passive = [str(x) for x in list_value(get_ci(
        record, "PassiveSkillList", "passive_skill_list", "PassiveSkills"
    )) if x not in (None, "")]
    active = [str(x) for x in list_value(get_ci(
        record, "EquipWaza", "equip_waza", "ActiveSkills"
    )) if x not in (None, "")]

    return {
        "species_id": character_id,
        "nickname": str(scalar(get_ci(record, "NickName", "Nickname", "nick_name")) or ""),
        "level": scalar(get_ci(record, "Level", "level")),
        "gender": gender,
        "instance_id": norm_guid(scalar(get_ci(record, "InstanceId", "InstanceID", "instance_id"))),
        "owner_player_uid": owner_uid,
        "passive_skills": passive,
        "active_skills": active,
        "rank": scalar(get_ci(record, "Rank", "rank")),
        "talent_hp": scalar(get_ci(record, "Talent_HP", "TalentHP", "talent_hp")),
        "talent_melee": scalar(get_ci(record, "Talent_Melee", "TalentMelee", "talent_melee")),
        "talent_shot": scalar(get_ci(record, "Talent_Shot", "TalentShot", "talent_shot")),
        "talent_defense": scalar(get_ci(record, "Talent_Defense", "TalentDefense", "talent_defense")),
    }


def find_converter() -> list[str]:
    exe = shutil.which("palworld-save-tools")
    if exe:
        return [exe]
    # Fallback for Python installations where Scripts/bin is not on PATH.
    return [sys.executable, "-m", "palworld_save_tools.commands.convert"]


def convert_level_sav(level_sav: Path, output_json: Path) -> None:
    cmd = find_converter() + [
        str(level_sav),
        "--to-json",
        "--minify-json",
        "--force",
        "--output", str(output_json),
        "--custom-properties",
        ".worldSaveData.CharacterSaveParameterMap.Value.RawData",
    ]
    print("Converting Level.sav (read-only)...", file=sys.stderr)
    result = subprocess.run(cmd, text=True, capture_output=True)
    if result.returncode != 0:
        raise RuntimeError(
            "palworld-save-tools could not convert Level.sav.\n"
            f"Command: {' '.join(cmd)}\n"
            f"stdout:\n{result.stdout}\n"
            f"stderr:\n{result.stderr}"
        )


def choose_player(players: list[dict[str, Any]], name: str | None, uid: str | None) -> dict[str, Any]:
    if uid:
        target = norm_guid(uid)
        matches = [p for p in players if p["uid"] == target or p["uid"].endswith(target)]
    elif name:
        exact = [p for p in players if p["name"].casefold() == name.casefold()]
        matches = exact or [p for p in players if name.casefold() in p["name"].casefold()]
    else:
        raise ValueError("Specify --player-name or --player-uid, or use --list-players first.")

    if not matches:
        raise ValueError("No matching player found.")
    if len(matches) > 1:
        lines = [f'  {p["name"]!r}: {p["uid"]}' for p in matches]
        raise ValueError("Multiple players matched:\n" + "\n".join(lines))
    return matches[0]


def write_csv(path: Path, pals: list[dict[str, Any]]) -> None:
    fields = [
        "species_id", "nickname", "level", "gender", "rank",
        "instance_id", "owner_player_uid",
        "passive_skills", "active_skills",
        "talent_hp", "talent_melee", "talent_shot", "talent_defense",
    ]
    with path.open("w", newline="", encoding="utf-8-sig") as fh:
        writer = csv.DictWriter(fh, fieldnames=fields, delimiter=";")
        writer.writeheader()
        for pal in pals:
            row = dict(pal)
            row["passive_skills"] = " | ".join(pal["passive_skills"])
            row["active_skills"] = " | ".join(pal["active_skills"])
            writer.writerow(row)


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Export all Pals owned by one player from Palworld Level.sav."
    )
    parser.add_argument("level_sav", type=Path, help="Path to the world's Level.sav")
    parser.add_argument("--player-name", help="In-game player name")
    parser.add_argument("--player-uid", help="Player UID/GUID")
    parser.add_argument("--list-players", action="store_true", help="Only list detected players")
    parser.add_argument("--output-dir", type=Path, default=Path("export"), help="Output directory")
    parser.add_argument("--keep-json", action="store_true", help="Keep converted Level.sav JSON")
    parser.add_argument("--input-json", type=Path, help="Use an already converted Level.sav JSON")
    args = parser.parse_args()

    level_sav = args.level_sav.resolve()
    if not args.input_json and not level_sav.is_file():
        parser.error(f"Level.sav not found: {level_sav}")

    args.output_dir.mkdir(parents=True, exist_ok=True)

    temp_dir_obj = None
    try:
        if args.input_json:
            json_path = args.input_json.resolve()
        elif args.keep_json:
            json_path = args.output_dir / "Level.sav.json"
            convert_level_sav(level_sav, json_path)
        else:
            temp_dir_obj = tempfile.TemporaryDirectory(prefix="palworld-export-")
            json_path = Path(temp_dir_obj.name) / "Level.sav.json"
            convert_level_sav(level_sav, json_path)

        print("Reading character records...", file=sys.stderr)
        with json_path.open("r", encoding="utf-8") as fh:
            data = json.load(fh)

        records = candidate_character_records(data)
        players = extract_players(records)

        if not players:
            raise RuntimeError(
                "No players were detected. The save schema may have changed, or the converter "
                "did not decode CharacterSaveParameterMap."
            )

        if args.list_players:
            print(json.dumps(players, ensure_ascii=False, indent=2))
            return 0

        player = choose_player(players, args.player_name, args.player_uid)
        pals = [p for r in records if (p := pal_from_record(r, player["uid"])) is not None]
        pals.sort(key=lambda p: (p["species_id"].lower(), str(p["nickname"]).lower(), p["instance_id"]))

        safe_name = "".join(c if c.isalnum() or c in "-_" else "_" for c in player["name"]).strip("_")
        safe_name = safe_name or player["uid"][:8]
        json_out = args.output_dir / f"{safe_name}_pals.json"
        csv_out = args.output_dir / f"{safe_name}_pals.csv"
        summary_out = args.output_dir / f"{safe_name}_summary.json"

        json_out.write_text(json.dumps({
            "player": player,
            "pal_count": len(pals),
            "pals": pals,
        }, ensure_ascii=False, indent=2), encoding="utf-8")
        write_csv(csv_out, pals)

        species = Counter(p["species_id"] for p in pals)
        summary_out.write_text(json.dumps({
            "player": player,
            "pal_count": len(pals),
            "unique_species": len(species),
            "species_counts": dict(sorted(species.items())),
        }, ensure_ascii=False, indent=2), encoding="utf-8")

        print(f"Player: {player['name']} ({player['uid']})")
        print(f"Exported Pals: {len(pals)}")
        print(f"JSON: {json_out.resolve()}")
        print(f"CSV:  {csv_out.resolve()}")
        print(f"Summary: {summary_out.resolve()}")
        return 0
    except (ValueError, RuntimeError, json.JSONDecodeError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1
    finally:
        if temp_dir_obj is not None:
            temp_dir_obj.cleanup()


if __name__ == "__main__":
    raise SystemExit(main())
