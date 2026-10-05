# Glyph Roadmap

Status legend for milestone/feature areas: **Not Started** · **In Progress** · **Implemented** · **Tested** · **Deferred**.

Product requirements source: [`FEATURES.md`](FEATURES.md).  
Per-requirement tracking: [`FEATURE_MATRIX.md`](FEATURE_MATRIX.md).

## Guiding principles

1. Vertical slices over disconnected infrastructure.
2. A minimal usable Glyph app early, then expand.
3. Do not mark features complete because they compile; require behavior tests and, for UI, visual proof when practical.
4. Prefer small reviewable PRs.
5. Adjust milestone order only for clear architectural dependencies; document the reason in [`DECISIONS.md`](DECISIONS.md).

## Milestone overview

| Milestone | Name | Status | Depends on |
| --- | --- | --- | --- |
| M0 | Architecture and engineering foundation | **Tested** | — |
| M1 | Application shell and basic file opening | **Implemented** (CI green; interactive screenshot pending) | M0 |
| M2 | Core PDF viewer | **Implemented** (landed via PR #7 → `33deca2`) | M1 |
| M3 | Core PDF page manipulation | **Implemented** (CI green; §11 cross-doc DnD screen recording pending) | M2 |
| M4 | PDF markup and editing | **In Progress** | M2 (forms/security touch M7) |
| M5 | Image viewer/editor | **In Progress** | M1 (shares shell/DnD with M3) |
| M6 | OCR and scanned-document capabilities | **In Progress** | M2, M5 |
| M7 | Redaction, PDF security, optimization, metadata | In Progress | M2–M4 |
| M8 | Batch ops, scanner, color management, advanced | Not Started | M5–M7 |
| M9 | Performance, polish, a11y, installer, audit | Not Started | M1–M8 core paths |

M5 may proceed in parallel with M3/M4 once M1 is stable, because imaging is behind separate interfaces. M3 remains the highest-priority Preview-differentiator after viewing works.

---

## Milestone 0 — Architecture and engineering foundation

**Status:** In Progress

### Scope

- Read and treat `FEATURES.md` as authoritative scope
- Choose native Windows stack (C# / .NET 10 / WinUI / Windows App SDK)
- Investigate PDF/image/OCR dependencies and licensing
- Author `ARCHITECTURE.md`, `ROADMAP.md`, `DECISIONS.md`, `AGENTS.md`, `FEATURE_MATRIX.md`
- Create solution/project structure
- Build scripts, test projects, formatting/analyzers
- Windows GitHub Actions CI
- Minimal runnable application shell
- Repeatable local build/test commands

### Completion criteria

- [x] Architecture and dependency strategy documented
- [x] Solution builds on Windows CI
- [x] Core unit tests pass in CI (Windows + Linux)
- [x] Unpackaged WinUI app project exists with shell UI (Windows CI build validated; interactive launch still manual)
- [x] Agents have persistent instructions in `AGENTS.md`
- [x] Feature matrix exists with every `FEATURES.md` section represented

---

## Milestone 1 — Application shell and basic file opening

**Status:** In Progress · Depends on M0

### Scope (`FEATURES.md` §1–2, parts of §40–41, §52)

- Native window, menu/command bar, toolbar host
- Tabs (open multiple documents)
- Drag-and-drop opening onto the window
- File → Open / Close / Exit
- Recent files
- File associations where practical (unpackaged progressive enhancement / later MSIX)
- New from Clipboard (image) baseline
- Theme follows system (light/dark)
- High-DPI / multi-monitor basics via WinUI + manifest

### Completion criteria

- [x] Open PDF and common images into tabs (viewers may still be placeholders until M2/M5)
- [x] Drop files onto the window to open
- [x] Recent files list persists across restarts
- [x] Automated tests for recent-files store, settings, and open routing
- [ ] Screenshot of shell with tabs (requires Windows interactive run)
- [x] Menu bar + keyboard accelerators (Open/Close/Exit/Next/Previous tab)
- [x] Theme preference (System/Light/Dark) persisted
- [x] Sidebar show/hide persisted
- [ ] File associations (deferred to packaging/MSIX — see ADR-012)
- [x] New from Clipboard baseline (image bitmap → dirty tab)

---

## Milestone 2 — Core PDF viewer

**Status:** Implemented · Depends on M1

### Scope (`FEATURES.md` §3–7, §57–58 PDF parts)

- PDFium-backed rendering behind `IPdfRenderer`
- Page virtualization and LRU cache
- Thumbnail sidebar
- Page navigation (goto/prev/next/first/last, wheel, Page Up/Down)
- Zoom (fit page/width/actual/custom, Ctrl+wheel, touchpad pinch where available)
- Continuous / single / two-page layouts
- Text selection + copy
- Search with results sidebar
- Password prompt for encrypted PDFs (open path)

### Completion criteria

- Open multi-hundred-page PDF without rasterizing all pages
- Memory stays bounded while scrolling
- Search finds and navigates matches
- Tests for page model, search, and cache eviction behavior
- Screenshots: continuous view, thumbnails, search hits

### Progress notes

- PDFium open/render + LRU cache + continuous viewer + bitmap thumbnails landed (landing PR #6).
- Offline Find hardened: async/cancelable PdfPig adapter behind `IPdfTextSearchService`, coordinator cancels in-flight queries, automated coverage for multi-page/multi-hit/empty/case/phrase/punctuation/Unicode/imperfect extraction/image-only/encrypted/large-doc/cancel cases.
- Viewer chrome: single/two-page layouts (incl. cover), fit page/width/100%, Ctrl+wheel + pinch zoom, First/Last/goto/PageUp/Down, in-document back/forward history, password-open prompt + typed `PdfPasswordRequiredException`.
- Text extraction/selection/copy, TOC outlines, and internal link navigation landed behind PDFium abstractions with tests.
- Durable page/zoom/layout persistence via `IDocumentViewStateStore`; drag text selection + on-page Find highlights.
- Continuous mode uses windowed page virtualization (`ContinuousPageWindow`) so large docs do not materialize every page control.
- Page `/Rotate` metadata read via PDFium; Find Clear + Escape clears results/overlays.
- Deferred (ADR-014): rectangular zoom, loupe, presentation mode.
- Optional stretch still open: search-all-open-PDFs, richer multi-line/column selection.
- PR #7 squash-merged to `main` (`33deca2`) with Windows + Linux CI green on head `0074671`.

---

## Milestone 3 — Core PDF page manipulation

**Status:** Implemented · Depends on M2

### Scope (`FEATURES.md` §10–12, §59 PDF DnD)

- Page selection (multi, range, keyboard)
- Reorder / rotate / delete / insert / duplicate
- Merge / split / extract
- Drag pages between documents/windows/tabs
- Drag PDFs from Explorer into thumbnail sidebar
- Drag pages out to Explorer as a new PDF
- Non-destructive CropBox crop

### Completion criteria

- [x] All first-class DnD workflows in §11 work (code + CI; interactive screen recording still pending)
- [x] Undo/redo for page ops
- [x] Round-trip tests on sample PDFs
- [ ] Screen recording of cross-document page drag

### Progress notes

- Landed via PRs #9–#15, #14 (CropBox), #18 (multi-window / keyboard / page clipboard) → `acd07ae`.
- `IPdfPageEditor` / `PdfiumPageEditor`: rotate, delete, reorder, extract, insert blank, duplicate, insert-from, merge/split, crop, save behind PDFium.
- `PageSelection` models Ctrl/Shift/keyboard thumbnail multi-select; viewer wires rotate/delete/move ↑↓/blank/dup/extract/merge/split/crop.
- Thumbnail drag-reorder within a document (`PageReorder` + thumb CanDrag/Drop).
- Snapshot undo/redo for page edits (`PdfPageEditHistory` + Ctrl+Z/Y).
- Explorer PDF → thumbnail insert, cross-tab/window page DnD (`PageDragPayload` / `PdfPageDragRegistry`), drag-out extract via deferred StorageItems, Ctrl+C/V via `PdfPageClipboard`.
- Non-destructive CropBox crop (`CropPagesAsync` / `SetCropBoxAsync` + numeric dialog / visual handles + optional permanent export).
- Multi-window shell (`File → New Window`) with per-window `WorkspaceState`.
- Remaining M3 polish: §11 cross-document DnD screen recording (same class of proof debt as M1 interactive screenshot).

---

## Milestone 4 — PDF markup and editing

**Status:** In Progress · Depends on M2

### Scope (`FEATURES.md` §13–20, §22)

- Highlights / underline / strikethrough
- Notes, text boxes, callouts
- Shapes and freehand mouse drawing
- Signatures (mouse / image import / webcam)
- AcroForm fill + overlay form mode
- Annotation sidebar
- Flatten annotations

### Completion criteria

- [x] Markup survives save/reopen
- [x] Flatten produces non-editable visuals
- [x] Form field tab order works on sample AcroForms
- [x] Tests for annotation model serialization

### Progress notes

- `IPdfAnnotationService` / `PdfiumAnnotationService`: create/list/remove text markup (Highlight / Underline / StrikeOut) with QuadPoints + color; round-trip save/reopen covered by Pdf.Tests.
- Viewer: select text → Highlight (multi-color picker; persistent mode toggles so every selection highlights) / Underline / Strike toolbar actions; sidebar Color recolors selected markup.
- Annotation sidebar lists text markup; click jumps to page; Delete removes selected markup.
- Sticky notes: `AddStickyNoteAsync` + Note toolbar dialog (text + color presets); Contents/color/move APIs; Sidebar Edit → `SetContentsAsync`; notes appear in sidebar; Expand/Collapse popup overlay; Export notes → printable text listing (`PdfNotesExport`).
- Freehand ink: `AddInkAsync` + Ink draw mode on page surface (stroke color + width picker); listed in annotation sidebar; Ctrl+Z undoes last ink/freeform/polygon stroke; smart drawing (`PdfStrokeShapeRecognizer`) offers cleaned line/rect/ellipse/triangle after ink/freeform.
- Shapes: `AddShapeAsync` for rectangle/ellipse (Square/Circle), line, arrow, star, speech bubble (ink strokes; arrow adds head wings; star is a closed 5-point path; bubble is body+pointer outline), and loupe (Circle + Contents=Loupe; select shows 3× magnified crop); Rect/Ellipse/Line/Arrow/Star/Bubble/Loupe draw modes with border/fill color and width picker; line/arrow selection uses endpoint handles (`SetLineEndpointsAsync`).
- Freeform: `AddFreeformAsync` closed ink path; Freeform draw mode.
- Polygon: `AddPolygonAsync` click-to-place vertices (Enter / near-first closes); Polygon toolbar mode.
- Text boxes: `AddTextBoxAsync` FreeText with Contents + DA + optional fill/border; TextBox toolbar dialog (font family/size/bold/italic/underline, text/fill/border color); listed in sidebar.
- Callouts: `AddCalloutAsync` FreeText (`Subj=Callout`) + ink pointer; Callout draw mode (drag tip → box) with font + text color dialog; underline via `SetUnderlineAsync`.
- Flatten: `FlattenAsync` via `FPDFPage_Flatten` + Flatten toolbar (confirm dialog); editable annots removed after bake.
- Signatures: `AddStampAsync` (BGRA stamp image) + local `FileSignatureLibrary`; Sign toolbar Draw (mouse stroke → PNG/library/stamp), Import image, or Webcam (`MediaCapture` preview + capture with near-white paper keying via `SignaturePaperKeying`); library dialog lists saved signatures with ↑/↓ reorder + Insert/Delete; `DuplicateAsync` clones stamp pixels with offset.
- AcroForm: `IPdfFormStore` / `PdfiumFormStore` lists widgets, sets text/combo/list `/V`, toggles checkboxes (`/V`+`/AS`), selects radios (mutual exclusion by field name), exposes choice `/Opt` via PdfPig, tab-adjacent focus; Form toolbar Overlay mode (clickable field boxes) or list dialog; recent text values via `IFormValueHistory` (`form-values.json`); AutoFill profile (name/address/email/phone) via `IFormAutofillProfileStore`; text fill sets `/DA` to `0 Tf` (automatic font sizing) via `PdfFormDefaultAppearance`; push buttons resolve URI `/A` (`PdfFormButtonAction`) and activate from Form UI; signature fields accept visual stamp fill from the signature library; Pdf.Tests sample AcroForm.
- Annotation selection: click annot on page (or sidebar) to select; Ctrl+click / Extended list multi-select; Group/Ungroup persists `GlyphGroup` and selects/moves members together; drag moves via `MoveAsync` (multi moves together); corner/edge handles resize; line/arrow endpoint handles; Rotate 90° via `RotateAsync` (stamp/ink/FreeText/shapes); Dup clones (offset); Color / Opacity / Width via `SetColorAsync` / `SetOpacityAsync` / `SetBorderWidthAsync`; Delete removes all selected; selection chrome on overlay.

---

## Milestone 5 — Image viewer/editor

**Status:** In Progress · Depends on M1

### Scope (`FEATURES.md` §26–35, §37–38, §61 image parts)

- Major formats via WIC + Magick.NET adapter
- Navigation, zoom/pan, image list sidebar
- Crop / resize / rotate / flip
- Conversion + color adjustments
- Markup layer (shared tool model with PDF where practical)
- Metadata/EXIF/GPS inspector basics

### Completion criteria

- Open large images without mandatory full decode
- Round-trip edit tests for crop/resize/rotate
- Screenshots of viewer and crop UI

### Progress notes

- Magick.NET adapter: `MagickImageDecoder` / `Document` / `Processor` / `Encoder` (open with EXIF AutoOrient, rotate, flip, crop, resize, color adjust including brightness/contrast/saturation/highlights/shadows/levels/gamma/temperature/tint/auto-levels/sharpness/sepia, metadata/EXIF/IPTC/XMP/GPS, selection extract/clear/paste/move, freehand markup flatten, export PNG/JPEG/WebP/TIFF/BMP/GIF/AVIF/JP2/HEIC with quality/alpha/metadata/TIFF-compression/sRGB options).
- `ImageDocumentView`: zoom/fit, fullscreen, rotate L/R/180, Orient (normalize EXIF), flip H/V, numeric crop, interactive drag-crop (Crop… + aspect presets), rectangular/elliptical/freeform-lasso pixel selection (copy/cut/paste/delete/crop-to/move via drag or arrow keys), Draw markup overlay (freehand/rect/ellipse/line/arrow/text/callout; non-destructive until Flatten/Save), Stamp from signature library, clipboard Copy/Paste (whole image or selection), resize dialog (px/%/in/cm/mm, DPI, resampling, size estimate), Adjust (brightness/contrast/highlights/shadows/levels/gamma/saturation/temperature/tint/sharpness/auto-levels/sepia), Meta (EXIF/IPTC/XMP/GPS copy/map/strip), Convert dialog (WebP/AVIF/JP2/HEIC quality/lossless + TIFF compression + preserve alpha/metadata + embed sRGB), JPEG quality export, folder prev/next + Slideshow (3s loop) + swipe nav + image list sidebar (in-place tab reuse when clean), edit undo (Ctrl+Z), save/export; Open picker includes HEIF/AVIF/JP2; wired from MainWindow for image kinds.
- Imaging.Tests cover processor round-trips including rotate-right/180, resize+DPI/filter, AdjustAsync (incl. shadows/highlights/levels/gamma/temp/tint), MoveRectAsync, elliptical/freeform extract/clear/move, FlattenMarkupAsync, IPTC title/description/keywords/copyright, metadata strip on convert, TIFF LZW SaveAs, multi-format SaveAs, JPEG quality sizing, WebP lossless, AVIF/JP2 decode, EXIF read/orientation, GPS strip, folder sibling navigation, and crop display→pixel mapping.
---

## Milestone 6 — OCR and scanned-document capabilities

**Status:** In Progress · Depends on M2, M5

### Scope (`FEATURES.md` §8, search OCR hooks in §6)

- Detect/select text on images and scanned PDF pages
- OCR page / selection / document
- Optional embed OCR text layer into PDF
- Actionable entities (URL/email/phone/address/date) with contextual actions
- Fully offline path

### Completion criteria

- OCR works without network
- Search includes OCR text when present
- Cancelable OCR jobs with progress

### Progress notes

- `IOcrEngine` / `OcrRequest` / `OcrResult` abstractions in `Glyph.Ocr`
- `WindowsOcrEngine` (Windows.Media.Ocr) registered in App DI; **OCR** toolbar on `ImageDocumentView` with result dialog + copy
- `UnsupportedOcrEngine` + Fake engine coverage in `Glyph.Ocr.Tests` (Linux)
- PDF **OCR** current/selected/entire document on `PdfDocumentView` (chooser → render → BGRA → engine + result dialog)
- **Cancel OCR** + `n/m` status progress for multi-page PDF recognition
- Document **Find** merges session OCR page text (`PdfPageTextSearch`) when OCR has been run
- Find on image-only PDFs offers OCR-current-page fallback; **Find sel** searches the text selection
- PDF **Live Text** word-box overlay after OCR (click/Ctrl+click select + **Copy OCR**)
- **OCR→PDF** exports OCR'd pages as a searchable PDF (image + invisible text via `OcrSearchablePdfWriter`)
- **Entities** dialog on PDF OCR text (URL/email/phone/address/date/time + maps/calendar/search web)
- Right-click selected text → Copy / Find selection / Search web / Copy region as image
- Multi-line drag selects across lines in reading order; Alt/wide drag keeps column rect selection
- Drag selected text out of the page (OLE/text drag) when a selection exists
- **Find in all open PDFs** (Edit menu / Ctrl+Shift+F) aggregates PdfPig hits across tabs
- Image-folder OCR / Live Text image overlays still on parallel entities/live-text stack
---

## Milestone 7 — Redaction, PDF security, optimization, metadata

**Status:** In Progress · Depends on M2–M4

### Scope (`FEATURES.md` §21, §23–25)

- True redaction (content removal) — mark/preview/apply, find-matches, annotation sanitize
- Password open + Info encryption/permissions display; write-protect deferred pending ADR-015
- Optimization presets + custom controls + page image export
- Metadata view/edit (version, page size, fonts, title/author/subject/keywords)

### Completion criteria

- Redacted text not extractable after apply
- Optimization size estimate within reasonable tolerance
- Tests for security round-trips and metadata edits
- ADR-015 approved before shipping password-protect / permission-write

### Progress notes

- Redact mark/preview/apply removes intersecting text/images/annotations + attachment name-tree entries + Info metadata
- Info dialog: encryption/permissions, version, page size, fonts, attachment count; Edit writes Title/Author/Subject/Keywords
- Optimize presets + Custom + estimate; F24-08 JPEG rewrite blocked (PDFiumCore FILEACCESS); font subset/linearize deferred (ADR-016)
- Page Export → PNG/JPEG/WebP/TIFF/BMP/GIF/AVIF/JP2 with DPI/quality, Title/Author metadata, sRGB ICC (F45-07), alpha where codec allows
- Password-protect write blocked on ADR-015 (Needs approval)

---

## Milestone 8 — Batch operations, scanner, color management, advanced

**Status:** Not Started · Depends on M5–M7

### Scope (`FEATURES.md` §27, §29, §36, §39, §42–48 advanced)

- Batch image ops
- Scanner support (Windows APIs)
- Webcam capture for signatures/docs
- Color management / soft proof
- Animated image controls
- Smart selection / background removal (local)
- Printing polish, Share UI, inspector completeness

### Completion criteria

- Batch job progress/cancel
- At least one scanner path validated on hardware when available (emulated tests otherwise)
- Color-managed display path documented and tested with profiled sample

---

## Milestone 9 — Performance, polish, accessibility, installer, audit

**Status:** Not Started · Depends on prior milestones’ core paths

### Scope (`FEATURES.md` §52–56, §57–60 remaining, distribution)

- Startup and large-doc performance pass
- Accessibility (UIA, keyboard, high contrast, text scaling)
- Shortcut customization
- Toolbar customization polish
- Installer / MSIX / file associations finalize
- Complete feature-spec audit against `FEATURE_MATRIX.md`
- No silent drops: every requirement Implemented/Tested or Deferred with reason

### Completion criteria

- Matrix has no blank/unknown rows
- Installer produces a clean-machine runnable build
- Accessibility smoke pass
- Performance checklist signed off for representative large PDF/image fixtures

---

## Feature-area status board

| Area | FEATURES.md | Milestone | Status |
| --- | --- | --- | --- |
| App/file handling | §1 | M1, M9 | In Progress |
| Main window/UI chrome | §2 | M1, M9 | In Progress |
| Sidebar modes | §3 | M2–M5 | In Progress |
| PDF viewing | §4 | M2 | Implemented |
| PDF TOC/links | §5 | M2 | Implemented |
| PDF search | §6 | M2, M6 | Implemented |
| PDF text interaction | §7 | M2, M6 | Implemented |
| OCR / Live Text | §8 | M6 | In Progress |
| User bookmarks | §9 | M2/M4 | Not Started |
| PDF page manipulation | §10–12 | M3 | Implemented |
| PDF annotations/markup | §13–19 | M4 | In Progress |
| PDF forms | §20 | M4 | In Progress |
| Redaction | §21 | M7 | Implemented (mark/preview/apply + sanitize options; password-write separate) |
| Flattening | §22 | M4 | Tested (`FlattenAsync` / FPDFPage_Flatten) |
| PDF security | §23 | M7 | In Progress (open + info/permissions; write-protect blocked on ADR-015) |
| Optimization | §24 | M7 | In Progress (presets + downsample + estimate + page export/ICC; JPEG rewrite blocked) |
| PDF metadata | §25 | M7 | Implemented (read + edit title/author/subject/keywords) |
| Image viewing/editing | §26–35 | M5 | In Progress |
| Batch images | §36 | M8 | Not Started |
| Image metadata/GPS | §37–38 | M5, M8 | In Progress (EXIF/GPS inspector + strip) |
| Color management | §39 | M8 | Not Started |
| Clipboard/screenshots | §40–41 | M1, M5 | Not Started |
| Scanner/webcam | §42–43 | M8 | Not Started |
| Printing | §44 | M8 | Not Started |
| Export/share/integration | §45–48 | M5–M9 | In Progress (PDF page export formats/DPI/quality/metadata/ICC/alpha) |
| Undo/autosave/snapshots | §49–51 | M1–M4, M9 | Not Started |
| Shortcuts/touchpad/toolbar/prefs | §52–55 | M1, M9 | Not Started |
| Accessibility | §56 | M9 | Not Started |
| Performance/large docs | §57–58 | M2+, M9 | In Progress |
| Multi-doc workflows | §59–60 | M1, M3 | In Progress (tabs/windows/page DnD/clipboard/undo; tab tear-off + §60 context cmds open) |
| Non-destructive editing | §61 | M3–M5 | In Progress (CropBox crops; annotations/markup later) |
| Output formats | §62 | M5, M7 | In Progress (PNG/JPEG/WebP/TIFF/BMP/GIF/AVIF/JP2; HEIF deferred) |
| Explicit exclusions | §63 | — | Documented (out of scope) |
