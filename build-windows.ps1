$ErrorActionPreference = 'Stop'
dotnet restore .\PalworldHelper.sln
dotnet publish .\src\PalworldHelper\PalworldHelper.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish
Write-Host "Fertig: .\publish\PalworldHelper.exe" -ForegroundColor Green
