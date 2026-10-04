#!/usr/bin/env pwsh
param(
    [switch]$CoreOnly,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if ($CoreOnly) {
    Write-Host "Running core tests ($Configuration)..."
    $projects = @(
        "./tests/Glyph.Core.Tests/Glyph.Core.Tests.csproj",
        "./tests/Glyph.Infrastructure.Tests/Glyph.Infrastructure.Tests.csproj",
        "./tests/Glyph.Pdf.Tests/Glyph.Pdf.Tests.csproj"
    )
    foreach ($project in $projects) {
        dotnet test $project -c $Configuration
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
}
else {
    Write-Host "Running all tests in Glyph.sln ($Configuration)..."
    dotnet test ./Glyph.sln -c $Configuration
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host "Tests succeeded."
