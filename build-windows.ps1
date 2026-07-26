$ErrorActionPreference = 'Stop'
$repoRoot = $PSScriptRoot
$publishRoot = Join-Path (Split-Path -Parent $repoRoot) 'publish'

$parserSource = 'https://github.com/deafdudecomputers/PalworldSaveTools.git'
$gitExe = $null
foreach ($candidate in @(
    'git',
    'C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd\git.exe',
    'C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\mingw64\bin\git.exe'
)) {
    if (Get-Command $candidate -ErrorAction SilentlyContinue) {
        $gitExe = (Get-Command $candidate).Source
        break
    }
}
if (-not $gitExe) {
    throw 'Git executable not found.'
}

$pythonExe = $null
$pythonArgs = @()
foreach ($candidate in @('py', 'python', 'python3', 'python.exe')) {
    if (Get-Command $candidate -ErrorAction SilentlyContinue) {
        $resolved = (Get-Command $candidate).Source
        if ($candidate -eq 'py') {
            $pythonExe = $resolved
            $pythonArgs = @('-3')
        }
        else {
            $pythonExe = $resolved
            $pythonArgs = @()
        }
        break
    }
}
if (-not $pythonExe) {
    throw 'Python executable not found.'
}

Remove-Item parser-upstream, dist, build -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $publishRoot | Out-Null
Remove-Item $publishRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $publishRoot | Out-Null

& $gitExe clone --depth 1 $parserSource parser-upstream
$parserCommit = & $gitExe -C parser-upstream rev-parse HEAD
Write-Host "Parser source: $parserSource@$parserCommit"
& $pythonExe @pythonArgs -m pip install --disable-pip-version-check pyinstaller==6.14.2
& $pythonExe @pythonArgs -m pip install .\parser-upstream\src\palsav\palooz
& $pythonExe @pythonArgs -m pip install .\parser-upstream\src\palsav
& $pythonExe @pythonArgs -m PyInstaller --noconfirm --clean --onefile --name PalworldSaveParser --collect-all palsav --collect-all palooz --add-data=".\tools\save_parser\palworld_character_names.json:." --add-data=".\tools\save_parser\palworld_passive_skills.json:." .\tools\save_parser\palworld_save_parser.py
dotnet restore .\PalworldHelper.sln
dotnet publish .\src\PalworldHelper\PalworldHelper.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $publishRoot
New-Item -ItemType Directory -Force (Join-Path $publishRoot 'parser') | Out-Null
Copy-Item .\dist\PalworldSaveParser.exe (Join-Path $publishRoot 'parser/PalworldSaveParser.exe') -Force
Write-Host "Fertig: $publishRoot/PalworldHelper.exe und $publishRoot/parser/PalworldSaveParser.exe" -ForegroundColor Green
