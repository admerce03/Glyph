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

    [string]$Output = (Join-Path $PSScriptRoot '..' 'artifacts' 'msix')
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$project = Join-Path $repoRoot 'src' 'Glyph.App' 'Glyph.App.csproj'
$outDir = Join-Path $repoRoot ($Output.TrimStart('.', '\', '/'))

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

Write-Host "MSIX publish completed. Look under $outDir for .msix / layout."
