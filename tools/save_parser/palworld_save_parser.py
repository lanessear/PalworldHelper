#!/usr/bin/env python3
import argparse
import json
import os
import sys
from pathlib import Path
from typing import Any

from palsav.core import decompress_sav_to_gvas
from palsav.gvas import GvasFile
from palsav.paltypes import PALWORLD_CUSTOM_PROPERTIES, PALWORLD_TYPE_HINTS


def load_character_names() -> dict[str, str]:
    base = Path(getattr(sys, "_MEIPASS", Path(__file__).resolve().parent))
    path = base / "palworld_character_names.json"
    if not path.is_file():
        return {}
    with path.open("r", encoding="utf-8") as file:
        data = json.load(file)
    return {str(key): str(value) for key, value in data.items() if key and value}


def load_passive_skill_names() -> dict[str, str]:
    base = Path(getattr(sys, "_MEIPASS", Path(__file__).resolve().parent))
    path = base / "palworld_passive_skills.json"
    if not path.is_file():
        return {}
    with path.open("r", encoding="utf-8") as file:
        data = json.load(file)
    if not isinstance(data, list):
        return {}
    return {
        str(item.get("id")): str(item.get("name"))
        for item in data
        if isinstance(item, dict) and item.get("id") and item.get("name")
    }


def unwrap(value: Any) -> Any:
    """Unwrap the small property wrappers emitted by palsav without searching siblings."""
    while isinstance(value, dict):
        moved = False
        for key in ("value", "Value", "Str", "Name", "Enum", "Guid", "Bool", "Int", "Int64", "Float"):
            if key in value and len(value) <= 4:
                value = value[key]
                moved = True
                break
        if not moved:
            break
    return value


def direct(container: Any, *keys: str, default: Any = None) -> Any:
    if not isinstance(container, dict):
        return default
    for key in keys:
        if key in container:
            value = unwrap(container[key])
            return default if value is None else value
    return default


def normalize_uid(value: Any) -> str:
    value = unwrap(value)
    if value is None:
        return ""
    return str(value).replace("-", "").lower()


def display_uid(value: Any) -> str:
    value = unwrap(value)
    return "" if value is None else str(value)


def normalize_id(value: Any) -> str:
    value = unwrap(value)
    if value is None:
        return ""
    return str(value).replace("-", "").lower()


def world_save_data(root: dict) -> dict:
    try:
        return root["properties"]["worldSaveData"]["value"]
    except (KeyError, TypeError):
        raise ValueError("The decoded file does not contain Palworld worldSaveData.")


def map_entries(world: dict, name: str) -> list:
    node = world.get(name, {})
    value = node.get("value", []) if isinstance(node, dict) else []
    return value if isinstance(value, list) else []


def save_parameter(entry: dict) -> dict:
    try:
        return entry["value"]["RawData"]["value"]["object"]["SaveParameter"]["value"]
    except (KeyError, TypeError):
        return {}


def player_save_data(root: dict) -> dict:
    try:
        return root["properties"]["SaveData"]["value"]
    except (KeyError, TypeError):
        return {}


def passive_skills(parameter: dict) -> list[str]:
    node = parameter.get("PassiveSkillList", {})
    value = node.get("value", node) if isinstance(node, dict) else node
    if isinstance(value, dict):
        value = value.get("values", value.get("value", []))
    if not isinstance(value, list):
        return []

    result: list[str] = []
    for item in value:
        skill = unwrap(item)
        if isinstance(skill, dict):
            skill = direct(skill, "value", "Name", "Enum", default="")
        skill = str(skill or "")
        if skill and skill not in ("None", "EPalPassiveSkillID::None") and skill not in result:
            result.append(skill)
    return result


