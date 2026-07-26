$ErrorActionPreference = 'Stop'
$repoRoot = $PSScriptRoot
$publicRoot = Join-Path (Split-Path -Parent $repoRoot) 'public'
$publishRoot = Join-Path $publicRoot 'publish'

$parserSource = 'https://github.com/deafdudecomputers/PalworldSaveTools.git'
Remove-Item parser-upstream, dist, build -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $publicRoot | Out-Null
Remove-Item $publishRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $publishRoot | Out-Null

git clone --depth 1 $parserSource parser-upstream
$parserCommit = git -C parser-upstream rev-parse HEAD
Write-Host "Parser source: $parserSource@$parserCommit"
python -m pip install --disable-pip-version-check pyinstaller==6.14.2
python -m pip install .\parser-upstream\src\palsav\palooz
python -m pip install .\parser-upstream\src\palsav
python -m PyInstaller --noconfirm --clean --onefile --name PalworldSaveParser --collect-all palsav --collect-all palooz --add-data=".\tools\save_parser\palworld_character_names.json:." --add-data=".\tools\save_parser\palworld_passive_skills.json:." .\tools\save_parser\palworld_save_parser.py
dotnet restore .\PalworldHelper.sln
dotnet publish .\src\PalworldHelper\PalworldHelper.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $publishRoot
New-Item -ItemType Directory -Force (Join-Path $publishRoot 'parser') | Out-Null
Copy-Item .\dist\PalworldSaveParser.exe (Join-Path $publishRoot 'parser/PalworldSaveParser.exe') -Force
Write-Host "Fertig: $publishRoot/PalworldHelper.exe und $publishRoot/parser/PalworldSaveParser.exe" -ForegroundColor Green
