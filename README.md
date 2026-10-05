# Glyph

Glyph is a native Windows desktop application inspired by the workflow of macOS Preview: fast document opening, thumbnail-first PDF page manipulation, and lightweight image viewing/editing — without Electron or cloud-required processing.

Authoritative product scope: [`docs/FEATURES.md`](docs/FEATURES.md).

## Current status

Milestones **M0–M8** are matrix-**Tested** on `main` (password-write Blocked on ADR-015; HDR/HEIF/ML and packaging associations Deferred). **M9** is In Progress (MSIX scaffold; signing/sideload verify + interactive demos pending).

See [`docs/ROADMAP.md`](docs/ROADMAP.md) and [`docs/FEATURE_MATRIX.md`](docs/FEATURE_MATRIX.md).

## Technology

- C# / .NET 10
- WinUI (Windows App SDK)
- Unpackaged desktop app by default (`WindowsPackageType=None`); optional MSIX via `scripts/publish-msix.ps1`
- PDFium behind interfaces
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

## Optional MSIX publish (Milestone 9)

Default builds stay unpackaged. To produce a self-contained MSIX layout on Windows:

```powershell
./scripts/publish-msix.ps1 -Configuration Release -Runtime win-x64
```

Output lands under `artifacts/msix/`. Signing and Store/sideload verification are still open (ADR-012 associations remain Deferred until verified).

## Contributing / agents

Read [`AGENTS.md`](AGENTS.md) before making changes. Track requirement progress in [`docs/FEATURE_MATRIX.md`](docs/FEATURE_MATRIX.md).
