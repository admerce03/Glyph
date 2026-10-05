#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Publish Glyph as a self-contained MSIX package (Milestone 9 / ADR-006, ADR-012).

.DESCRIPTION
  Keeps the default solution build unpackaged (WindowsPackageType=None).
  Passes -p:GlyphPackage=MSIX so the app csproj enables MSIX tooling for this publish only.

  Unsigned packages are fine for local sideload with developer mode / test certs.
  Production signing and Store submission remain a separate distribution step.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64',

    # Relative to repo root, or an absolute path. Default: artifacts/msix
    [string]$Output = 'artifacts/msix'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $repoRoot 'src' 'Glyph.App' 'Glyph.App.csproj'

if ([System.IO.Path]::IsPathRooted($Output)) {
    $outDir = $Output
}
else {
    $outDir = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Output))
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

Write-Host "Publishing Glyph MSIX ($Configuration / $Runtime) → $outDir"

dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:GlyphPackage=MSIX `
    -p:WindowsAppSDKSelfContained=true `
    -p:PublishReadyToRun=false `
    -o $outDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$msix = Get-ChildItem -Path $outDir -Filter *.msix -Recurse -ErrorAction SilentlyContinue
$manifest = Get-ChildItem -Path $outDir -Filter AppxManifest.xml -Recurse -ErrorAction SilentlyContinue
Write-Host "MSIX publish completed under $outDir"
if ($msix) {
    Write-Host ("Found MSIX: " + (($msix | ForEach-Object FullName) -join ', '))
}
elseif ($manifest) {
    Write-Host ("Found AppxManifest layout: " + (($manifest | ForEach-Object FullName) -join ', '))
}
else {
    Write-Host "Note: no .msix / AppxManifest.xml found yet — inspect $outDir for publish layout."
}
