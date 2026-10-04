# Glyph agent instructions

Persistent engineering rules for every agent working on this repository.

## Product authority

- [`docs/FEATURES.md`](docs/FEATURES.md) is the authoritative product-scope specification.
- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md), [`docs/ROADMAP.md`](docs/ROADMAP.md), and [`docs/DECISIONS.md`](docs/DECISIONS.md) govern implementation.
- Track requirement status in [`docs/FEATURE_MATRIX.md`](docs/FEATURE_MATRIX.md). Do not silently drop requirements.

## Stack constraints

- Native Windows app: **C#**, **.NET 10**, **WinUI**, current stable **Windows App SDK**.
- **Do not** introduce Electron, WebView-as-shell, or other browser-wrapper architectures.
- Prefer unpackaged WinUI (`WindowsPackageType=None`) unless an ADR changes distribution strategy.
- Target **x64** and **ARM64** only.

## Licensing

- Prefer permissively licensed dependencies (MIT/Apache-2.0/BSD).
- **Do not** add AGPL, GPL, commercial-license-required, or other restrictive dependencies without documenting implications in `docs/DECISIONS.md` and obtaining approval.
- Default PDF engine: **PDFium** behind abstractions. Do not add MuPDF/iText/commercial PDF SDKs casually.
- Default imaging: **WIC/WinRT** + optional **Magick.NET**. Do not add ImageSharp without approval.
- Ship third-party notices for native redistributables (PDFium, ImageMagick, etc.).

## Architecture rules

1. Put expensive/replaceable capabilities behind interfaces: PDF, OCR, imaging, codecs.
2. Design for very large PDFs/images from the start — no full-document rasterization requirement.
3. Preserve Preview-like low-friction workflows, especially thumbnail DnD between documents.
4. **Never** implement touchscreen, stylus, pen, Windows Ink, or pressure input.
5. Ordinary mouse, keyboard, and precision-touchpad behavior must work.
6. Prefer offline/local processing; do not require cloud services for ordinary document/OCR/image/PDF ops.
7. Keep `Glyph.Core` free of WinUI and native engine packages.

## Delivery style

- Prefer vertical slices that produce usable user value.
- Use small, reviewable commits and logical PRs.
- Do not mark a feature complete merely because it compiles. Test expected behavior.
- For UI features, attach screenshots or recordings when practical.
- Update `FEATURE_MATRIX.md` status when work lands (`Not Started` → `In Progress` → `Implemented` → `Tested`, or `Deferred` with reason).

## Decision escalation

Make routine engineering decisions and document them. Ask the human only when the choice materially affects:

- product behavior
- licensing or cost
- privacy
- distribution
- a major irreversible architectural choice

## Build / test commands

Windows (full solution):

```powershell
./scripts/build.ps1
./scripts/test.ps1
./scripts/format.ps1
```

Core-only (non-UI libraries/tests; usable on non-Windows agents):

```powershell
./scripts/build.ps1 -CoreOnly
./scripts/test.ps1 -CoreOnly
```

CI source of truth: [`.github/workflows/ci.yml`](.github/workflows/ci.yml) on `windows-latest`.

## Project map

- `src/Glyph.App` — WinUI shell
- `src/Glyph.Core` — sessions, commands, selection, undo
- `src/Glyph.Infrastructure` — settings, recent files, paths, logging helpers
- `src/Glyph.Pdf` — PDF abstractions + adapters
- `src/Glyph.Imaging` — image abstractions + adapters
- `src/Glyph.Ocr` — OCR abstractions + adapters
- `tests/*` — xUnit behavior tests

## PR hygiene

- Branch names for cloud agents follow the repository’s required prefix/suffix policy.
- Keep PR descriptions tied to milestone/feature-matrix rows.
- Do not commit build outputs, user secrets, or gigantic binary fixtures; keep test fixtures small and purposeful.
