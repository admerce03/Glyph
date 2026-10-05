#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Build Glyph as a single-project MSIX package (Milestone 9 / ADR-006, ADR-012).

.DESCRIPTION
  Keeps the default solution build unpackaged (WindowsPackageType=None).
  Passes -p:GlyphPackage=MSIX + GenerateAppxPackageOnBuild so WinUI single-project
  MSIX tooling emits a .msix (see Microsoft Learn: single-project MSIX).

  With -TestSign (CI default), creates an ephemeral self-signed code-signing cert
  (CN=Glyph, matching Package.appxmanifest Publisher), signs the package, and
  exports a .cer beside the .msix for Trusted People install before sideload.
  Production / Store signing remains a separate distribution step.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64',

    # Relative to repo root, or an absolute path. Default: artifacts/msix
    [string]$Output = 'artifacts/msix',

    # Ephemeral self-signed cert for Developer Mode sideload (CI).
    [switch]$TestSign
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $repoRoot 'src' 'Glyph.App' 'Glyph.App.csproj'
$platform = if ($Runtime -eq 'win-arm64') { 'ARM64' } else { 'x64' }

# Must match Identity/@Publisher in Package.appxmanifest
$publisher = 'CN=Glyph'

if ([System.IO.Path]::IsPathRooted($Output)) {
    $outDir = $Output
}
else {
    $outDir = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Output))
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$cert = $null
$signingArgs = @(
    '-p:AppxPackageSigningEnabled=false'
)

try {
    if ($TestSign) {
        if (-not $IsWindows -and $env:OS -ne 'Windows_NT') {
            throw '-TestSign requires Windows (New-SelfSignedCertificate).'
        }

        Write-Host "Creating ephemeral test-signing certificate ($publisher)…"
        $cert = New-SelfSignedCertificate `
            -Type Custom `
            -Subject $publisher `
            -KeyUsage DigitalSignature `
            -FriendlyName 'Glyph CI Ephemeral Test Sign' `
            -CertStoreLocation 'Cert:\CurrentUser\My' `
            -TextExtension @(
                '2.5.29.37={text}1.3.6.1.5.5.7.3.3',
                '2.5.29.19={text}'
            )

        $cerPath = Join-Path $outDir 'Glyph.CI.TestSign.cer'
        Export-Certificate -Cert $cert -FilePath $cerPath | Out-Null
        Write-Host "Exported trust cert: $cerPath (install to Trusted People before sideload)"

        $signingArgs = @(
            '-p:AppxPackageSigningEnabled=true',
            "-p:PackageCertificateThumbprint=$($cert.Thumbprint)"
        )
        Write-Host "Signing with thumbprint $($cert.Thumbprint)"
    }

    Write-Host "Building Glyph MSIX ($Configuration / $Runtime / $platform) → $outDir"

    # Single-project MSIX uses GenerateAppxPackageOnBuild (not plain dotnet publish -o).
    # https://learn.microsoft.com/windows/apps/windows-app-sdk/single-project-msix
    $buildArgs = @(
        $project,
        '-c', $Configuration,
        '-r', $Runtime,
        "-p:Platform=$platform",
        '-p:GlyphPackage=MSIX',
        '-p:WindowsAppSDKSelfContained=true',
        '-p:GenerateAppxPackageOnBuild=true',
        '-p:AppxBundle=Never',
        "-p:AppxPackageDir=$outDir\\"
    ) + $signingArgs

    & dotnet build @buildArgs

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build (MSIX) failed with exit code $LASTEXITCODE"
    }

    # Ship curated notices + vendor NOTICE/LICENSE files when NuGet cache has them.
    $noticesSrc = Join-Path $repoRoot 'THIRD_PARTY_NOTICES.md'
    if (Test-Path -LiteralPath $noticesSrc) {
        Copy-Item -LiteralPath $noticesSrc -Destination (Join-Path $outDir 'THIRD_PARTY_NOTICES.md') -Force
        Write-Host "Copied THIRD_PARTY_NOTICES.md to $outDir"
    }
    $vendorDir = Join-Path $outDir 'third-party'
    New-Item -ItemType Directory -Force -Path $vendorDir | Out-Null
    $nuget = Join-Path $env:USERPROFILE '.nuget' 'packages'
    $magickNotice = Get-ChildItem -Path (Join-Path $nuget 'magick.net-q16-anycpu') -Filter Notice.txt -Recurse -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending |
        Select-Object -First 1
    if ($null -ne $magickNotice) {
        Copy-Item -LiteralPath $magickNotice.FullName -Destination (Join-Path $vendorDir 'Magick.NET-Notice.txt') -Force
        Write-Host "Copied Magick.NET Notice.txt"
    }
    $pdfiumLicense = Get-ChildItem -Path (Join-Path $nuget 'pdfiumcore') -Filter LICENSE -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match 'win-x64|win-arm64|linux-x64' } |
        Sort-Object FullName -Descending |
        Select-Object -First 1
    if ($null -ne $pdfiumLicense) {
        Copy-Item -LiteralPath $pdfiumLicense.FullName -Destination (Join-Path $vendorDir 'PDFium-LICENSE.txt') -Force
        Write-Host "Copied PDFium LICENSE"
    }

    $msix = @(Get-ChildItem -Path $outDir -Filter *.msix -Recurse -ErrorAction SilentlyContinue)
    $manifest = @(Get-ChildItem -Path $outDir -Filter AppxManifest.xml -Recurse -ErrorAction SilentlyContinue)
    Write-Host "MSIX build completed under $outDir"
    if ($msix.Count -gt 0) {
        Write-Host ("Found MSIX: " + (($msix | ForEach-Object FullName) -join ', '))
        if ($TestSign) {
            foreach ($pkg in $msix) {
                $sig = Get-AuthenticodeSignature -FilePath $pkg.FullName
                if ($null -eq $sig.SignerCertificate) {
                    throw "Test-signed MSIX has no signer certificate: $($pkg.FullName)"
                }
                if ($sig.SignerCertificate.Thumbprint -ne $cert.Thumbprint) {
                    throw "MSIX signer thumbprint mismatch for $($pkg.FullName)"
                }
                Write-Host ("Signature OK: $($pkg.Name) status=$($sig.Status) thumbprint=$($sig.SignerCertificate.Thumbprint)")
            }

            $cerOut = Join-Path $outDir 'Glyph.CI.TestSign.cer'
            if (-not (Test-Path -LiteralPath $cerOut)) {
                throw "TestSign completed but trust cert missing: $cerOut"
            }
            Write-Host "Trust cert present: $cerOut"
        }
    }
    elseif ($manifest.Count -gt 0) {
        Write-Host ("Found AppxManifest layout: " + (($manifest | ForEach-Object FullName) -join ', '))
        if ($TestSign) {
            throw 'TestSign requested but only loose AppxManifest layout was produced (no .msix).'
        }
    }
    else {
        throw "No .msix or AppxManifest.xml under $outDir — single-project MSIX packaging did not emit a package."
    }
}
finally {
    if ($null -ne $cert) {
        $storePath = "Cert:\CurrentUser\My\$($cert.Thumbprint)"
        if (Test-Path $storePath) {
            Remove-Item $storePath -Force -ErrorAction SilentlyContinue
            Write-Host "Removed ephemeral cert from CurrentUser\My"
        }
    }
}
