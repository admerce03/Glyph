# Glyph Architecture

Authoritative product scope: [`FEATURES.md`](FEATURES.md).  
Roadmap: [`ROADMAP.md`](ROADMAP.md).  
Decisions log: [`DECISIONS.md`](DECISIONS.md).  
Feature tracking: [`FEATURE_MATRIX.md`](FEATURE_MATRIX.md).  
Packaging / MSIX: [`PACKAGING.md`](PACKAGING.md).

## 1. Goals

Glyph is a native Windows desktop application inspired by macOS Preview’s low-friction document workflows: open fast, manipulate PDF pages through thumbnails, drag pages between documents, and keep markup immediately accessible.

Non-goals for the platform layer:

- Electron / WebView / browser-wrapper shells
- Touchscreen, stylus, pen, Windows Ink, or pressure-sensitive input
- Cloud-required OCR, PDF, or image processing for ordinary operations
- Loading entire PDFs or images as full-resolution rasters in memory

## 2. Technology stack

| Layer | Choice | Notes |
| --- | --- | --- |
| Language | C# | Modern C# with nullable reference types |
| Runtime | .NET 10 | `net10.0` for libraries; `net10.0-windows10.0.19041.0` for the app |
| UI | WinUI 3 (WinUI) | Delivered via Windows App SDK |
| Windows App SDK | **2.5.1** (`Microsoft.WindowsAppSDK`) | Current stable umbrella package; revisit component packages later for publish-size optimization |
| Packaging | Unpackaged desktop first; optional MSIX | `WindowsPackageType=None` default; `-p:GlyphPackage=MSIX` + `Package.appxmanifest` / `scripts/publish-msix.ps1` (`-TestSign` on CI) |
| Architectures | x64 and ARM64 | No x86 target |
| DPI | Per-Monitor V2 | Declared in `app.manifest` |
| DI / logging | `Microsoft.Extensions.*` | Lightweight host composition inside the app |
| Tests | xUnit + FluentAssertions | Behavior tests required for completed features |
| CI | GitHub Actions `windows-latest` | Full WinUI build/test; Linux may run core-only tests |

## 3. Solution layout

```text
Glyph.sln
├── src/
│   ├── Glyph.App/                 WinUI shell, tabs, commands, drag-drop UI
│   ├── Glyph.Core/                Document sessions, commands, undo, selection
│   ├── Glyph.Infrastructure/      Paths, settings, recent files, logging helpers
│   ├── Glyph.Pdf/                 PDF abstractions + engine adapters
│   ├── Glyph.Imaging/             Image abstractions + codec adapters
│   └── Glyph.Ocr/                 OCR abstractions + Windows/Tesseract adapters
└── tests/
    ├── Glyph.Core.Tests/
    ├── Glyph.Infrastructure.Tests/
    ├── Glyph.Pdf.Tests/
    └── Glyph.Imaging.Tests/
```

Layering rules:

1. `Glyph.App` may reference all other projects.
2. `Glyph.Core` must not reference WinUI, PDFium, Magick.NET, or OCR natives.
3. Format engines (`Glyph.Pdf`, `Glyph.Imaging`, `Glyph.Ocr`) depend on `Glyph.Core` contracts and `Glyph.Infrastructure` utilities only.
4. Expensive or replaceable capabilities are always consumed through interfaces defined in the owning project’s `Abstractions` namespace.

## 4. Major components

### 4.1 Application shell (`Glyph.App`)

- Native WinUI window(s) with standard title bar
- Menu / command bar / keyboard accelerators
- Tabbed document host with tear-off / move-between-windows (Milestone 1+)
- Contextual sidebar host (thumbnails, TOC, search, annotations, images)
- Document viewport host that swaps PDF vs image viewers
- Global services: open/save dialogs, drag-drop routing, clipboard, printing bridge

### 4.2 Document model (`Glyph.Core`)

Every open file is a `DocumentSession`:

- Identity: path (nullable for untitled), display name, format kind
- State: dirty flag, read-only flag, last-known disk write time
- View state: zoom, layout mode, current page/image index, sidebar mode
- Selection: page/image/annotation/text selection scopes
- History: document-local undo/redo stack
- Capabilities: which services apply (PDF page ops, image adjust, OCR, etc.)

