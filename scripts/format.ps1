#!/usr/bin/env pwsh
param(
    [switch]$Verify
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if ($Verify) {
    dotnet format ./Glyph.sln --verify-no-changes --severity-level warn
}
else {
    dotnet format ./Glyph.sln
}

Write-Host "Format step completed."
