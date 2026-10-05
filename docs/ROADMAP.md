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
| M2 | Core PDF viewer | **Implemented** (+ post-core Zoom ▭ / Glass / Present) | M1 |
| M3 | Core PDF page manipulation | **Tested** (matrix F10–F12; §11 DnD screen recording + CI merge pending) | M2 |
| M4 | PDF markup and editing | **Implemented** (matrix-complete; Quadding `/Q` via dict patch) | M2 (forms/security touch M7) |
| M5 | Image viewer/editor | **Implemented** (matrix-complete; HDR/HEIF deferred) | M1 (shares shell/DnD with M3) |
| M6 | OCR and scanned-document capabilities | **In Progress** (PRs #62–#66 stacked; Actions billing blocks CI/merge) | M2, M5 |
| M7 | Redaction, PDF security, optimization, metadata | **In Progress** (redact/optimize/metadata Tested; password-write → ADR-015) | M2–M4 |
| M8 | Batch ops, scanner, color management, advanced | **Implemented** (hardware validation TBD; ML subject deferred) | M5–M7 |
| M9 | Performance, polish, a11y, installer, audit | **In Progress** (prefs/a11y/perf/session; MSIX → ADR-012) | M1–M8 core paths |

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
- Deferred (ADR-014): rectangular zoom, loupe, presentation mode — **shipped post-core** (Zoom ▭ / Glass / Present on m7 polish branch; ADR-014 annotated).
- Optional stretch still open: richer multi-line/column selection polish.
- PR #7 squash-merged to `main` (`33deca2`) with Windows + Linux CI green on head `0074671`.
- Post-core: Ctrl+A page text select-all; sidebar mode ComboBox; continuous-scroll throttled renders; cold-start ms.

---

## Milestone 3 — Core PDF page manipulation

**Status:** Tested · Depends on M2

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
- Remaining M3 polish: §11 cross-document DnD screen recording (same class of proof debt as M1 interactive screenshot). Matrix F10–F12 / F11 DnD rows are unit-Tested; Windows CI merge of local +N stack still blocked on Actions billing.

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

**Status:** Near Complete · Depends on M1

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

- Magick.NET adapter: `MagickImageDecoder` / `Document` / `Processor` / `Encoder` (open with EXIF AutoOrient, rotate, flip, crop, resize, color adjust including brightness/contrast/saturation/highlights/shadows/levels/gamma/temperature/tint/auto-levels/sharpness/sepia, metadata/EXIF/IPTC/XMP/GPS, selection extract/clear/move, freehand markup flatten, export PNG/JPEG/WebP/TIFF/BMP/GIF/AVIF/JP2/HEIC with quality/alpha/metadata/TIFF-compression/sRGB options).
- `ImageDocumentView`: zoom/fit, fullscreen, rotate L/R/180, Orient (normalize EXIF), Batch… folder rotate/flip/orient, flip H/V, numeric crop, interactive drag-crop (Crop… + aspect presets), rectangular/elliptical/freeform/smart-lasso pixel selection with invert (copy/cut/paste/delete/crop-to/move via drag or arrow keys), Draw markup overlay (freehand/rect/ellipse/line/arrow/text/callout; non-destructive until Flatten/Save), Stamp from signature library, clipboard Copy/Paste (whole image or selection), resize dialog (px/%/in/cm/mm, DPI, resampling, size estimate, optional batch folder scale %), Adjust dialog with live preview + luminance histogram + per-slider reset (brightness/contrast/highlights/shadows/levels/gamma/saturation/temperature/tint/sharpness/auto-levels/sepia), Meta (EXIF/IPTC/XMP/GPS copy/map/strip + Edit… for IPTC title/caption/keywords/copyright), Convert dialog (WebP/AVIF/JP2/HEIC quality/lossless + TIFF compression + preserve alpha/metadata + embed sRGB), JPEG quality export, folder prev/next + Slideshow (3s loop) + swipe nav + image list sidebar (in-place tab reuse when clean), edit undo (Ctrl+Z), save/export; Open picker includes HEIF/AVIF/JP2; wired from MainWindow for image kinds.
- Imaging.Tests cover processor round-trips including rotate-right/180, resize+DPI/filter, AdjustAsync (incl. shadows/highlights/levels/gamma/temp/tint), MoveRectAsync, elliptical/freeform/smart extract/clear/move, inverted extract/clear, FlattenMarkupAsync, IPTC read/write title/description/keywords/copyright, metadata strip on convert, TIFF LZW SaveAs, multi-format SaveAs, JPEG quality sizing, WebP lossless, AVIF/JP2 decode, EXIF read/orientation, GPS strip, folder sibling navigation, and crop display→pixel mapping.
- Deferred: HDR display (F26-24) and color-managed display (F26-25) → M8 §39 color management / WinUI HDR pipeline.
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
- **Blocked on GitHub Actions spending limit** (empty-step CI ~2s): do not push/merge until billing restores; stacked PRs #62–#66 + m7 #67 ready locally/remotely as applicable
---

## Milestone 7 — Redaction, PDF security, optimization, metadata

**Status:** In Progress (write-protect → ADR-015) · Depends on M2–M4

### Scope (`FEATURES.md` §21, §23–25)

- True redaction (content removal) — mark/preview/apply, find-matches, annotation sanitize
- Password open + Info encryption/permissions display; write-protect deferred pending ADR-015
- Optimization presets + custom controls + page image export
- Metadata view/edit (version, page size, fonts, title/author/subject/keywords/creator/producer)

### Completion criteria

- Redacted text not extractable after apply
- Optimization size estimate within reasonable tolerance
- Tests for security round-trips and metadata edits
- ADR-015 approved before shipping password-protect / permission-write

### Progress notes

- Redact mark/preview/apply removes intersecting text/images/annotations + attachment name-tree entries + Info metadata (F21 mark/apply/search-hit paths unit-tested)
- Info dialog: encryption/permissions, version, page size, fonts, attachment count; Edit writes Title/Author/Subject/Keywords/Creator/Producer (Ctrl+Z undoes); `/ModDate` stamped on edit
- Optimize presets + Custom (DPI + JPEG quality NumberBox) + estimate; HighQuality/Lossless/PreserveMonochrome covered by unit tests; F24-08 JPEG via zeroed FILEACCESS + LoadJpegFileInline; font subset/linearize deferred (ADR-016)
- Page Export → PNG/JPEG/WebP/TIFF/BMP/GIF/AVIF/JP2 with DPI/quality, Title/Author metadata, sRGB ICC (F45-07), alpha where codec allows
- FreeText Align / `/Q` quadding via post-save dict patch (F13-38 / F16-15)
- Bookmarks → PDF `/Outlines` export (F09-08)
- Password-protect write blocked on ADR-015 (Needs approval)
- Local polish (+139 on `cursor/m7-redaction-50da`) parked while Actions billing empty-step CI; push when jobs have real steps
- Print N-up/scale/rotate/center extracted to `PrintSheetLayout`; scroll throttle + presentation defaults unit-tested
- Search result snippets unified via `PdfSearchSnippet` (F06-10)
- Page drop accept/copy helpers + ink dash preview pattern extracted
- M3 insert prepend/append + text reading-order helpers unit-tested (F10-09/11/12, F07-03)
- Page paste/drag-out/registry helpers moved to Core/Pdf with tests (F10-24, F11-07/09)
- Cross-window page drop same-doc semantics via `PageDragSemantics` (F11-08)
- Merge prepend + undo-after-insert + PageReorder→ReorderPagesAsync edge tests
- Crop undo restores page size via `PdfPageEditHistory` (F12-08 / F49-07)
- Permanent crop undo + F59-01/02/09 multi-doc workflow rows Tested
- Contact sheet layout extract + merge undo; F59-05 / F61 non-destructive rows Tested
- Tab tear-off policy extract; F02-11/12 + F59-03/07 Tested
- SidebarModeCombo extract; F03-10 / F01-26 / F02-10 Tested
- ThumbnailWidthConstraints + DocumentClosePolicy; F03-01 / F04-13 / F01-22 Tested
- FileFormatDetector owns picker extensions + FilterSupportedPaths; F01-02 Tested
- ExplorerFileDropPolicy + ReadOnlySavePolicy; F01-08 / F01-13 Tested
- DocumentFileNamePolicy duplicate/rename; F01-15 / F01-19 Tested
- DocumentMovePolicy same-folder/overwrite; F01-20 Tested
- ToolbarVisibilityLabel; F02-05 Tested
- FullscreenTogglePolicy + MonitorCyclePolicy; F02-09 / F02-19 Tested
- ClipboardImageFileName + DocumentPropertiesRouting + StartupReadyStatus + PdfLoupeSampleRegion; F01-14/21 / F04-30 / F57-01 Tested
- DocumentSaveStatus + DpiAwarenessDeclaration; F01-16 / F02-18 / F02-20 Tested
- CaptureFileName camera/scan naming; F43-02 Tested; M3 section status → Tested
- PdfZoom ApplyManipulationScale + FindAllOpenPdfsStatus; F02-22 / F06-08 Tested
- ShellKeyboardShortcuts + PdfSearchHighlightStyle; F02-23 / F06-09 / F06-15 Tested
- DocumentExportFormats + ChromeAutomationNames; F01-18 / F02-24 / F56-01 Tested
- ShellMenuCatalog + PdfOutlineTree + WheelInputPolicy; F02-02 / F03-02/03 / F04-20 / F05-02/05 Tested
- ThumbnailContextMenu + PdfLinkAction/OutlineNavigation; F02-14 / F03-17 / F05-04/08 Tested
- PdfTextSelectAllPolicy + PdfSearchHitOrder + OutlineExpandPolicy + PdfPageSizeSet; F07-05 / F06-12 / F05-03 / F04-07 Tested
- PdfTextInteractionUi context/copy labels; F07-02/06/07/08/11 Tested
- Region/OCR/drag/chrome/render capability extracts; F02-01/21 / F04-02..05 / F07-09/12/13 / F56-08 Tested

---

## Milestone 8 — Batch operations, scanner, color management, advanced

**Status:** In Progress · Depends on M5–M7

### Scope (`FEATURES.md` §27, §29, §36, §39, §42–48 advanced)

- Batch image ops
- Scanner support (Windows APIs)
- Webcam capture for signatures/docs
- Color management / soft proof
- Animated image controls
- Smart selection / background removal (local)
- Printing polish, Share UI, inspector completeness (File → Properties / Ctrl+I)

### Completion criteria

- Batch job progress/cancel
- At least one scanner path validated on hardware when available (emulated tests otherwise)
- Color-managed display path documented and tested with profiled sample

### Progress notes

- Folder Batch… covers rotate/flip/orient, convert/export (PNG/JPEG/WebP/TIFF/BMP/GIF/AVIF/JP2), strip metadata, rename (`{name}-{n:000}`), and color profile assign/convert (sRGB/Adobe RGB); Resize dialog can scale all folder siblings; progress dialog with Cancel.
- Color management: detect ICC (`HasIccProfile`), assign/convert via Magick `SetProfile` / `TransformColorSpace`; Meta dialog Assign sRGB / Convert → sRGB; display honors ICC→sRGB (F39-02) with soft-proof Adobe RGB + rendering intent (F39-06/07/09); monitor profile / gamut warning deferred.
- Animated GIF/WebP: decoder coalesces multi-frame images; Play/Pause/Restart/prev/next frame, Loop, frame label, Save frame → PNG (F27-01–10).
- Background/subject: BG dialog corner flood-fill + fuzz, optional trim; extract to clipboard or PNG; Smart lasso covers F29-01/02 (F29-05 ML deferred).
- Printing: PDF/image Print… + Ctrl+P via WinUI `PrintManager`/`PrintDocument` (scope/range/scale/grayscale/center/auto-rotate; annotations in render; optional notes page; 1/2/4-up pages-per-sheet). System UI covers printer/copies/collate/duplex/paper (F44).
- Webcam import: File → Capture from Camera… opens PNG tab; PDF Camera stamps capture onto current page (F43). Signature webcam path unchanged.
- Scanner: File → Scan… discovers WinRT ImageScanner devices; flatbed/ADF, color/gray/B&W, DPI, duplex, auto-crop (single/multi-photo), straighten (Magick deskew), brightness/contrast, paper size (Letter/Legal/A4/… + feeder auto-detect); destinations images / new PDF / insert into open PDF (F42).
- Share/Explorer: File → Share / Show in File Explorer / Copy path|file / Open With / Send Email (F46–F47).
- Webcam signature capture already shipped in M4; HDR display (F26-24) still deferred.

## Milestone 9 — Performance, polish, accessibility, installer, audit

**Status:** In Progress · Depends on prior milestones’ core paths

### Scope (`FEATURES.md` §52–56, §57–60 remaining, distribution)

- Startup and large-doc performance pass
- Accessibility (UIA, keyboard, high contrast, text scaling) — toolbar icon Names started (F56-01/04/08)
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

### Progress notes

- Prefs, toolbar customization, session restore, crash recovery, version snapshots, a11y Names, cold-start timing shipped in matrix
- MSIX / file associations still Deferred (ADR-012)
- Matrix: essentially no In Progress rows left; remaining Blocked = ADR-015 password-write; F64-00 is product framing

---

## Feature-area status board

| Area | FEATURES.md | Milestone | Status |
| --- | --- | --- | --- |
| App/file handling | §1 | M1, M9 | Implemented (associations → ADR-012) |
| Main window/UI chrome | §2 | M1, M9 | Implemented |
| Sidebar modes | §3 | M2–M5 | Implemented (mode ComboBox + panels) |
| PDF viewing | §4 | M2 | Implemented (incl. Zoom ▭ / Glass / Present) |
| PDF TOC/links | §5 | M2 | Implemented |
| PDF search | §6 | M2, M6 | Implemented |
| PDF text interaction | §7 | M2, M6 | Implemented |
| OCR / Live Text | §8 | M6 | In Progress (PR stack #62–#66; billing blocks merge) |
| User bookmarks | §9 | M2/M4 | Implemented (app-local + export to PDF `/Outlines`) |
| PDF page manipulation | §10–12 | M3 | Tested (selection/reorder/insert/merge/split/crop/DnD/clipboard unit-covered; §11 screen recording pending) |
| PDF annotations/markup | §13–19 | M4 | Implemented |
| PDF forms | §20 | M4 | Implemented |
| Redaction | §21 | M7 | Tested (mark/preview/apply + sanitize + confirm copy; password-write separate) |
| Flattening | §22 | M4 | Tested (`FlattenAsync` / FPDFPage_Flatten) |
| PDF security | §23 | M7 | In Progress (open + info/permissions/advisory Tested; write-protect blocked on ADR-015) |
| Optimization | §24 | M7 | Tested (presets + downsample + JPEG quality + estimate + page export/ICC; font subset/linearize → ADR-016) |
| PDF metadata | §25 | M7 | Tested (read + edit title/author/subject/keywords/creator/producer + ModDate) |
| Image viewing/editing | §26–35 | M5 | Implemented (HDR/HEIF deferred) |
| Batch images | §36 | M8 | Implemented (ops + progress/cancel) |
| Animated images | §27 | M8 | Implemented (play/pause/frame nav/extract) |
| Smart selection / BG | §29 | M8 | Implemented (flood-fill remove; ML subject deferred) |
| Image metadata/GPS | §37–38 | M5, M8 | Implemented (EXIF/GPS inspector + strip; in-app map deferred) |
| Color management | §39 | M8 | Implemented (display ICC→sRGB + soft-proof; monitor ICC deferred) |
| Clipboard/screenshots | §40–41 | M1, M5 | Implemented (region/annot/image clipboard + Snipping Tool Ctrl+V) |
| Scanner/webcam | §42–43 | M8 | Implemented (webcam + scanner WinRT; hardware validation TBD) |
| Printing | §44 | M8 | Tested (system Print UI + `PrintSheetLayout` 1/2/4-up / scale / rotate / center) |
| Export/share/integration | §45–48 | M5–M9 | Implemented (PDF security export → ADR-015) |
| Undo/autosave/snapshots | §49–51 | M1–M4, M9 | Implemented (per-doc stacks + F50/F51; unified app-wide later) |
| Shortcuts/touchpad/toolbar/prefs | §52–55 | M1, M9 | Implemented (update check → ADR-012) |
| Accessibility | §56 | M9 | Implemented |
| Performance/large docs | §57–58 | M2+, M9 | Implemented (bg index / GPU deferred) |
| Multi-doc workflows | §59–60 | M1, M3 | Tested (tabs/windows/page+image DnD/clipboard/registry; interactive DnD demo pending) |
| Non-destructive editing | §61 | M3–M5 | Implemented (CropBox + in-memory image edits until Save) |
| Output formats | §62 | M5, M7 | Implemented (HEIF deferred) |
| Explicit exclusions | §63 | — | Documented (out of scope) |
| Product framing | §64 | all | Implemented (charter via FEATURES/ROADMAP/matrix) |
