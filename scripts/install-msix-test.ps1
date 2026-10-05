#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Trust the CI ephemeral test cert and sideload the Glyph MSIX (Developer Mode).

.DESCRIPTION
  Companion to scripts/publish-msix.ps1 -TestSign (Milestone 9 / ADR-012).
  Imports Glyph.CI.TestSign.cer into CurrentUser\TrustedPeople, then
  Add-AppxPackage the Glyph.App_*.msix. Does not claim file-association
  verification complete — that remains a manual ADR-012 check after install.

.NOTES
  Requires Windows + Developer Mode (or an equivalent sideloading policy).
  Production / Store signing is out of scope.
#>
[CmdletBinding()]
param(
    # Directory containing Glyph.App_*.msix and Glyph.CI.TestSign.cer
    # Relative to repo root, or absolute. Default: artifacts/msix
    [string]$PackageDir = 'artifacts/msix',

    # Skip Add-AppxPackage (cert import only)
    [switch]$CertOnly,

    # Remove an existing Glyph.Desktop package before install
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

if (-not $IsWindows -and $env:OS -ne 'Windows_NT') {
    throw 'install-msix-test.ps1 requires Windows (cert store + Add-AppxPackage).'
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if ([System.IO.Path]::IsPathRooted($PackageDir)) {
    $dir = $PackageDir
}
else {
    $dir = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $PackageDir))
}

if (-not (Test-Path -LiteralPath $dir)) {
    throw "Package directory not found: $dir"
}

$cer = @(Get-ChildItem -Path $dir -Filter 'Glyph.CI.TestSign.cer' -Recurse -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending)
$msix = @(Get-ChildItem -Path $dir -Filter 'Glyph.App_*.msix' -Recurse -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending)

if ($cer.Count -eq 0) {
    throw "No Glyph.CI.TestSign.cer under $dir — run publish-msix.ps1 -TestSign first (or download the CI artifact)."
}

$cerPath = $cer[0].FullName
Write-Host "Importing test cert → CurrentUser\TrustedPeople: $cerPath"
Import-Certificate -FilePath $cerPath -CertStoreLocation 'Cert:\CurrentUser\TrustedPeople' | Out-Null

if ($CertOnly) {
    Write-Host 'Cert imported (-CertOnly). Skipping Add-AppxPackage.'
    return
}

if ($msix.Count -eq 0) {
    throw "No Glyph.App_*.msix under $dir — run publish-msix.ps1 -TestSign first (or download the CI artifact)."
}

$msixPath = $msix[0].FullName
Write-Host "Sideloading: $msixPath"

$existing = Get-AppxPackage -Name 'Glyph.Desktop' -ErrorAction SilentlyContinue
if ($null -ne $existing) {
    if ($Force) {
        Write-Host "Removing existing package $($existing.PackageFullName)"
        Remove-AppxPackage -Package $existing.PackageFullName
    }
    else {
        throw "Glyph.Desktop already installed ($($existing.PackageFullName)). Re-run with -Force to replace."
    }
}

Add-AppxPackage -Path $msixPath
$installed = Get-AppxPackage -Name 'Glyph.Desktop' -ErrorAction SilentlyContinue
if ($null -eq $installed) {
    throw 'Add-AppxPackage completed but Get-AppxPackage Glyph.Desktop returned nothing.'
}

Write-Host "Installed: $($installed.PackageFullName)"
Write-Host 'Next: open Glyph, confirm .pdf/.png Open With / default association (ADR-012 manual verify).'
