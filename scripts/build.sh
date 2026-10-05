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
  echo "Building Glyph core projects ($CONFIGURATION)..."
  for proj in \
    src/Glyph.Core/Glyph.Core.csproj \
    src/Glyph.Infrastructure/Glyph.Infrastructure.csproj \
    src/Glyph.Pdf/Glyph.Pdf.csproj \
    src/Glyph.Imaging/Glyph.Imaging.csproj \
    src/Glyph.Ocr/Glyph.Ocr.csproj \
    tests/Glyph.Core.Tests/Glyph.Core.Tests.csproj \
    tests/Glyph.Infrastructure.Tests/Glyph.Infrastructure.Tests.csproj \
    tests/Glyph.Pdf.Tests/Glyph.Pdf.Tests.csproj \
    tests/Glyph.Imaging.Tests/Glyph.Imaging.Tests.csproj \
    tests/Glyph.Ocr.Tests/Glyph.Ocr.Tests.csproj
  do
    dotnet build "$proj" -c "$CONFIGURATION"
  done
else
  echo "Building Glyph.sln ($CONFIGURATION)..."
  dotnet build ./Glyph.sln -c "$CONFIGURATION"
fi

echo "Build succeeded."
