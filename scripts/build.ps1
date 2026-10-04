#!/usr/bin/env pwsh
param(
    [switch]$CoreOnly,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if ($CoreOnly) {
    Write-Host "Building Glyph core projects ($Configuration)..."
    $projects = @(
        "./src/Glyph.Core/Glyph.Core.csproj",
        "./src/Glyph.Infrastructure/Glyph.Infrastructure.csproj",
        "./src/Glyph.Pdf/Glyph.Pdf.csproj",
        "./src/Glyph.Imaging/Glyph.Imaging.csproj",
        "./src/Glyph.Ocr/Glyph.Ocr.csproj",
        "./tests/Glyph.Core.Tests/Glyph.Core.Tests.csproj",
        "./tests/Glyph.Infrastructure.Tests/Glyph.Infrastructure.Tests.csproj",
        "./tests/Glyph.Pdf.Tests/Glyph.Pdf.Tests.csproj"
    )
    foreach ($project in $projects) {
        dotnet build $project -c $Configuration
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
}
else {
    Write-Host "Building Glyph.sln ($Configuration)..."
    dotnet build ./Glyph.sln -c $Configuration
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host "Build succeeded."
