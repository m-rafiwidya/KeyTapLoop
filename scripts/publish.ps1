# Builds the portable Windows x64 release and writes its SHA-256 hash.
# Usage (from the repository root, on Windows):  ./scripts/publish.ps1
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$version = ([xml](Get-Content Directory.Build.props)).Project.PropertyGroup.Version
$name    = "KeyTapLoop-v$version-win-x64"
$out     = Join-Path $root 'artifacts'
$stage   = Join-Path $out 'KeyTapLoop'
$zip     = Join-Path $out "$name.zip"

if (Test-Path $out) { Remove-Item $out -Recurse -Force }

dotnet publish src/KeyTapLoop -c Release -r win-x64 --self-contained -o $stage
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

Copy-Item LICENSE $stage
Compress-Archive -Path (Join-Path $stage 'KeyTapLoop.exe'), (Join-Path $stage 'LICENSE') -DestinationPath $zip

# "<hash>  <file>" is the format `sha256sum -c` understands.
$zipHash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower()
"$zipHash  $name.zip" | Set-Content -Encoding ascii "$zip.sha256"

$exeHash = (Get-FileHash (Join-Path $stage 'KeyTapLoop.exe') -Algorithm SHA256).Hash.ToLower()

Write-Host ''
Write-Host "Built    $zip"
Write-Host "SHA-256  $zipHash  ($name.zip)"
Write-Host "SHA-256  $exeHash  (KeyTapLoop.exe)"
