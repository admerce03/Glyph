# Glyph

Glyph is a native Windows desktop application inspired by the workflow of macOS Preview: fast document opening, thumbnail-first PDF page manipulation, and lightweight image viewing/editing — without Electron or cloud-required processing.

Authoritative product scope: [`docs/FEATURES.md`](docs/FEATURES.md).

## Current status

Milestones **M0–M8** are matrix-**Tested** on `main` (password-write Blocked on ADR-015; HDR/HEIF/ML and packaging associations Deferred). **M9** is In Progress (test-signed `.msix` builds on Windows CI; sideload verify + interactive demos pending).

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
# Unsigned package (layout / tooling smoke)
./scripts/publish-msix.ps1 -Configuration Release -Runtime win-x64

# CI / Developer Mode sideload: ephemeral self-signed cert + .cer
./scripts/publish-msix.ps1 -Configuration Release -Runtime win-x64 -TestSign
```

Output lands under `artifacts/msix/` (`Glyph.App_*.msix`; with `-TestSign`, also `Glyph.CI.TestSign.cer`). Install the `.cer` into **Trusted People**, enable Developer Mode, then `Add-AppxPackage`. Store signing and association verification remain open (ADR-012).

## Contributing / agents

Read [`AGENTS.md`](AGENTS.md) before making changes. Track requirement progress in [`docs/FEATURE_MATRIX.md`](docs/FEATURE_MATRIX.md).
