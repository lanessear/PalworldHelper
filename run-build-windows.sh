#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "$0")" && pwd)"
cd "$repo_root"

if command -v pwsh >/dev/null 2>&1; then
  pwsh -ExecutionPolicy Bypass -File ./build-windows.ps1
elif [ -x /mnt/c/Windows/System32/WindowsPowerShell/v1.0/powershell.exe ]; then
  /mnt/c/Windows/System32/WindowsPowerShell/v1.0/powershell.exe -ExecutionPolicy Bypass -File ./build-windows.ps1
elif command -v powershell.exe >/dev/null 2>&1; then
  powershell.exe -ExecutionPolicy Bypass -File ./build-windows.ps1
else
  echo "PowerShell is required to run this script. Install pwsh or ensure powershell.exe is available." >&2
  exit 1
fi
