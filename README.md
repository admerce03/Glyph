# Glyph

Glyph is a native Windows desktop application inspired by the workflow of macOS Preview: fast document opening, thumbnail-first PDF page manipulation, and lightweight image viewing/editing — without Electron or cloud-required processing.

Authoritative product scope: [`docs/FEATURES.md`](docs/FEATURES.md).

## Current status

**Milestone 0 — Architecture and engineering foundation** is in progress.

- Architecture, roadmap, decisions, and agent instructions are documented under `docs/` and `AGENTS.md`.
- Solution structure, analyzers, tests, scripts, and Windows CI are established.
- The WinUI application shell opens documents into tabs (PDF/image viewers arrive in later milestones).

## Technology

- C# / .NET 10
- WinUI (Windows App SDK)
- Unpackaged desktop app (`WindowsPackageType=None`)
- PDFium behind interfaces (rendering arrives in Milestone 2)
- Offline-first OCR/image/PDF processing

See [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) and [`docs/DECISIONS.md`](docs/DECISIONS.md).

## Repository layout

```text
src/Glyph.App            WinUI shell
src/Glyph.Core           Document sessions, commands, undo
src/Glyph.Infrastructure Settings/recent files/paths
src/Glyph.Pdf            PDF abstractions (+ later PDFium adapter)
src/Glyph.Imaging        Image abstractions (+ later codecs)
src/Glyph.Ocr            OCR abstractions (+ later engines)
tests/                   xUnit behavior tests
docs/                    Product + engineering docs
scripts/                 Build/test/format helpers
```

## Prerequisites (Windows)

- Windows 10 1809+ or Windows 11
- .NET 10 SDK (see `global.json`)
- Visual Studio 2022/2026 with the **Windows application development** workload, or equivalent WinUI tooling

## Build and test

Full solution (Windows):

```powershell
./scripts/build.ps1
./scripts/test.ps1
./scripts/format.ps1
```

Core libraries only (works on non-Windows agents):

```bash
./scripts/build.sh --core-only
./scripts/test.sh --core-only
```

Or directly:

```powershell
dotnet build ./Glyph.sln -c Release
dotnet test ./Glyph.sln -c Release
```

## Run

On Windows, after building:

```powershell
dotnet run --project ./src/Glyph.App/Glyph.App.csproj -c Debug -r win-x64
```

## Contributing / agents

Read [`AGENTS.md`](AGENTS.md) before making changes. Track requirement progress in [`docs/FEATURE_MATRIX.md`](docs/FEATURE_MATRIX.md).
