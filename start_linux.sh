#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
if [[ ! -x .venv/bin/python ]]; then
  python3 -m venv .venv
  .venv/bin/python -m pip install --upgrade pip
  .venv/bin/python -m pip install -r requirements.txt
fi
( sleep 1; command -v xdg-open >/dev/null && xdg-open http://127.0.0.1:8765 >/dev/null 2>&1 || true ) &
exec .venv/bin/python server.py
