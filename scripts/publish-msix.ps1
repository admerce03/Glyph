#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Build Glyph as a single-project MSIX package (Milestone 9 / ADR-006, ADR-012).

.DESCRIPTION
  Keeps the default solution build unpackaged (WindowsPackageType=None).
  Passes -p:GlyphPackage=MSIX + GenerateAppxPackageOnBuild so WinUI single-project
  MSIX tooling emits a .msix (see Microsoft Learn: single-project MSIX).

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
$platform = if ($Runtime -eq 'win-arm64') { 'ARM64' } else { 'x64' }

if ([System.IO.Path]::IsPathRooted($Output)) {
    $outDir = $Output
}
else {
    $outDir = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Output))
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

Write-Host "Building Glyph MSIX ($Configuration / $Runtime / $platform) → $outDir"

# Single-project MSIX uses GenerateAppxPackageOnBuild (not plain dotnet publish -o).
# https://learn.microsoft.com/windows/apps/windows-app-sdk/single-project-msix
dotnet build $project `
    -c $Configuration `
    -r $Runtime `
    -p:Platform=$platform `
    -p:GlyphPackage=MSIX `
    -p:WindowsAppSDKSelfContained=true `
    -p:GenerateAppxPackageOnBuild=true `
    -p:AppxPackageSigningEnabled=false `
    -p:AppxBundle=Never `
    -p:AppxPackageDir="$outDir\\"

if ($LASTEXITCODE -ne 0) {
    throw "dotnet build (MSIX) failed with exit code $LASTEXITCODE"
}

$msix = @(Get-ChildItem -Path $outDir -Filter *.msix -Recurse -ErrorAction SilentlyContinue)
$manifest = @(Get-ChildItem -Path $outDir -Filter AppxManifest.xml -Recurse -ErrorAction SilentlyContinue)
Write-Host "MSIX build completed under $outDir"
if ($msix.Count -gt 0) {
    Write-Host ("Found MSIX: " + (($msix | ForEach-Object FullName) -join ', '))
}
elseif ($manifest.Count -gt 0) {
    Write-Host ("Found AppxManifest layout: " + (($manifest | ForEach-Object FullName) -join ', '))
}
else {
    throw "No .msix or AppxManifest.xml under $outDir — single-project MSIX packaging did not emit a package."
}
