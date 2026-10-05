# Glyph Architecture Decision Records

Format: short ADRs. Newest first within each status group.  
When a decision needs product/licensing/privacy approval, it is marked **Needs approval**.

---

## ADR-016 — Defer PDF font subsetting and Fast Web View linearization

**Status:** Accepted (Milestone 7)  
**Date:** 2026-10-05

### Context

M7 §24 asks for font subsetting and linearize / Fast Web View. PDFiumCore exposes neither a font-subset writer nor a linearization save flag (only `FPDF_SaveAsCopy` / `SaveWithVersion` with incremental / no-incremental / remove-security). Hand-rolling either feature is a large PDF-structure project outside Preview scope.

### Decision

1. Defer F24-11 (subset fonts) and F24-14 (linearize / Fast Web View) until a dedicated optimizer engine or approved second write stack lands.
2. Treat F24-12/13 (remove unused objects / optimize structure) as **best-effort** via `FPDF_NO_INCREMENTAL` full rewrite after attachment unlink and image downsample — not a full unused-object GC.

### Consequences

- Matrix rows F24-11/F24-14 marked Deferred with this ADR.
- Optimize presets remain useful for DPI downsample, attachment strip, metadata clear, and full rewrite.

---

## ADR-015 — PDF password-protect write path (no PDFium encrypt API)

**Status:** Needs approval (Milestone 7)  
**Date:** 2026-10-05

### Context

M7 §23 requires creating password-protected PDFs, setting user/owner passwords and permission flags, changing protection, and removing it when authorized. Glyph already **opens** encrypted PDFs via PDFium (`FPDF_LoadDocument` + password) and **displays** encryption/permission info (`IPdfDocumentInfoService`).

PDFiumCore’s save path is `FPDF_SaveAsCopy` / `FPDF_SaveWithVersion` only — there is **no** `SetPassword`, encrypt-on-save flag, or security-handler write API in the bindings we ship. PdfPig’s encryption types are read-path (open/decrypt); its writer does not emit a Standard Security Handler. Shipping F23-02–F23-07 therefore needs a **separate write-encrypt strategy**.

### Options

| Option | Approach | Pros | Cons |
| --- | --- | --- | --- |
| **A. PdfSharp / PdfSharpCore write-encrypt adapter** | Behind `IPdfSecurityService`: save/unencrypt with PDFium when needed, then apply user/owner password + `/P` flags with PdfSharp (MIT) or PdfSharpCore (MIT fork) | Known library; AES/RC4 Standard Security Handler; keeps PDFium for render | Extra dependency; round-trip may drop exotic constructs; must verify annotations/forms survive |
| **B. Hand-rolled Standard Security Handler (BCL crypto)** | After PDFium save, rewrite trailer `/Encrypt` + encrypt streams/strings with `System.Security.Cryptography` (RC4 rev 2–3 and/or AES-128/256 rev 4–6) | No new package; full control | High crypto/PDF correctness risk; long test surface; easy to ship broken interop |
| **C. Commercial PDF SDK encrypt** | License a commercial engine solely for security write | Fastest “correct” encrypt UX | Cost + licensing — product escalate |
| **D. Defer write-protect past M7 Preview** | Keep open + Info only; matrix rows stay Blocked | Zero risk now | §23 incomplete for Preview if protect is in-scope |

### Proposed decision (pending approval)

1. **Do not** implement password-write until this ADR is Accepted.
2. Prefer **Option A** (PdfSharp MIT, pinned centrally) behind a narrow `IPdfSecurityService` (set/change/remove protection + permission flags). Keep PDFium as the sole renderer/open path (ADR-003).
3. Reject **Option B** as a default — too easy to get wrong for Preview.
4. **Option C** only if A fails interoperability testing and licensing cost is approved.
5. First slice after approval: user (open) password round-trip testable with existing `encrypted.pdf` open path; then owner/permissions with the existing Info advisory warning (F23-09).

### Needs approval

- Adding a second PDF write stack (architecture).
- Shipping document encryption UX (major product/security behavior).
- Any move to a commercial SDK (cost/licensing).

