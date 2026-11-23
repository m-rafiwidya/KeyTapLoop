# Zips KeyTapLoop.exe with the license and writes the SHA-256 hash next to the zip.
# Pass the signed executable here when you have one; otherwise pass the freshly built one.
# Usage:  ./scripts/package.ps1 -ExePath path\to\KeyTapLoop.exe
param([Parameter(Mandatory = $true)][string]$ExePath)
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if (-not (Test-Path $ExePath)) { throw "Executable not found: $ExePath" }

$version = ([xml](Get-Content Directory.Build.props)).Project.PropertyGroup.Version
$name    = "KeyTapLoop-v$version-win-x64"
$out     = Join-Path $root 'artifacts'
$content = Join-Path $out 'package'
$zip     = Join-Path $out "$name.zip"

New-Item -ItemType Directory -Force $content | Out-Null
Copy-Item $ExePath (Join-Path $content 'KeyTapLoop.exe') -Force
Copy-Item (Join-Path $root 'LICENSE') $content -Force
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $content 'KeyTapLoop.exe'), (Join-Path $content 'LICENSE') -DestinationPath $zip

# "<hash>  <file>" is the format `sha256sum -c` understands.
$zipHash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower()
"$zipHash  $name.zip" | Set-Content -Encoding ascii "$zip.sha256"

$exeHash = (Get-FileHash (Join-Path $content 'KeyTapLoop.exe') -Algorithm SHA256).Hash.ToLower()
$signature = (Get-AuthenticodeSignature (Join-Path $content 'KeyTapLoop.exe')).Status

Write-Host ''
Write-Host "Built      $zip"
Write-Host "SHA-256    $zipHash  ($name.zip)"
Write-Host "SHA-256    $exeHash  (KeyTapLoop.exe)"
Write-Host "Signature  $signature"