def guild_player_names(world: dict) -> dict[str, str]:
    """Read the authoritative player names from guild membership records."""
    names: dict[str, str] = {}
    for group in map_entries(world, "GroupSaveDataMap"):
        try:
            group_type = unwrap(group["value"]["GroupType"])
            if str(group_type) != "EPalGroupType::Guild":
                continue
            raw = group["value"]["RawData"]["value"]
        except (KeyError, TypeError):
            continue

        for member in raw.get("players", []):
            if not isinstance(member, dict):
                continue
            uid = normalize_uid(member.get("player_uid"))
            info = member.get("player_info", {})
            name = info.get("player_name", "") if isinstance(info, dict) else ""
            if uid and name:
                names[uid] = str(name)
    return names


def parse_player_container_file(path: Path) -> tuple[str, dict[str, str]] | None:
    try:
        with path.open("rb") as file:
            raw, _ = decompress_sav_to_gvas(file.read())
        decoded = GvasFile.read(raw, PALWORLD_TYPE_HINTS, PALWORLD_CUSTOM_PROPERTIES, allow_nan=False).dump()
        save_data = player_save_data(decoded)
    except Exception:
        return None

    stem = path.stem.lower()
    player_uid = stem.removesuffix("_dps").replace("-", "")
    palbox_id = direct(save_data, "PalStorageContainerId", default={})
    party_id = direct(save_data, "OtomoCharacterContainerId", default={})
    result = {
        "palbox": normalize_id(palbox_id.get("ID") if isinstance(palbox_id, dict) else palbox_id),
        "party": normalize_id(party_id.get("ID") if isinstance(party_id, dict) else party_id),
    }
    result = {key: value for key, value in result.items() if value}
    return (player_uid, result) if result else None


def player_container_ids(players_dir: str | None) -> dict[str, dict[str, str]]:
    if not players_dir:
        return {}
    directory = Path(players_dir)
    if not directory.is_dir():
        return {}

    result: dict[str, dict[str, str]] = {}
    for path in directory.glob("*.sav"):
        parsed = parse_player_container_file(path)
        if parsed is None:
            continue
        uid, containers = parsed
        result.setdefault(uid, {}).update(containers)
    return result


def container_slots(world: dict) -> dict[str, dict[str, Any]]:
    slots_by_instance: dict[str, dict[str, Any]] = {}
    for container in map_entries(world, "CharacterContainerSaveData"):
        container_id = normalize_id(direct(container.get("key", {}), "ID", default=""))
        slot_values = direct(container.get("value", {}), "Slots", default={})
        if isinstance(slot_values, dict):
            slot_values = slot_values.get("values", [])
        if not container_id or not isinstance(slot_values, list):
            continue

        for slot in slot_values:
            raw = direct(slot, "RawData", default={})
            if not isinstance(raw, dict):
                continue
            instance_id = normalize_id(raw.get("instance_id"))
            if not instance_id or instance_id == "00000000000000000000000000000000":
                continue
            slots_by_instance[instance_id] = {
                "containerId": container_id,
                "slotIndex": direct(slot, "SlotIndex", default=0),
                "playerUid": display_uid(raw.get("player_uid")),
                "playerUidNormalized": normalize_uid(raw.get("player_uid")),
            }
    return slots_by_instance