### Consequences (if A accepted)

- New package pin in `Directory.Packages.props`; confine types to `Glyph.Pdf` security adapter.
- Automated round-trips: protect → reopen with PDFium password → wrong password fails → remove protection.
- Matrix F23-02–F23-07 move from Blocked → Implemented/Tested only after those tests land.

---

## ADR-001 — Native WinUI / .NET 10 stack

**Status:** Accepted (Phase 0)  
**Date:** 2026-10-04

### Context

Glyph must be a Windows desktop Preview-class document app with deep Explorer/DnD integration, high performance, and no browser-wrapper architecture.

### Decision

Use:

- C#
- .NET 10
- WinUI 3 (WinUI) via Windows App SDK
- Unpackaged app (`WindowsPackageType=None`) for early milestones

### Alternatives considered

| Option | Why not (for now) |
| --- | --- |
| Electron / WebView2 shell | Explicitly disallowed; weaker native DnD/file UX; higher memory |
| WPF | Mature, but WinUI is the current native Windows UI direction and matches WASDK APIs |
| WinForms | Inadequate for modern document UI/virtualization ambitions |
| C++/WinUI | Higher implementation cost; C# productivity preferred unless native interop demands otherwise |

### Consequences

- Full UI builds require Windows + Windows App SDK tooling
- CI must include a Windows runner
- MSIX/installer can be added later without rewriting the UI stack

---

## ADR-002 — Windows App SDK package references

**Status:** Accepted (Phase 0; revised during foundation setup)  
**Date:** 2026-10-04

### Context

The product requirement is to use the **current stable Windows App SDK**. As of Phase 0 that is **2.5.1**.

Component packages (`Microsoft.WindowsAppSDK.WinUI`, `Microsoft.WindowsAppSDK.Runtime`) can shrink publish output by omitting ONNX/DirectML, but the WinUI component line lagged (latest stable WinUI package observed: 2.3.9) and restoring `WinUI 2.3.9` produced `NU1603` against `InteractiveExperiences` when warnings are treated as errors.

### Decision

Use the umbrella package:

- `Microsoft.WindowsAppSDK` **2.5.1**

Revisit switching to component packages once the WinUI component versions align cleanly with the 2.5 stable line.

### Consequences

- Matches the “current stable Windows App SDK” requirement exactly
- Publish output may include unused AI/ML native binaries until a later optimization ADR
- Pin the version in `Directory.Packages.props`
---

## ADR-003 — PDF engine: PDFium behind interfaces

**Status:** Accepted (Phase 0)  
**Date:** 2026-10-04

### Context

Glyph needs high-quality PDF rendering, text extraction, annotations, forms, and page manipulation for large documents, with permissive licensing and offline use.

### Evaluation summary

| Library | Render | Text | Annot | Forms | Modify/pages | License | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| **PDFium** (+ `PDFiumCore` / `bblanchon.PDFium.*`) | Excellent (Chromium) | Yes | Yes (API surface) | AcroForm | Yes | Apache-2.0 + permissive third-party notices | Best permissive render engine |
| **PdfPig** | No | Strong | Read-limited | Read | Limited create/edit | Apache-2.0 | Excellent managed text/metadata companion |
| **Docnet.Core** | Yes | Basic | No | No | Split/merge/unlock | MIT wrapper / PDFium native | Narrower API than raw PDFium |
| **PDFiumZ** | Yes | Yes | High-level | Yes | Yes | Apache-2.0 claimed | Young project; revisit as optional adapter |
| **MuPDF** | Excellent | Yes | Yes | Yes | Yes | **AGPL** or commercial | Rejected without approval |
| **iText** | N/A focus | Yes | Yes | Yes | Yes | AGPL/commercial | Rejected without approval |
| **Syncfusion / Apryse / Patagames** | Yes | Yes | Yes | Yes | Yes | Commercial | Rejected without approval |

### Decision

