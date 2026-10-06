#!/usr/bin/env pwsh
<#
.SYNOPSIS
  One-command Windows interactive verify orchestrator (M9 / INTERACTIVE_VERIFY.md).

.DESCRIPTION
  Prepares a Developer Mode Windows host for the remaining M9 interactive gates:
  downloads (or builds) the test-signed MSIX, sideloads Glyph, probes associations /
  UserChoice, opens Default apps Settings, and prints the capture checklist for
  docs/proof/ artifacts. Does not flip matrix rows — that waits on attached proof.

  On Linux/macOS, -DownloadArtifact prefetches and validates the CI package layout
  (no sideload). Combine with -StatusOnly to report docs/proof capture gaps.

.NOTES
  Sideload / Explorer defaults require Windows. Prefer a green main CI artifact
  over a local publish.
#>
[CmdletBinding()]
param(
    # Directory with Glyph.App_*.msix + Glyph.CI.TestSign.cer
    [string]$PackageDir = 'artifacts/msix',

    # Repo (owner/name) for gh run download when -DownloadArtifact is set
    [string]$Repo = 'admerce03/Glyph',

    # Download latest successful main glyph-msix-layout artifact via gh (any OS)
    [switch]$DownloadArtifact,

    # Local publish when no package is present (slow; Windows)
    [switch]$PublishIfMissing,

    # Skip Add-AppxPackage; only probe + print checklist
    [switch]$VerifyOnly,

    # Skip opening ms-settings:defaultapps
    [switch]$SkipOpenDefaultApps,

    # Only report which docs/proof files exist (any OS; may follow -DownloadArtifact)
    [switch]$StatusOnly,

    # After sideload, open sample.pdf / sample.png via Explorer (current defaults)
    [switch]$OpenSamples
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $repoRoot

$proofExpected = @(
    'docs/proof/m1-shell-tabs.png',
    'docs/proof/m2-pdf-viewer.png',
    'docs/proof/m3-page-dnd.mp4'
)

function Test-IsWindowsHost {
    return ($IsWindows -or $env:OS -eq 'Windows_NT')
}

function Resolve-PackageDir {
    param([string]$PackageDir)
    if ([System.IO.Path]::IsPathRooted($PackageDir)) {
        return $PackageDir
    }

    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $PackageDir))
}

function Test-HasMsixPackage {
    param([string]$Dir)
    if (-not (Test-Path -LiteralPath $Dir)) {
        return $false
    }

    return @(Get-ChildItem -Path $Dir -Filter 'Glyph.App_*.msix' -Recurse -ErrorAction SilentlyContinue).Count -gt 0
}

function Test-HasTestSignCert {
    param([string]$Dir)
    $cer = Join-Path $Dir 'Glyph.CI.TestSign.cer'
    return (Test-Path -LiteralPath $cer)
}

function Show-ProofStatus {
    Write-Host ''
    Write-Host '=== docs/proof status ==='
    $missing = 0
    foreach ($rel in $proofExpected) {
        $full = Join-Path $repoRoot $rel
        if (Test-Path -LiteralPath $full) {
            Write-Host ("  OK   {0}" -f $rel)
        }
        else {
            Write-Host ("  MISS {0}" -f $rel)
            $missing++
        }
    }

    Write-Host ''
    Write-Host 'Explorer default-app screenshots (F01-06/07) are not fixed filenames —'
    Write-Host 'attach under docs/proof/ or the closing PR when flipping those rows.'
    return $missing
}

function Show-CaptureChecklist {
    Write-Host ''
    Write-Host '=== Remaining interactive captures ==='
    Write-Host '1. Settings → Apps → Default apps: set Glyph for .pdf and image types; screenshot.'
    Write-Host '2. Double-click samples (after setting defaults):'
    Write-Host '     docs\proof\samples\sample.pdf'
    Write-Host '     docs\proof\samples\sample.png'
    Write-Host '3. Shell with ≥2 tabs → save docs/proof/m1-shell-tabs.png'
    Write-Host '4. Multi-page PDF viewer (sample.pdf has 2 pages) → save docs/proof/m2-pdf-viewer.png'
    Write-Host '5. Cross-doc page DnD (open sample.pdf twice / duplicate) → docs/proof/m3-page-dnd.mp4 (~30s)'
    Write-Host ''
    Write-Host 'Full checklist: docs/INTERACTIVE_VERIFY.md'
    Write-Host 'Still escalate separately: Store/production signing (ADR-015 Accept A landed).'
}

function Open-SampleFiles {
    param([switch]$Launch)

    $pdf = Join-Path $repoRoot 'docs\proof\samples\sample.pdf'
    $png = Join-Path $repoRoot 'docs\proof\samples\sample.png'
    if (-not (Test-Path -LiteralPath $pdf) -or -not (Test-Path -LiteralPath $png)) {
        Write-Warning 'docs/proof/samples fixtures missing — skip sample open.'
        return
    }

    Write-Host ''
    Write-Host 'Sample fixtures:'
    Write-Host "  $pdf"
    Write-Host "  $png"
    if (-not $Launch) {
        return
    }

    if ($env:GITHUB_ACTIONS -eq 'true' -or $env:CI -eq 'true') {
        Write-Host 'Skipping sample Explorer launch on CI.'
        return
    }

    Write-Host 'Opening sample.pdf / sample.png via Explorer (uses current defaults)…'
    Start-Process explorer.exe -ArgumentList $pdf
    Start-Process explorer.exe -ArgumentList $png
}