Documents are never assumed to be fully materialized rasters. PDFs expose page descriptors; images expose dimension/metadata plus lazily decoded pixel sources.

### 4.3 PDF subsystem (`Glyph.Pdf`)

Primary engine: **PDFium** via Apache-licensed .NET bindings (`PDFiumCore` / `bblanchon.PDFium.*`), wrapped behind Glyph interfaces.

Complementary library: **PdfPig** (Apache-2.0) for managed text/layout analysis and metadata helpers where it is stronger than raw PDFium bindings.

Key abstractions:

- `IPdfDocumentFactory` / `IPdfDocument`
- `IPdfPage` (size, rotation, boxes, text runs, links)
- `IPdfRenderer` (page → bitmap tile/surface at a scale)
- `IPdfPageEditor` (insert/delete/reorder/rotate/extract/merge/split)
- `IPdfAnnotationService`
- `IPdfFormStore`
- `IPdfSecurityService` (write-protect blocked on ADR-015; `BlockedPdfSecurityService`)
- `IPdfExportService` (page → image; app host implements with Magick/WIC)

### 4.4 Imaging subsystem (`Glyph.Imaging`)

Primary stack:

- Windows Imaging Component / WinRT `BitmapDecoder`/`BitmapEncoder` for common formats and color-managed display integration
- **Magick.NET** (Apache-2.0 ImageMagick wrapper) as the broad codec/processing adapter for formats and operations WIC does not cover well

ImageSharp is intentionally **not** a default dependency because of its split commercial license.

Abstracted as:

- `IImageDocument` (includes `GetMetadataAsync` for EXIF/IPTC/GPS)
- `IImageDecoder` / `IImageEncoder`
- `IImageProcessor` (crop/resize/rotate/flip/adjust + `FlattenMarkupAsync`)
- `ImageMarkupLayer` (strokes/shapes baked via processor)

### 4.5 OCR subsystem (`Glyph.Ocr`)

Offline-first:

1. Prefer `Windows.Media.Ocr` when available for the installed language packs
2. Optional Tesseract adapter for broader language packs / batch consistency

Always behind `IOcrEngine` with cancelable page/region jobs and text-hit overlays.

### 4.6 Infrastructure (`Glyph.Infrastructure`)

- Recent files and optional session restore
- Preferences persistence (JSON under `%LocalAppData%\Glyph`)
- Crash-recovery snapshots
- Path/long-path helpers
- Structured logging sinks
- Background work helpers / progress tokens

## 5. Rendering architecture

### 5.1 PDF virtualization

- Maintain a logical page list only (metadata + boxes), not bitmaps for all pages.
- Viewport requests visible page range (+ small prefetch window).
- Render scheduler prioritizes: current page → adjacent pages → visible thumbnails → remaining thumbnails.
- Page bitmap cache is LRU, scale-aware, and memory-budgeted.
- Zoom changes invalidate incompatible cache entries; fit/width modes compute scale from viewport size.
- Continuous / single / two-page layouts are viewport composition strategies over the same page model.
- Progressive or tiled rendering may be introduced for extremely large page dimensions; architecture must not preclude it.

### 5.2 Image virtualization

- Decode using scaled decode options where codecs support it.
- For very large images, keep a pyramid/cache of downscaled previews and decode full resolution only for the visible region / edit surface.
- Never require a full ARGB32 allocation of an entire multi-gigapixel image as a precondition for opening.

### 5.3 UI composition

- WinUI hosts surfaces; rasterization happens off the UI thread.
- Completed bitmaps are marshaled back via `DispatcherQueue`.
- Scrolling must remain responsive while renders are in flight; stale results are discarded by generation counters.

## 6. Editing architecture

### 6.1 Command pattern

All mutating operations implement `IEditCommand` with `Execute` / `Undo` and a human-readable name. The active `DocumentSession` owns the stack.

Selection-scoped commands (rotate selected pages, batch resize selected images) read the current selection from the session rather than embedding brittle UI state.

### 6.2 PDF page manipulation

Page operations mutate a page-order model and ask the PDF engine to rewrite page trees / produce new documents. Drag-and-drop between documents uses a Glyph-specific data payload describing source session + page ids, plus file drops from Explorer.

