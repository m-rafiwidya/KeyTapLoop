# Builds the portable Windows x64 release.
# Usage (from the repository root, on Windows):
#   ./scripts/publish.ps1             build KeyTapLoop.exe, then zip it and write the SHA-256 hash
#   ./scripts/publish.ps1 -NoPackage  build KeyTapLoop.exe only (used when the exe is signed before packaging)
param([switch]$NoPackage)
$ErrorActionPreference = 'Stop'

$root  = Split-Path -Parent $PSScriptRoot
Set-Location $root

$out   = Join-Path $root 'artifacts'
$stage = Join-Path $out 'KeyTapLoop'

if (Test-Path $out) { Remove-Item $out -Recurse -Force }

dotnet publish src/KeyTapLoop -c Release -r win-x64 --self-contained -o $stage
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

$exe = Join-Path $stage 'KeyTapLoop.exe'
if ($NoPackage) {
    Write-Host "Built $exe"
    return
}

& (Join-Path $PSScriptRoot 'package.ps1') -ExePath $exe
