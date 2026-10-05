#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Trust the CI ephemeral test cert and sideload the Glyph MSIX (Developer Mode).

.DESCRIPTION
  Companion to scripts/publish-msix.ps1 -TestSign (Milestone 9 / ADR-012).
  Imports Glyph.CI.TestSign.cer into CurrentUser\TrustedPeople, then
  Add-AppxPackage the Glyph.App_*.msix. After install, probes the installed
  package manifest for declared file-type associations (same set as
  PackageFileAssociationDeclaration). Optionally reports HKCU UserChoice
  ProgIds (-ProbeUserDefaults) and opens Default apps Settings
  (-OpenDefaultApps). Explorer Open With / default-app assignment remains a
  manual ADR-012 check until those UserChoice entries point at Glyph.

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
    [switch]$Force,

    # Only run association probe against an already-installed Glyph.Desktop
    [switch]$VerifyOnly,

    # Print HKCU FileExts UserChoice ProgId for each expected extension
    [switch]$ProbeUserDefaults,

    # Launch Settings → Default apps (ms-settings:defaultapps)
    [switch]$OpenDefaultApps
)

$ErrorActionPreference = 'Stop'

# Keep in sync with PackageFileAssociationDeclaration.ExpectedExtensions
$expectedExtensions = @(
    '.pdf', '.png', '.jpg', '.jpeg', '.gif', '.bmp', '.tif', '.tiff', '.webp'
)

function Get-InstalledGlyphPackage {
    Get-AppxPackage -Name 'Glyph.Desktop' -ErrorAction SilentlyContinue
}

function Test-GlyphPackageAssociations {
    param([Parameter(Mandatory)] $Package)

    Write-Host "Probing installed associations for $($Package.PackageFullName)…"
    $manifest = Get-AppxPackageManifest -Package $Package.PackageFullName
    if ($null -eq $manifest) {
        throw "Get-AppxPackageManifest returned nothing for $($Package.PackageFullName)"
    }

    $ns = @{
        uap = 'http://schemas.microsoft.com/appx/manifest/uap/windows10'
    }
    $declared = @(
        Select-Xml -Xml $manifest -XPath '//uap:FileType' -Namespace $ns |
            ForEach-Object { ($_.Node.InnerText ?? '').Trim().ToLowerInvariant() } |
            Where-Object { $_ -like '.*' } |
            Select-Object -Unique
    )

    if ($declared.Count -eq 0) {
        throw 'Installed package manifest declares no uap:FileType associations.'
    }

    $missing = @($expectedExtensions | Where-Object { $declared -notcontains $_ })
    if ($missing.Count -gt 0) {
        throw ("Installed package missing associations: " + ($missing -join ', ') +
            " (declared: " + ($declared -join ', ') + ')')
    }

    Write-Host ("Association probe OK: " + ($expectedExtensions -join ', '))
    Write-Host 'Still manual: Explorer Open With / default app for .pdf/.png (ADR-012).'
    Write-Host 'Proof folder: docs/proof/ (see docs/INTERACTIVE_VERIFY.md).'
}

function Show-GlyphUserDefaultProbe {
    Write-Host 'HKCU UserChoice ProgIds (informational — set Defaults in Settings if empty):'
    foreach ($ext in $expectedExtensions) {
        $path = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\$ext\UserChoice"
        if (-not (Test-Path -LiteralPath $path)) {
            Write-Host ("  {0,-6}  (no UserChoice)" -f $ext)
            continue
        }

        $progId = (Get-ItemProperty -LiteralPath $path -ErrorAction SilentlyContinue).ProgId
        if ([string]::IsNullOrWhiteSpace($progId)) {
            Write-Host ("  {0,-6}  (empty ProgId)" -f $ext)
        }
        else {
            Write-Host ("  {0,-6}  {1}" -f $ext, $progId)
        }
    }
}

function Open-DefaultAppsSettings {
    if ($env:GITHUB_ACTIONS -eq 'true' -or $env:CI -eq 'true') {
        Write-Host 'Skipping ms-settings:defaultapps on CI (no interactive Settings UI).'
        return
    }

    Write-Host 'Opening ms-settings:defaultapps …'
    Start-Process 'ms-settings:defaultapps'
}

if (-not $IsWindows -and $env:OS -ne 'Windows_NT') {
    throw 'install-msix-test.ps1 requires Windows (cert store + Add-AppxPackage).'
}

if ($VerifyOnly) {
    $installed = Get-InstalledGlyphPackage
    if ($null -eq $installed) {
        throw 'Glyph.Desktop is not installed. Run without -VerifyOnly first.'
    }
    Test-GlyphPackageAssociations -Package $installed
    if ($ProbeUserDefaults) {
        Show-GlyphUserDefaultProbe
    }
    if ($OpenDefaultApps) {
        Open-DefaultAppsSettings
    }
    return
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if ([System.IO.Path]::IsPathRooted($PackageDir)) {
    $dir = $PackageDir
}
else {
    $dir = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $PackageDir))
}

# Allow defaults probe without a package directory present.
if (($ProbeUserDefaults -or $OpenDefaultApps) -and -not (Test-Path -LiteralPath $dir)) {
    if ($ProbeUserDefaults) {
        Show-GlyphUserDefaultProbe
    }
    if ($OpenDefaultApps) {
        Open-DefaultAppsSettings
    }
    if (-not $CertOnly) {
        return
    }
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
Write-Host "Importing test cert for Appx trust: $cerPath"

# CurrentUser TrustedPeople covers interactive Developer Mode installs.
Import-Certificate -FilePath $cerPath -CertStoreLocation 'Cert:\CurrentUser\TrustedPeople' | Out-Null

# Add-AppxPackage on CI / elevated hosts validates the signature against machine trust.
# Self-signed CI certs need LocalMachine TrustedPeople (and Root as the chain terminator).
foreach ($store in @(
        'Cert:\LocalMachine\TrustedPeople',
        'Cert:\LocalMachine\Root')) {
    try {
        Import-Certificate -FilePath $cerPath -CertStoreLocation $store | Out-Null
        Write-Host "  imported → $store"
    }
    catch {
        Write-Warning "Could not import into $store (need elevation?): $($_.Exception.Message)"
    }
}

if ($CertOnly) {
    Write-Host 'Cert imported (-CertOnly). Skipping Add-AppxPackage.'
    if ($ProbeUserDefaults) {
        Show-GlyphUserDefaultProbe
    }
    if ($OpenDefaultApps) {
        Open-DefaultAppsSettings
    }
    return
}

if ($msix.Count -eq 0) {
    throw "No Glyph.App_*.msix under $dir — run publish-msix.ps1 -TestSign first (or download the CI artifact)."
}

$msixPath = $msix[0].FullName
Write-Host "Sideloading: $msixPath"

$existing = Get-InstalledGlyphPackage
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
$installed = Get-InstalledGlyphPackage
if ($null -eq $installed) {
    throw 'Add-AppxPackage completed but Get-AppxPackage Glyph.Desktop returned nothing.'
}

Write-Host "Installed: $($installed.PackageFullName)"
Test-GlyphPackageAssociations -Package $installed
if ($ProbeUserDefaults) {
    Show-GlyphUserDefaultProbe
}
if ($OpenDefaultApps) {
    Open-DefaultAppsSettings
}