First-class DnD flows (from `FEATURES.md` §11):

- Thumbnails within a document → reorder
- Thumbnails across documents/windows/tabs → copy/move insert
- Explorer PDF → thumbnail sidebar insert
- Thumbnails → Explorer → new PDF extract

### 6.3 Annotations and markup

Annotations are retained as editable PDF annotation objects until the user flattens or exports flattened output. Image markup stays on a non-destructive layer until save/export to a flat raster format.

### 6.4 Non-destructive defaults

In-memory edit state is authoritative until Save/Save As/Export. Crash recovery snapshots capture enough to restore the session without silently overwriting originals.

## 7. Threading and background work

| Work | Threading |
| --- | --- |
| UI / input / XAML | UI thread |
| File open metadata / first page | Thread pool; first page prioritized |
| Page/thumbnail render | Dedicated render queue with cancellation |
| Text indexing | Background, incremental |
| OCR | Background, cancelable, one visible-page bias |
| Save/export/optimize/batch | Background with progress + cancellation |
| Autosave / recovery write | Background debounced I/O |

Rules:

- Never block the UI thread on native PDF/OCR/codec calls.
- Every long operation accepts `CancellationToken`.
- Render requests carry a generation id; outdated completions are dropped.
- Prefer channels/`Channel<T>` or a custom priority queue for render work over fire-and-forget tasks.

## 8. Persistence strategy

| Data | Location | Format |
| --- | --- | --- |
| Preferences | `%LocalAppData%\Glyph\settings.json` | JSON |
| Recent files | `%LocalAppData%\Glyph\recent.json` | JSON |
| Signatures library | `%LocalAppData%\Glyph\signatures\` | PNG + metadata JSON |
| Crash recovery | `%LocalAppData%\Glyph\recovery\` | temp docs + session manifest |
| Optional snapshots | `%LocalAppData%\Glyph\snapshots\<doc-id>\` | versioned copies |
| Temporary exports | `%Temp%\Glyph\` | deleted opportunistically |

Defaults:

- Edits stay in memory until Save
- Recovery snapshots are periodic and silent
- Closing a dirty document prompts
- Optional “autosave to original” is opt-in

## 9. Plugin / service boundaries

Glyph does not need a public plugin SDK in Milestone 0–3. Internally, treat engines as swappable services registered in DI:

```text
IPdfDocumentFactory  → PdfiumDocumentFactory
IPdfRenderer         → PdfiumRenderer
IImageDecoder        → WicImageDecoder (+ MagickImageDecoder fallback)
IOcrEngine           → WindowsOcrEngine (+ TesseractOcrEngine optional)
IRecentFilesStore    → JsonRecentFilesStore
ISettingsStore       → JsonSettingsStore
```

This keeps licensing-sensitive or native-heavy components replaceable without rewriting the shell.

## 10. Input model

Supported: mouse, keyboard, precision touchpad gestures that map to scroll/pinch-zoom.

Explicitly unsupported and must not be implemented: touchscreen gestures, stylus/pen, Windows Ink, pressure.

## 11. Security and privacy defaults

- Offline/local processing for ordinary PDF, image, and OCR operations
- No telemetry requirement for core features
- PDF permission restrictions are displayed honestly (they are not strong security)
- Redaction must remove content, not merely cover it
- Strip-metadata defaults are user-configurable

## 12. Distribution posture

Phase 0–8 develop and validate as an unpackaged WinUI app for Preview-like Explorer integration. Milestone 9 adds installer/MSIX/signing strategy (`Package.appxmanifest` + `scripts/publish-msix.ps1`; default build remains unpackaged). See [`PACKAGING.md`](PACKAGING.md) for publish, Developer Mode sideload, Explorer verify checklist, and Store-signing options. Self-contained publish (`WindowsAppSDKSelfContained`) remains available for machines without a preinstalled Windows App SDK runtime.

## 13. Build topology

- Developers on Windows build `Glyph.sln` with the Windows application development workload.
- CI on `windows-latest` builds the full solution and runs tests.
- Non-Windows agents may build/test `Glyph.Core`, `Glyph.Infrastructure`, and any TFM-`net10.0` pure-managed tests; WinUI projects are Windows-only.

See root `README.md` and `scripts/` for repeatable commands.
