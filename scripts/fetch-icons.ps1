$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$manifest = Get-Content -LiteralPath (Join-Path $projectRoot 'assets\icons8.json') -Raw | ConvertFrom-Json
$cache = Join-Path $projectRoot 'bin\icons'
New-Item -ItemType Directory -Path $cache -Force | Out-Null
foreach ($icon in $manifest.icons) {
    $path = Join-Path $cache ($icon.name + '.png')
    if (!(Test-Path -LiteralPath $path)) {
        Invoke-WebRequest -Uri "https://img.icons8.com/sf-regular/100/$($icon.name).png" -OutFile $path
    }
    $hasher = [Security.Cryptography.SHA256]::Create()
    $stream = [IO.File]::OpenRead($path)
    try { $checksum = [BitConverter]::ToString($hasher.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
    finally { $stream.Dispose(); $hasher.Dispose() }
    if ($checksum -ne $icon.sha256) {
        throw "Icons8 asset checksum changed: $($icon.name). Review the asset before updating the manifest."
    }
}
$license = Join-Path $cache 'Icons8-license.html'
if (!(Test-Path -LiteralPath $license)) { Invoke-WebRequest -Uri $manifest.license -OutFile $license }