function Invoke-DownloadMsixArtifact {
    param(
        [Parameter(Mandatory)][string]$Dir,
        [Parameter(Mandatory)][string]$Repo
    )

    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw '-DownloadArtifact requires the GitHub CLI (gh) on PATH.'
    }

    New-Item -ItemType Directory -Force -Path $Dir | Out-Null

    Write-Host "Resolving latest successful main CI run ($Repo)…"
    $runId = gh run list --repo $Repo --branch main --status success --limit 1 --json databaseId --jq '.[0].databaseId'
    if ([string]::IsNullOrWhiteSpace($runId) -or $runId -eq 'null') {
        throw "No successful main CI run found for $Repo (gh run list)."
    }

    Write-Host "Downloading glyph-msix-layout from run $runId…"
    # gh run download has no --branch flag; pin the run id from main above.
    gh run download $runId --repo $Repo --name glyph-msix-layout --dir $Dir

    if (-not (Test-HasMsixPackage -Dir $Dir)) {
        throw "Download finished but no Glyph.App_*.msix under $Dir."
    }

    if (-not (Test-HasTestSignCert -Dir $Dir)) {
        throw "Download finished but Glyph.CI.TestSign.cer missing under $Dir."
    }

    $msix = @(Get-ChildItem -Path $Dir -Filter 'Glyph.App_*.msix' -Recurse)[0]
    Write-Host ("Package OK: {0}" -f $msix.FullName)
    Write-Host ("Cert OK:    {0}" -f (Join-Path $Dir 'Glyph.CI.TestSign.cer'))
}

$dir = Resolve-PackageDir -PackageDir $PackageDir
$hasMsix = Test-HasMsixPackage -Dir $dir

# Prefetch works on any OS (Linux agents / operator prep before Windows sideload).
if (-not $hasMsix -and $DownloadArtifact) {
    Invoke-DownloadMsixArtifact -Dir $dir -Repo $Repo
    $hasMsix = Test-HasMsixPackage -Dir $dir
}

# Filesystem-only proof status works on any OS (Linux CI / agents included).
if ($StatusOnly) {
    $missing = Show-ProofStatus
    Open-SampleFiles
    Show-CaptureChecklist
    if ($hasMsix) {
        Write-Host ''
        Write-Host ("MSIX package present under {0}" -f $dir)
    }
    elseif ($DownloadArtifact) {
        Write-Host ''
        Write-Host 'No MSIX package after -DownloadArtifact (unexpected).'
    }

    if ($missing -gt 0) {
        Write-Host ("Proof incomplete: {0} expected capture(s) missing." -f $missing)
        exit 2
    }

    Write-Host 'All expected docs/proof captures present.'
    exit 0
}

if (-not (Test-IsWindowsHost)) {
    if ($hasMsix) {
        Write-Host ("Non-Windows host: MSIX layout ready under {0}" -f $dir)
        Write-Host 'Re-run on a Developer Mode Windows host for sideload / Explorer defaults:'
        Write-Host '  ./scripts/interactive-verify.ps1 -PackageDir artifacts/msix'
        Write-Host '  ./scripts/interactive-verify.ps1 -DownloadArtifact'
        Show-CaptureChecklist
        exit 0
    }

    throw 'interactive-verify.ps1 requires Windows for sideload / Explorer defaults (use -StatusOnly or -DownloadArtifact on Linux).'
}

if (-not $hasMsix -and $PublishIfMissing) {
    Write-Host 'No MSIX found — publishing test-signed win-x64 package locally…'
    & (Join-Path $PSScriptRoot 'publish-msix.ps1') -Configuration Release -Runtime win-x64 -TestSign -Output $PackageDir
    $hasMsix = Test-HasMsixPackage -Dir $dir
}

if (-not $VerifyOnly -and -not $hasMsix) {
    throw @"
No Glyph.App_*.msix under $dir.
Re-run with -DownloadArtifact (needs gh) or -PublishIfMissing, or place the CI artifact first.
"@
}

$install = Join-Path $PSScriptRoot 'install-msix-test.ps1'
$installArgs = @{
    PackageDir        = $PackageDir
    ProbeUserDefaults = $true
}
if ($VerifyOnly) {
    $installArgs.VerifyOnly = $true
}
else {
    $installArgs.Force = $true
}
if (-not $SkipOpenDefaultApps) {
    $installArgs.OpenDefaultApps = $true
}

Write-Host "Running install-msix-test.ps1 $($installArgs.Keys -join ', ')…"
& $install @installArgs

Show-ProofStatus | Out-Null
Open-SampleFiles -Launch:$OpenSamples
Show-CaptureChecklist
Write-Host 'Sideload/probe step done. Complete captures above, then update FEATURE_MATRIX / ROADMAP.'
if (-not $OpenSamples) {
    Write-Host 'Tip: re-run with -OpenSamples after setting Glyph as default to launch the fixtures.'
}
