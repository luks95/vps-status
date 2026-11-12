#requires -Version 7
<#
.SYNOPSIS
  Publica VPS Monitor (win-x64, autocontenido) y genera el instalador con Inno Setup 6.
.EXAMPLE
  ./installer/build-installer.ps1 -Version 1.1.0
  Resultado: artifacts/installer/VPSMonitor-Setup-1.1.0.exe
#>
param(
    [string]$Version = "1.1.0"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $root "artifacts/VPSMonitor-win-x64"

$candidates = @(
    (Join-Path $env:LOCALAPPDATA "Programs/Inno Setup 6/ISCC.exe"),
    "${env:ProgramFiles(x86)}/Inno Setup 6/ISCC.exe",
    "$env:ProgramFiles/Inno Setup 6/ISCC.exe"
)
$iscc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    throw "No se encontró Inno Setup 6. Instálalo con: winget install JRSoftware.InnoSetup"
}

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

dotnet publish (Join-Path $root "src/VPSMonitor.App/VPSMonitor.App.csproj") `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:Version=$Version `
    --output $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish falló." }

& $iscc "/DAppVersion=$Version" (Join-Path $PSScriptRoot "VPSMonitor.iss")
if ($LASTEXITCODE -ne 0) { throw "La compilación del instalador falló." }

Write-Host "Instalador: $(Join-Path $root "artifacts/installer/VPSMonitor-Setup-$Version.exe")"
