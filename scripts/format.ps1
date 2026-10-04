#!/usr/bin/env pwsh
param(
    [switch]$Verify
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if ($Verify) {
    dotnet format ./Glyph.sln --verify-no-changes --severity warn
}
else {
    dotnet format ./Glyph.sln --severity warn
}

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "Format step completed."
