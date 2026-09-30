param([switch]$Test, [switch]$Package, [string]$NativeTools)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw 'The Windows .NET Framework C# compiler is required.' }
$output = Join-Path $projectRoot 'dist\Revora'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$sourceRoot = Join-Path $projectRoot 'src\Revora'
$appIcon = Join-Path $projectRoot 'assets\revora.ico'
$sources = @(Get-ChildItem -LiteralPath $sourceRoot -Filter '*.cs' | ForEach-Object FullName)
$references = @('/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll', '/r:System.Xml.dll', '/r:System.Xml.Linq.dll', '/r:System.IO.Compression.dll', '/r:System.IO.Compression.FileSystem.dll')
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /warn:4 /warnaserror+ "/out:$output\Revora.exe" "/win32manifest:$sourceRoot\app.manifest" "/win32icon:$appIcon" @references @sources
if ($LASTEXITCODE -ne 0) { throw 'Revora compilation failed.' }
Copy-Item -LiteralPath (Join-Path $sourceRoot 'Revora.exe.config') -Destination $output
foreach ($document in @('README.md', 'LICENSE', 'THIRD-PARTY.md')) {
    if (Test-Path -LiteralPath (Join-Path $projectRoot $document)) { Copy-Item -LiteralPath (Join-Path $projectRoot $document) -Destination $output }
}
if ($Test) {
    $testOutput = Join-Path $projectRoot 'bin\tests'
    New-Item -ItemType Directory -Path $testOutput -Force | Out-Null
    $core = @('Device.cs', 'DeviceService.cs', 'Firmware.cs', 'ToolRunner.cs') | ForEach-Object { Join-Path $sourceRoot $_ }
    & $compiler /nologo /target:exe /platform:x64 /warn:4 /warnaserror+ "/out:$testOutput\Revora.Tests.exe" @references @core (Join-Path $projectRoot 'tests\Tests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    & (Join-Path $testOutput 'Revora.Tests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Revora tests failed.' }
    & $compiler /nologo /target:exe /platform:x64 /warn:4 /warnaserror+ /main:UiSmoke "/out:$testOutput\Revora.UiSmoke.exe" "/win32icon:$appIcon" @references @sources (Join-Path $projectRoot 'tests\UiSmoke.cs')
    if ($LASTEXITCODE -ne 0) { throw 'UI smoke test compilation failed.' }
    & (Join-Path $testOutput 'Revora.UiSmoke.exe') $testOutput
    if ($LASTEXITCODE -ne 0) { throw 'Revora UI smoke checks failed.' }
}
if ($NativeTools) {
    $testOutput = Join-Path $projectRoot 'bin\tests'
    New-Item -ItemType Directory -Path $testOutput -Force | Out-Null
    $core = @('Device.cs', 'Firmware.cs', 'ToolRunner.cs') | ForEach-Object { Join-Path $sourceRoot $_ }
    & $compiler /nologo /target:exe /platform:x64 /warn:4 /warnaserror+ "/out:$testOutput\Revora.NativeSmoke.exe" @references @core (Join-Path $projectRoot 'tests\NativeSmoke.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Native smoke test compilation failed.' }
    & (Join-Path $testOutput 'Revora.NativeSmoke.exe') ([System.IO.Path]::GetFullPath($NativeTools))
    if ($LASTEXITCODE -ne 0) { throw 'Native tool smoke checks failed.' }
}
if ($Package) {
    $tools = Join-Path $output 'tools'
    foreach ($tool in @('idevice_id', 'ideviceinfo', 'ideviceenterrecovery', 'irecovery', 'idevicerestore', 'plistutil')) {
        if (!(Test-Path -LiteralPath (Join-Path $tools "$tool.exe"))) { throw "Cannot package: missing $tool.exe. Build native tools first." }
    }
    $archive = Join-Path $projectRoot 'dist\Revora-windows-x64.zip'
    Compress-Archive -Path "$output\*" -DestinationPath $archive -Force
    Get-FileHash -LiteralPath $archive -Algorithm SHA256 | ForEach-Object { "$($_.Hash.ToLowerInvariant())  Revora-windows-x64.zip" } | Set-Content -LiteralPath (Join-Path $projectRoot 'dist\SHA256SUMS.txt') -Encoding ASCII
    Write-Host "Package: $archive"
}
Write-Host "Executable: $output\Revora.exe"