1. Standardize on **PDFium** as the rendering and page-mutation engine.
2. Access it through Glyph abstractions (`IPdfDocument`, `IPdfRenderer`, `IPdfPageEditor`, etc.).
3. Use **`PDFiumCore`** (Apache-2.0 P/Invoke bindings) + official `bblanchon.PDFium.Win32` native binaries as the initial adapter.
4. Use **PdfPig** selectively for managed text/layout/metadata analysis.
5. Do **not** take AGPL/GPL/commercial PDF SDKs without a dedicated approval ADR.

### Licensing / redistribution notes

- PDFium is Apache-2.0; redistributed binaries include third-party notices (FreeType, ICU, zlib, libpng, etc.). Ship those notices with the app.
- NuGet packages may require license acceptance for the aggregated notices — comply in packaging.
- Keep native binaries behind the Pdf project so they are not referenced by Core tests unnecessarily.

### Consequences

- Need careful P/Invoke lifetime management (PDFium is not thread-safe for unconstrained concurrent access to one document)
- Annotation/form gaps in bindings must be filled incrementally in the adapter
- Engine remains replaceable if a better permissive option appears

---

## ADR-004 — Image codecs: WIC first, Magick.NET second; avoid ImageSharp by default

**Status:** Accepted (Phase 0)  
**Date:** 2026-10-04

### Context

Glyph must open many raster formats, support large images, and prefer permissive licenses.

### Decision

- Primary: Windows Imaging Component / WinRT imaging APIs for common formats and color-managed display integration
- Secondary broad codec/processor: **Magick.NET** (Apache-2.0)
- Do **not** take **Six Labors ImageSharp** as a default direct dependency due to its split license / commercial threshold and build-time license key enforcement in v4

### Needs approval

If product distribution is closed-source and organization revenue exceeds ImageSharp’s commercial threshold **and** we later want ImageSharp specifically, obtain approval before adding it.

### Consequences

- Native ImageMagick binaries increase package size when the Magick adapter is shipped
- Codec surface area is still interface-hidden for testability

---

## ADR-005 — OCR: Windows.Media.Ocr first, Tesseract optional

**Status:** Accepted (Phase 0)  
**Date:** 2026-10-04

### Decision

- Default offline engine: `Windows.Media.Ocr` (local language packs)
- Optional adapter: Tesseract (Apache-2.0) for additional languages / batch consistency
- No cloud OCR requirement for ordinary operations

### Consequences

- OCR quality depends on installed language packs for the Windows engine
- Tesseract redistributables/traineddata must be handled explicitly if enabled

---

## ADR-006 — Unpackaged-first distribution

**Status:** Accepted (Phase 0)  
**Date:** 2026-10-04

### Decision

Develop Glyph as an unpackaged WinUI desktop app first to preserve Preview-like double-click / Explorer workflows during development. Add MSIX/installer and finalized file-association strategy in Milestone 9 (earlier if associations prove blocked without packaging).

### Consequences

- Requires Windows App SDK bootstrap/runtime availability (framework-dependent or self-contained)
- Some Store-only APIs may be unavailable; prefer APIs that work unpackaged

---

## ADR-007 — Virtualized document architecture from day one

**Status:** Accepted (Phase 0)  
**Date:** 2026-10-04

### Decision

Never require full-document rasterization or full-image ARGB residency to open a file. Use page/region virtualization, bounded caches, cancelable render queues, and background thumbnail/search work.

### Consequences

- More complex viewport/render scheduler early
- Avoids catastrophic memory use on thousand-page PDFs and huge images later

---

## ADR-008 — No touchscreen / pen / Windows Ink features

**Status:** Accepted (from product spec)  
**Date:** 2026-10-04

### Decision

Do not implement touchscreen, stylus, pen, Windows Ink, or pressure-sensitive input. Support mouse, keyboard, and precision touchpad behaviors only.

### Consequences

- Simplifies input stack and testing matrix
- Freehand drawing is mouse-oriented

---

## ADR-009 — Central package management and analyzers

**Status:** Accepted (Phase 0)  
**Date:** 2026-10-04

### Decision

Use `Directory.Packages.props` + `Directory.Build.props` for shared TFM/nullable/analyzer settings. Enable .NET analyzers and treat warnings as errors in CI once the foundation is green.

