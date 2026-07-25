$ErrorActionPreference = 'Stop'
$parserSource = 'https://github.com/deafdudecomputers/PalworldSaveTools.git'
Remove-Item parser-upstream, dist, build -Recurse -Force -ErrorAction SilentlyContinue
git clone --depth 1 $parserSource parser-upstream
$parserCommit = git -C parser-upstream rev-parse HEAD
Write-Host "Parser source: $parserSource@$parserCommit"
python -m pip install --disable-pip-version-check pyinstaller==6.14.2
python -m pip install .\parser-upstream\src\palsav\palooz
python -m pip install .\parser-upstream\src\palsav
python -m PyInstaller --noconfirm --clean --onefile --name PalworldSaveParser --collect-all palsav --collect-all palooz .\tools\save_parser\palworld_save_parser.py
dotnet restore .\PalworldHelper.sln
dotnet publish .\src\PalworldHelper\PalworldHelper.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish
New-Item -ItemType Directory -Force .\publish\parser | Out-Null
Copy-Item .\dist\PalworldSaveParser.exe .\publish\parser\PalworldSaveParser.exe -Force
Write-Host "Fertig: .\publish\PalworldHelper.exe und .\publish\parser\PalworldSaveParser.exe" -ForegroundColor Green