def parse_character(entry: dict) -> dict:
    parameter = save_parameter(entry)
    key = entry.get("key", {}) if isinstance(entry, dict) else {}

    player_uid_raw = direct(key, "PlayerUId", "PlayerUID", default="")
    instance_id_raw = direct(key, "InstanceId", "InstanceID", default="")
    owner_uid_raw = direct(parameter, "OwnerPlayerUId", "OwnerPlayerUID", default="")

    is_player = bool(direct(parameter, "IsPlayer", default=False))
    nickname = direct(parameter, "NickName", "Nickname", default="")
    species = direct(parameter, "CharacterID", "CharacterId", default="")
    level = direct(parameter, "Level", default=0)
    gender = direct(parameter, "Gender", default="")

    return {
        "isPlayer": is_player,
        "playerUid": display_uid(player_uid_raw),
        "playerUidNormalized": normalize_uid(player_uid_raw),
        "ownerPlayerUid": display_uid(owner_uid_raw),
        "ownerPlayerUidNormalized": normalize_uid(owner_uid_raw),
        "instanceId": display_uid(instance_id_raw),
        "name": str(nickname or ""),
        "species": str(species or ""),
        "level": int(level or 0),
        "gender": str(gender or ""),
        "passiveSkills": passive_skills(parameter),
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("save")
    parser.add_argument("output")
    parser.add_argument("--players-dir", default="")
    args = parser.parse_args()

    if not os.path.isfile(args.save):
        raise FileNotFoundError(args.save)

    with open(args.save, "rb") as file:
        raw, save_type = decompress_sav_to_gvas(file.read())

    wanted = {
        key: value
        for key, value in PALWORLD_CUSTOM_PROPERTIES.items()
        if "CharacterSaveParameterMap" in key or "GroupSaveDataMap" in key or "CharacterContainerSaveData" in key
    }
    decoded = GvasFile.read(raw, PALWORLD_TYPE_HINTS, wanted, allow_nan=False).dump()
    world = world_save_data(decoded)
    character_names = load_character_names()
    passive_skill_names = load_passive_skill_names()

    characters = [parse_character(entry) for entry in map_entries(world, "CharacterSaveParameterMap")]
    guild_names = guild_player_names(world)
    player_containers = player_container_ids(args.players_dir)
    slots_by_instance = container_slots(world)

    # Use one record per player UID. Guild data is authoritative; the character nickname is only a fallback.
    player_by_uid: dict[str, dict] = {}
    for character in characters:
        if not character["isPlayer"]:
            continue
        normalized_uid = character["playerUidNormalized"]
        key = normalized_uid or character["instanceId"]
        if not key:
            continue
        player_by_uid[key] = {
            "name": guild_names.get(normalized_uid) or character["name"] or character["playerUid"] or "Unknown player",
            "playerUid": character["playerUid"],
            "level": character["level"],
        }

    players = list(player_by_uid.values())
    container_to_owner: dict[str, dict[str, str]] = {}
    for uid, containers in player_containers.items():
        owner_name = guild_names.get(uid) or player_by_uid.get(uid, {}).get("name") or uid
        for container_type, container_id in containers.items():
            container_to_owner[container_id] = {
                "ownerName": owner_name,
                "ownerUid": uid,
                "containerType": "Dimensional Pal Storage" if container_type == "palbox" else "Party",
            }

    pals = []
    for character in characters:
        if character["isPlayer"]:
            continue
        owner_uid = character["ownerPlayerUidNormalized"]
        owner_name = guild_names.get(owner_uid)
        if not owner_name and owner_uid in player_by_uid:
            owner_name = player_by_uid[owner_uid]["name"]
        slot = slots_by_instance.get(normalize_id(character["instanceId"]), {})
        container_owner = container_to_owner.get(slot.get("containerId", ""))
        if container_owner:
            owner_name = container_owner["ownerName"]
            owner_uid = container_owner["ownerUid"]
        species = character_names.get(character["species"], character["species"])
        pals.append({
            "owner": owner_name or character["ownerPlayerUid"] or "World / base",
            "ownerPlayerUid": character["ownerPlayerUid"] or owner_uid,
            "species": species,
            "characterId": character["species"],
            "nickname": character["name"],
            "level": character["level"],
            "gender": character["gender"],
            "passiveSkills": [passive_skill_names.get(skill, skill) for skill in character["passiveSkills"]],
            "instanceId": character["instanceId"],
            "storage": container_owner["containerType"] if container_owner else ("Container" if slot else "World / base"),
            "containerId": slot.get("containerId", ""),
            "slotIndex": slot.get("slotIndex", 0),
        })

    result = {
        "parser": "palsav-flex",
        "saveType": save_type,
        "playerCount": len(players),
        "palCount": len(pals),
        "players": players,
        "pals": pals,
    }
    with open(args.output, "w", encoding="utf-8") as file:
        json.dump(result, file, ensure_ascii=False, separators=(",", ":"))


if __name__ == "__main__":
    try:
        main()
    except Exception as exception:
        print(f"Parser error: {exception}", file=sys.stderr)
        sys.exit(1)