### Consequences

- Dependency bumps happen in one place
- Agents must not introduce random package versions in project files

---

## ADR-010 — Test policy

**Status:** Accepted (Phase 0)  
**Date:** 2026-10-04

### Decision

- Every feature PR includes automated tests for the behavior it claims
- Pure logic lives in testable projects outside WinUI
- UI milestones attach screenshots or recordings when practical
- “Compiles” is never completion

### Consequences

- Slightly slower early feature delivery; much higher confidence for a long-running multi-agent project

---

## ADR-014 — Defer loupe, rectangular zoom, and presentation mode

**Status:** Accepted (Milestone 2); **superseded for shipping** — F04-29/30/31 Implemented post-core (M7 polish, 2026-10-05)  
**Date:** 2026-10-04

### Context

`FEATURES.md` §4 includes rectangular zoom-to-area, magnifier/loupe, and presentation/slideshow mode. The Milestone 2 completion bar for Glyph’s Core PDF Viewer (open/render/virtualize/navigate/layouts/zoom/text/Find/TOC/links/history/persistence/password) does not require those three tools to be demonstrably usable.

### Decision

Defer F04-29/F04-30/F04-31 until post-core polish (likely M9 or a focused M2.1 slice). Ship Fit/Width/Actual/arbitrary zoom, Ctrl+wheel, and precision-touchpad pinch first.

### Consequences

- Core viewer can be marked complete without loupe/rect/slideshow
- Matrix rows stay Deferred with this ADR as the reason rather than silent Not Started
- **Follow-up:** Zoom ▭ / Glass / Present shipped on `cursor/m7-redaction-50da`; matrix rows now Implemented

---

## ADR-013 — PDF text search behind `IPdfTextSearchService`

**Status:** Accepted (Milestone 2)  
**Date:** 2026-10-04

### Context

Glyph needs offline Find across PDF text layers. PdfPig is a strong managed extractor, but rendering already uses PDFium, and the text stack may later incorporate PDFium text APIs and/or OCR layers.

### Decision

1. Expose search only through `IPdfTextSearchService` / `PdfSearchResult` in `Glyph.Pdf.Text`.
2. Keep PdfPig confined to `PdfPigTextSearchService` (and test fixture generation). `Glyph.App` and `Glyph.Core` must not reference PdfPig types.
3. Run extraction/search on a thread-pool thread with cooperative cancellation between pages.
4. Use `PdfSearchCoordinator` so a new query cancels any in-flight search (no stale results).
5. Distinguish empty query, no matches, no extractable text (OCR required), and encrypted documents in `PdfSearchStatus` rather than throwing for expected user-facing cases.

### Consequences

- Find remains replaceable without UI churn
- Image-only/scanned PDFs fail clearly until M6 OCR wiring
- Passworded PDFs need an open/search password path before their text layer is searchable

---

## ADR-012 — File associations deferred to packaging

**Status:** Accepted (Milestone 1)  
**Date:** 2026-10-04

### Context

Native Explorer double-click / Open With associations are most reliable with MSIX (or a sparse package) identity. Glyph is unpackaged-first for development (ADR-006).

### Decision

Defer durable file-association registration to installer/MSIX work (Milestone 9), while Milestone 1 continues to support Open, drag-drop, and recent files. Document the gap in the feature matrix as Deferred with this reason.

---

## ADR-011 — FluentAssertions 7.x (not 8.x)

**Status:** Accepted (Phase 0)  
**Date:** 2026-10-04

### Decision

Pin **FluentAssertions 7.2.2** (Apache-2.0). Do not upgrade to 8.x without approval — FluentAssertions 8 introduces a commercial license/key model unsuitable as a default dependency.

---

## Rejected / parked options (summary)

| Option | Status | Reason |
| --- | --- | --- |
| Electron | Rejected | Product constraint |
| MuPDF AGPL | Rejected pending approval | Copyleft |
| Commercial PDF SDKs | Rejected pending approval | Cost/licensing |
| ImageSharp default | Rejected pending approval | Split commercial license |
| Cloud OCR default | Rejected | Offline/local preference |
