$ErrorActionPreference = 'Stop'

$dataDirectory = Join-Path $PSScriptRoot 'src/PalworldHelper/Embedded/BreedingData'
$parts = @(1..15 | ForEach-Object {
    Join-Path $dataDirectory ('part{0:D2}.txt' -f $_)
})

$builder = [Text.StringBuilder]::new()
foreach ($part in $parts) {
    if (-not (Test-Path $part)) {
        throw "Missing part: $part"
    }

    $raw = Get-Content $part -Raw
    $clean = [regex]::Replace($raw, '[^A-Za-z0-9+/=]', '')
    [void]$builder.Append($clean)
}

$base64 = $builder.ToString()
if ($base64.Length -ne 215048) {
    throw "Base64 length is $($base64.Length), expected 215048."
}

$compressed = [Convert]::FromBase64String($base64)
$gzipHash = [Convert]::ToHexString(
    [Security.Cryptography.SHA256]::HashData($compressed)
).ToLowerInvariant()

if ($gzipHash -ne 'ae292cc8f8b0db54e5c4462507e39274654f78f13c613c23d9b23c910f439934') {
    throw "GZIP SHA-256 mismatch: $gzipHash"
}

$input = [IO.MemoryStream]::new($compressed)
$gzip = [IO.Compression.GZipStream]::new(
    $input,
    [IO.Compression.CompressionMode]::Decompress
)
$output = [IO.MemoryStream]::new()

try {
    $gzip.CopyTo($output)
} finally {
    $gzip.Dispose()
    $input.Dispose()
}

$jsonBytes = $output.ToArray()
$output.Dispose()

$jsonHash = [Convert]::ToHexString(
    [Security.Cryptography.SHA256]::HashData($jsonBytes)
).ToLowerInvariant()

if ($jsonHash -ne '51ceacd65d7e484738d95f662362591888a30b50c756d7285a660c06ed4ac74f') {
    throw "JSON SHA-256 mismatch: $jsonHash"
}

$jsonText = [Text.Encoding]::UTF8.GetString($jsonBytes)
$json = $jsonText | ConvertFrom-Json

if ($json.pals.Count -ne 298) {
    throw "Expected 298 pals, got $($json.pals.Count)."
}
if ($json.results.Count -ne 44253) {
    throw "Expected 44253 results, got $($json.results.Count)."
}
if (@($json.results | Where-Object { $_[0] -eq $_[1] }).Count -ne 0) {
    throw 'Default dataset still contains self-breeding rows.'
}

Write-Host 'Default breeding data is valid.'
Write-Host "Pals: $($json.pals.Count)"
Write-Host "Results: $($json.results.Count)"
Write-Host "GZIP SHA-256: $gzipHash"
Write-Host "JSON SHA-256: $jsonHash"
