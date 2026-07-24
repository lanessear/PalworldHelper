#!/usr/bin/env python3
from __future__ import annotations

import json
import os
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path
from typing import Any

from flask import Flask, jsonify, render_template, request, send_from_directory

BASE = Path(getattr(sys, "_MEIPASS", Path(__file__).resolve().parent))
WRITABLE_BASE = Path(sys.executable).resolve().parent if getattr(sys, "frozen", False) else Path(__file__).resolve().parent
DATA_DIR = WRITABLE_BASE / "data"
DATA_DIR.mkdir(exist_ok=True)
app = Flask(__name__)
app.config["MAX_CONTENT_LENGTH"] = 128 * 1024 * 1024


def safe_json(path: Path, default: Any) -> Any:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return default


def export_from_save(save_path: Path, player_name: str) -> dict[str, Any]:
    with tempfile.TemporaryDirectory(prefix="palworld-assistant-") as temp:
        out = Path(temp) / "export"
        cmd = [
            sys.executable,
            str(BASE / "pal_exporter.py"),
            str(save_path),
            "--player-name",
            player_name,
            "--output-dir",
            str(out),
        ]
        proc = subprocess.run(cmd, capture_output=True, text=True, timeout=600)
        if proc.returncode != 0:
            raise RuntimeError(proc.stderr.strip() or proc.stdout.strip() or "Export fehlgeschlagen")
        files = list(out.glob("*_pals.json"))
        if not files:
            raise RuntimeError("Der Exporter hat keine Pal-Datei erzeugt.")
        payload = json.loads(files[0].read_text(encoding="utf-8"))
        (DATA_DIR / "latest_collection.json").write_text(
            json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8"
        )
        return payload


def fetch_ssh(form: dict[str, Any], target: Path) -> None:
    import paramiko

    host = str(form.get("host", "")).strip()
    username = str(form.get("username", "")).strip()
    remote_path = str(form.get("savePath", "")).strip()
    if not host or not username or not remote_path:
        raise ValueError("Host, SSH-Benutzer und Level.sav-Pfad werden benötigt.")

    port = int(form.get("port") or 22)
    password = str(form.get("password", "")) or None
    key_path = str(form.get("keyPath", "")).strip() or None

    client = paramiko.SSHClient()
    client.load_system_host_keys()
    # First connection is convenient, but the fingerprint is exposed to the UI response.
    client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    kwargs: dict[str, Any] = {
        "hostname": host,
        "port": port,
        "username": username,
        "timeout": 20,
        "banner_timeout": 20,
        "auth_timeout": 20,
        "look_for_keys": True,
        "allow_agent": True,
    }
    if password:
        kwargs["password"] = password
    if key_path:
        kwargs["key_filename"] = os.path.expanduser(key_path)
    try:
        client.connect(**kwargs)
        with client.open_sftp() as sftp:
            sftp.get(remote_path, str(target))
    finally:
        client.close()


@app.get("/")
def index():
    return render_template("index.html")


@app.get("/api/status")
def status():
    collection = safe_json(DATA_DIR / "latest_collection.json", None)
    breeding = safe_json(DATA_DIR / "palworld_breeding_results.json", None)
    return jsonify({
        "ok": True,
        "collectionLoaded": bool(collection),
        "palCount": (collection or {}).get("pal_count", 0),
        "player": (collection or {}).get("player", {}).get("name", ""),
        "breedingLoaded": bool(breeding and isinstance(breeding.get("results"), list)),
        "breedingCount": len((breeding or {}).get("results", [])),
    })


@app.get("/api/collection")
def collection():
    payload = safe_json(DATA_DIR / "latest_collection.json", None)
    if not payload:
        return jsonify({"error": "Noch keine Sammlung importiert."}), 404
    return jsonify(payload)


@app.get("/api/breeding")
def breeding():
    payload = safe_json(DATA_DIR / "palworld_breeding_results.json", None)
    if not payload:
        return jsonify({"error": "Noch keine Zucht-JSON hinterlegt."}), 404
    return jsonify(payload)


@app.post("/api/breeding/upload")
def breeding_upload():
    uploaded = request.files.get("file")
    if not uploaded:
        return jsonify({"error": "Keine JSON-Datei übertragen."}), 400
    try:
        payload = json.load(uploaded.stream)
        if not isinstance(payload, dict) or not isinstance(payload.get("results"), list):
            raise ValueError("Die JSON muss ein Array im Feld 'results' enthalten.")
        target = DATA_DIR / "palworld_breeding_results.json"
        target.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
        return jsonify({"ok": True, "count": len(payload["results"])})
    except (ValueError, json.JSONDecodeError) as exc:
        return jsonify({"error": str(exc)}), 400


@app.post("/api/import")
def import_collection():
    form = request.get_json(force=True, silent=False) or {}
    mode = str(form.get("mode", "ssh"))
    player_name = str(form.get("playerName", "Lanessear")).strip() or "Lanessear"
    try:
        if mode == "local":
            path = Path(str(form.get("savePath", ""))).expanduser().resolve()
            if not path.is_file():
                raise ValueError(f"Level.sav nicht gefunden: {path}")
            payload = export_from_save(path, player_name)
        else:
            with tempfile.TemporaryDirectory(prefix="palworld-download-") as temp:
                local_save = Path(temp) / "Level.sav"
                fetch_ssh(form, local_save)
                payload = export_from_save(local_save, player_name)
        return jsonify({
            "ok": True,
            "player": payload.get("player", {}),
            "pal_count": payload.get("pal_count", len(payload.get("pals", []))),
            "pals": payload.get("pals", []),
        })
    except subprocess.TimeoutExpired:
        return jsonify({"error": "Die Save-Konvertierung dauerte länger als 10 Minuten."}), 504
    except Exception as exc:
        return jsonify({"error": str(exc)}), 400


@app.get("/api/config/<name>")
def config(name: str):
    allowed = {"passive_skill_aliases.json", "pal_name_aliases.json"}
    if name not in allowed:
        return jsonify({"error": "Unbekannte Konfiguration."}), 404
    return send_from_directory(BASE, name)


if __name__ == "__main__":
    # Deliberately localhost-only: credentials must never be exposed to the LAN.
    app.run(host="127.0.0.1", port=8765, debug=False)
