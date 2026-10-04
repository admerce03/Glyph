#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

CORE_ONLY=0
CONFIGURATION=Release
while [[ $# -gt 0 ]]; do
  case "$1" in
    --core-only) CORE_ONLY=1; shift ;;
    --configuration) CONFIGURATION="$2"; shift 2 ;;
    *) echo "Unknown arg: $1" >&2; exit 1 ;;
  esac
done

if [[ "$CORE_ONLY" -eq 1 ]]; then
  echo "Running core tests ($CONFIGURATION)..."
  dotnet test ./tests/Glyph.Core.Tests/Glyph.Core.Tests.csproj -c "$CONFIGURATION"
  dotnet test ./tests/Glyph.Infrastructure.Tests/Glyph.Infrastructure.Tests.csproj -c "$CONFIGURATION"
  dotnet test ./tests/Glyph.Pdf.Tests/Glyph.Pdf.Tests.csproj -c "$CONFIGURATION"
else
  echo "Running all tests in Glyph.sln ($CONFIGURATION)..."
  dotnet test ./Glyph.sln -c "$CONFIGURATION"
fi

echo "Tests succeeded."
