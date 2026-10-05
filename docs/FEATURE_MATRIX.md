# Glyph Feature Completion Matrix

Source of truth: [`FEATURES.md`](FEATURES.md).

Status values: `Not Started` · `In Progress` · `Implemented` · `Tested` · `Deferred` (reason required).

Update this file when work lands. Do not delete rows to hide scope.

| ID | Area | Requirement | Milestone | Status | Notes |
| --- | --- | --- | --- | --- | --- |
| F01-01 | Application and file handling | Open files through: | M1/M9 | In Progress | Open / Open Multiple / drag-drop in shell |
| F01-02 | Application and file handling | Open multiple files simultaneously. | M1/M9 | In Progress | Multi-tab + multi-select open |
| F01-03 | Application and file handling | Open multiple files: | M1/M9 | Not Started |  |
| F01-04 | Application and file handling | Reopen recently used files. | M1/M9 | Tested | JsonRecentFilesStore covered by unit tests |
| F01-05 | Application and file handling | Restore previously open tabs/windows after restart, optionally. | M1/M9 | Not Started |  |
| F01-06 | Application and file handling | Native Windows file associations. | M1/M9 | Deferred | ADR-012: wait for MSIX/installer packaging |
| F01-07 | Application and file handling | Configurable default associations for supported formats. | M1/M9 | Deferred | ADR-012: wait for MSIX/installer packaging |
| F01-08 | Application and file handling | Drag files from Explorer into an existing application window. | M1/M9 | In Progress | Explorer → window drop opens documents |
| F01-09 | Application and file handling | Drag files from the application into Explorer where meaningful. | M1/M9 | In Progress | Thumbnail drag exposes extracted PDF via deferred StorageItems |
| F01-10 | Application and file handling | Open files from: | M1/M9 | Not Started |  |
| F01-11 | Application and file handling | Normal Windows long-path and Unicode filename support. | M1/M9 | Not Started |  |
| F01-12 | Application and file handling | Read-only file detection. | M1/M9 | Not Started |  |
| F01-13 | Application and file handling | Warn when attempting to overwrite a read-only file. | M1/M9 | Not Started |  |
| F01-14 | Application and file handling | File → New from Clipboard. | M1/M9 | In Progress | Menu + Ctrl+Shift+N; clipboard bitmap → temp PNG dirty tab |
| F01-15 | Application and file handling | File → Duplicate. | M1/M9 | Not Started |  |
| F01-16 | Application and file handling | File → Save. | M1/M9 | Not Started |  |
| F01-17 | Application and file handling | File → Save As. | M1/M9 | Not Started |  |
| F01-18 | Application and file handling | File → Export. | M1/M9 | Not Started |  |
| F01-19 | Application and file handling | File → Rename. | M1/M9 | Not Started |  |
| F01-20 | Application and file handling | File → Move. | M1/M9 | Not Started |  |
| F01-21 | Application and file handling | File → Properties. | M1/M9 | Not Started |  |
| F01-22 | Application and file handling | Close: | M1/M9 | Not Started |  |
| F01-23 | Application and file handling | Unsaved-change prompt where appropriate. | M1/M9 | Not Started |  |
| F01-24 | Application and file handling | Optional autosave. | M1/M9 | Not Started |  |
| F01-25 | Application and file handling | Crash-recovery copy. | M1/M9 | Not Started |  |
| F01-26 | Application and file handling | Undo/redo history. | M1/M9 | In Progress | Page-edit snapshot undo via `PdfPageEditHistory`; app-wide stack later |
| F01-27 | Application and file handling | Do not silently overwrite originals by default. | M1/M9 | Not Started |  |
| F02-01 | Main window and interface | Standard Windows title bar. | M1/M9 | Not Started |  |
| F02-02 | Main window and interface | Menu bar or equivalent command interface. | M1/M9 | In Progress | MenuBar File/View/Window |
| F02-03 | Main window and interface | Customizable toolbar. | M1/M9 | Not Started |  |
| F02-04 | Main window and interface | Optional compact toolbar. | M1/M9 | Not Started |  |
| F02-05 | Main window and interface | Hide/show toolbar. | M1/M9 | Not Started |  |
| F02-06 | Main window and interface | Hide/show sidebar. | M1/M9 | In Progress | Sidebar hide/show persisted |
| F02-07 | Main window and interface | Resizable sidebar. | M1/M9 | Not Started |  |
| F02-08 | Main window and interface | Resizable document area. | M1/M9 | Not Started |  |
| F02-09 | Main window and interface | Full-screen mode. | M1/M9 | Not Started |  |
| F02-10 | Main window and interface | Tabbed documents. | M1/M9 | In Progress | TabView + Ctrl+Tab navigation |
| F02-11 | Main window and interface | Tear tab into separate window. | M1/M9 | Not Started |  |
| F02-12 | Main window and interface | Move tabs between windows. | M1/M9 | Not Started |  |
| F02-13 | Main window and interface | Reorder tabs. | M1/M9 | Not Started |  |
| F02-14 | Main window and interface | Context menus throughout. | M1/M9 | Not Started |  |
| F02-15 | Main window and interface | Dark mode. | M1/M9 | In Progress | Theme preference Dark |
| F02-16 | Main window and interface | Light mode. | M1/M9 | In Progress | Theme preference Light |
| F02-17 | Main window and interface | Follow Windows system theme. | M1/M9 | In Progress | Theme preference System → ElementTheme.Default |
| F02-18 | Main window and interface | High-DPI scaling. | M1/M9 | In Progress | PerMonitorV2 declared in app.manifest |
| F02-19 | Main window and interface | Multi-monitor support. | M1/M9 | Not Started |  |
| F02-20 | Main window and interface | Per-monitor DPI awareness. | M1/M9 | In Progress | PerMonitorV2 declared in app.manifest |
| F02-21 | Main window and interface | Mouse support. | M1/M9 | Not Started |  |
| F02-22 | Main window and interface | Precision touchpad gesture support where useful. | M1/M9 | Not Started |  |
| F02-23 | Main window and interface | Keyboard-first operation. | M1/M9 | Not Started |  |
| F02-24 | Main window and interface | Accessibility through Windows UI Automation. | M1/M9 | Not Started |  |
| F03-01 | Sidebar modes | Page thumbnails | M2-M5 | Implemented | Thumbnail strip in PDF viewer sidebar |
| F03-02 | Sidebar modes | Table of contents | M2-M5 | Implemented | Outline TreeView when bookmarks present |
| F03-03 | Sidebar modes | Search results | M2-M5 | Implemented | Find hits list under Search header |
| F03-04 | Sidebar modes | Bookmarks | M2-M5 | Not Started |  |
| F03-05 | Sidebar modes | Highlights and annotations | M2-M5 | Not Started |  |
| F03-06 | Sidebar modes | Image list | M2-M5 | Not Started |  |
| F03-07 | Sidebar modes | Contact sheet | M2-M5 | Not Started |  |
| F03-08 | Sidebar modes | Document attachments, if supported | M2-M5 | Not Started |  |
| F03-09 | Sidebar modes | Metadata/properties where useful | M2-M5 | Not Started |  |
| F03-10 | Sidebar modes | Switch sidebar mode without opening another window. | M2-M5 | In Progress | Thumbs/TOC/search coexist in one panel; dedicated mode switcher later |
| F03-11 | Sidebar modes | Resize thumbnail size. | M2-M5 | Not Started |  |
| F03-12 | Sidebar modes | Multi-select sidebar items. | M2-M5 | Not Started |  |
| F03-13 | Sidebar modes | Shift-click range selection. | M2-M5 | Not Started |  |
| F03-14 | Sidebar modes | Ctrl-click noncontiguous selection. | M2-M5 | Not Started |  |
| F03-15 | Sidebar modes | Keyboard navigation. | M2-M5 | Not Started |  |
| F03-16 | Sidebar modes | Drag selected items. | M2-M5 | Not Started |  |
| F03-17 | Sidebar modes | Context menus. | M2-M5 | Not Started |  |
| F03-18 | Sidebar modes | Sidebar selection controls the scope of many editing commands. | M2-M5 | Not Started |  |
| F04-01 | PDF viewing | Open standard PDF files quickly. | M2 | Tested | PDFium open+render wired in shell |
| F04-02 | PDF viewing | Render vector content accurately. | M2 | Implemented | PDFium vector render path |
| F04-03 | PDF viewing | Render embedded images. | M2 | Implemented | Via PDFium rasterization |
| F04-04 | PDF viewing | Render embedded fonts. | M2 | Implemented | Via PDFium rasterization |
| F04-05 | PDF viewing | Support transparency. | M2 | Implemented | Via PDFium rasterization |
| F04-06 | PDF viewing | Support rotated pages. | M2 | Tested | `FPDFPageGetRotation` + size swap; Pdf.Tests cover /Rotate 90 |
| F04-07 | PDF viewing | Support PDFs containing different page sizes. | M2 | Implemented | Per-page size from PDFium |
| F04-08 | PDF viewing | Password-protected PDF opening. | M2 | Tested | `PdfPasswordRequiredException` + ContentDialog prompt |
| F04-09 | PDF viewing | Continuous scrolling. | M2 | Implemented | Continuous scroll with windowed page virtualization |
| F04-10 | PDF viewing | Single-page mode. | M2 | Implemented | Layout combo → SinglePage |
| F04-11 | PDF viewing | Two-page/facing-page mode. | M2 | Implemented | Even/odd spreads |
| F04-12 | PDF viewing | Optional cover-page behavior for facing pages. | M2 | Tested | `TwoPageWithCover` layout + calculator tests |
| F04-13 | PDF viewing | Page thumbnails. | M2 | Implemented | Bitmap thumbnails at low scale |
| F04-14 | PDF viewing | Page number navigation. | M2 | Implemented | Status + goto box + thumbs |
| F04-15 | PDF viewing | Go to page. | M2 | Implemented | Goto box (# + Enter) |
| F04-16 | PDF viewing | Previous page. | M2 | Implemented | Prev button / Page Up |
| F04-17 | PDF viewing | Next page. | M2 | Implemented | Next button / Page Down |
| F04-18 | PDF viewing | First page. | M2 | Implemented | First button / Home |
| F04-19 | PDF viewing | Last page. | M2 | Implemented | Last button / End |
| F04-20 | PDF viewing | Mouse-wheel scrolling. | M2 | Implemented | ScrollViewer wheel scrolling |
| F04-21 | PDF viewing | Page Up/Page Down navigation. | M2 | Implemented | Key handlers in viewer |
| F04-22 | PDF viewing | Fit page. | M2 | Tested | `PdfZoomCalculator.FitPage` + toolbar |
| F04-23 | PDF viewing | Fit width. | M2 | Tested | `PdfZoomCalculator.FitWidth` + toolbar |
| F04-24 | PDF viewing | Actual size / 100%. | M2 | Tested | 100% control |
| F04-25 | PDF viewing | Custom zoom percentage. | M2 | Implemented | Zoom percentage shown in status |
| F04-26 | PDF viewing | Zoom in/out. | M2 | Implemented | Zoom +/- controls |
| F04-27 | PDF viewing | Ctrl+mouse wheel zoom. | M2 | Implemented | Ctrl+wheel → zoom steps |
| F04-28 | PDF viewing | Pinch-to-zoom on supported precision touchpads. | M2 | Implemented | Ctrl+wheel + Manipulation Scale pinch |
| F04-29 | PDF viewing | Rectangular zoom-to-area. | M2 | Deferred | ADR-014: post-core polish; not required for M2 minimum viewer |
| F04-30 | PDF viewing | Magnifier/loupe tool. | M2 | Deferred | ADR-014: post-core polish; not required for M2 minimum viewer |
| F04-31 | PDF viewing | Presentation/slideshow mode. | M2 | Deferred | ADR-014: post-core polish; not required for M2 minimum viewer |
| F04-32 | PDF viewing | Remember last viewed page, optionally. | M2 | Tested | `IDocumentViewStateStore` persists page on close/reopen |
| F04-33 | PDF viewing | Remember zoom/layout per document, optionally. | M2 | Tested | Persists zoom + layout with page index |
| F05-01 | PDF table of contents and navigation | Read embedded PDF outlines/bookmarks. | M2 | Tested | `IPdfOutlineService` / PDFium bookmarks |
| F05-02 | PDF table of contents and navigation | Hierarchical table of contents. | M2 | Implemented | Nested `PdfOutlineNode` tree |
| F05-03 | PDF table of contents and navigation | Expand/collapse outline nodes. | M2 | Implemented | TreeView expand/collapse |
| F05-04 | PDF table of contents and navigation | Click outline entry to navigate. | M2 | Implemented | Outline invoke → GoToPage |
| F05-05 | PDF table of contents and navigation | Keyboard navigation. | M2 | In Progress | Page Up/Down/Home/End in viewer |
| F05-06 | PDF table of contents and navigation | Preserve embedded outline while editing when possible. | M2 | Not Started | Editing arrives in later milestones |
| F05-07 | PDF table of contents and navigation | Show internal PDF links. | M2 | Tested | `IPdfLinkService` enumerates page links |
| F05-08 | PDF table of contents and navigation | Support clickable: | M2 | Implemented | Click link rect → internal page jump |
| F05-09 | PDF table of contents and navigation | Back/forward navigation history within the document. | M2 | Tested | `DocumentNavigationHistory` + Back/Fwd buttons |
| F06-01 | PDF search | Full-text search. | M2/M6 | Tested | `IPdfTextSearchService` + PdfPig adapter; covered by Pdf.Tests |
| F06-02 | PDF search | Case-insensitive search. | M2/M6 | Tested | Default `OrdinalIgnoreCase` |
| F06-03 | PDF search | Optional case-sensitive search. | M2/M6 | Tested | `PdfSearchOptions.CaseSensitive` + UI toggle |
| F06-04 | PDF search | Exact phrase search. | M2/M6 | Tested | Default exact-phrase substring match |
| F06-05 | PDF search | Any-word search. | M2/M6 | Implemented | Service supports `ExactPhrase: false`; UI still phrase-default |
| F06-06 | PDF search | Search all occurrences. | M2/M6 | Tested | Collects every hit per page |
| F06-07 | PDF search | Search current PDF. | M2/M6 | Tested | Current document path |
| F06-08 | PDF search | Search all open PDFs. | M2/M6 | Implemented | Edit → Find in all open PDFs (Ctrl+Shift+F) |
| F06-09 | PDF search | Highlight matches on pages. | M2/M6 | Implemented | Gold overlays from PDFium char boxes |
| F06-10 | PDF search | Results sidebar. | M2/M6 | Implemented | Results list in viewer sidebar |
| F06-11 | PDF search | Show contextual snippets around matches. | M2/M6 | Tested | Snippet around match |
| F06-12 | PDF search | Sort results by: | M2/M6 | Implemented | Page order (document scan order); relevance sort deferred |
| F06-13 | PDF search | Next match. | M2/M6 | Implemented | Toolbar next-match control |
| F06-14 | PDF search | Previous match. | M2/M6 | Implemented | Toolbar previous-match control |
| F06-15 | PDF search | Clear search. | M2/M6 | Implemented | Clear button + Escape; cancels in-flight search and overlays |
| F06-16 | PDF search | Click result to jump to it. | M2/M6 | Tested | Selection jumps to hit page index |
| F06-17 | PDF search | Search OCR text where OCR has been generated. | M2/M6 | Implemented | Session OCR cache merged into Find results (`PdfPageTextSearch`) |
| F07-01 | PDF text interaction | Text selection. | M2/M6 | Implemented | Click near glyphs selects word-ish run via PDFium text |
| F07-02 | PDF text interaction | Copy text. | M2/M6 | Implemented | Copy button / Ctrl+C |
| F07-03 | PDF text interaction | Select across lines. | M2/M6 | Implemented | Drag stream selection across lines; Alt/wide drag = column rect |
| F07-04 | PDF text interaction | Select columns where practical. | M2/M6 | In Progress | Alt-drag or wide region uses rectangular column selection |
| F07-05 | PDF text interaction | Select all text on page/document. | M2/M6 | In Progress | Copy falls back to full page text |
| F07-06 | PDF text interaction | Right-click selected text. | M2/M6 | Implemented | Context menu: Copy / Find selection / Search web / Copy region as image |
| F07-07 | PDF text interaction | Copy. | M2/M6 | Implemented | Clipboard text package |
| F07-08 | PDF text interaction | Search selected text. | M2/M6 | Implemented | Find sel toolbar uses current text selection as query |
| F07-09 | PDF text interaction | OCR fallback on scanned PDFs. | M2/M6 | Implemented | Find on image-only PDF offers OCR current page then re-searches |
| F07-10 | PDF text interaction | Preserve reasonable reading order during copy. | M2/M6 | Implemented | `PdfTextSelection` top-to-bottom / left-to-right with newlines |
| F07-11 | PDF text interaction | Rectangular selection for copying a region as an image. | M2/M6 | Implemented | Drag region + right-click Copy region as image |
| F07-12 | PDF text interaction | Copy selected PDF region to clipboard as bitmap. | M2/M6 | Implemented | Renders page crop to PNG bitmap on clipboard |
| F07-13 | PDF text interaction | Drag selected text where supported. | M2/M6 | Implemented | Page CanDrag exports selected text via DragStarting |
| F08-01 | OCR / Live Text equivalent | Detect text automatically or on demand. | M6 | Implemented | Image + PDF page OCR toolbar → Windows.Media.Ocr |
| F08-02 | OCR / Live Text equivalent | Select detected text directly over the image. | M6 | Implemented | PDF OCR word-box overlay (click/Ctrl+click) + Copy OCR; image overlay on Live Text PR |
| F08-03 | OCR / Live Text equivalent | Copy text. | M6 | Implemented | OCR result dialog Copy text |
| F08-04 | OCR / Live Text equivalent | Copy all recognized text. | M6 | Implemented | Same as copy full result text |
| F08-05 | OCR / Live Text equivalent | Search recognized text. | M6 | Implemented | Find merges session OCR cache; OCR word overlays after page OCR |
| F08-06 | OCR / Live Text equivalent | OCR current page. | M6 | Implemented | PdfDocumentView OCR uses selected-or-current pages |
| F08-07 | OCR / Live Text equivalent | OCR selected pages. | M6 | Implemented | Multi-select thumbnails → OCR concatenates page sections |
| F08-08 | OCR / Live Text equivalent | OCR entire PDF. | M6 | Implemented | OCR chooser Secondary = entire document page range |
| F08-09 | OCR / Live Text equivalent | OCR multiple images. | M6 | Not Started |  |
| F08-10 | OCR / Live Text equivalent | Optionally embed OCR text layer into PDF. | M6 | Implemented | OCR→PDF export via `OcrSearchablePdfWriter` invisible text |
| F08-11 | OCR / Live Text equivalent | Preserve image underneath the OCR layer. | M6 | Implemented | Full-bleed page render under invisible text layer |
| F08-12 | OCR / Live Text equivalent | URLs | M6 | Implemented | `OcrEntityDetector` + Entities dialog Open |
| F08-13 | OCR / Live Text equivalent | email addresses | M6 | Implemented | `OcrEntityDetector` + mailto launch |
| F08-14 | OCR / Live Text equivalent | phone numbers | M6 | Implemented | `OcrEntityDetector` + Copy value |
| F08-15 | OCR / Live Text equivalent | physical addresses | M6 | Implemented | `OcrEntityDetector` street + city/ST/ZIP |
| F08-16 | OCR / Live Text equivalent | dates | M6 | Implemented | `OcrEntityDetector` date patterns |
| F08-17 | OCR / Live Text equivalent | times | M6 | Implemented | `OcrEntityDetector` time patterns |
| F08-18 | OCR / Live Text equivalent | Open URL in default browser. | M6 | Implemented | Entities → Open / act |
| F08-19 | OCR / Live Text equivalent | Create email in default mail application. | M6 | Implemented | Entities → mailto: |
| F08-20 | OCR / Live Text equivalent | Copy phone number. | M6 | Implemented | Entities → Copy value |
| F08-21 | OCR / Live Text equivalent | Open address in user's default mapping/web service. | M6 | Implemented | Entities → Bing Maps query |
| F08-22 | OCR / Live Text equivalent | Create calendar event through appropriate Windows/system mechanism where feasible. | M6 | Implemented | Entities → temp `.ics` via `OcrCalendarInvite` |
| F08-23 | OCR / Live Text equivalent | Search web. | M6 | Implemented | Entities dialog → Bing |
| F08-24 | OCR / Live Text equivalent | Copy recognized value. | M6 | Implemented | Entities → Copy value |
| F09-01 | PDF bookmarks | Add bookmark at current page/location. | M2/M4 | Not Started |  |
| F09-02 | PDF bookmarks | Rename bookmark. | M2/M4 | Not Started |  |
| F09-03 | PDF bookmarks | Delete bookmark. | M2/M4 | Not Started |  |
| F09-04 | PDF bookmarks | List bookmarks in sidebar. | M2/M4 | Not Started |  |
| F09-05 | PDF bookmarks | Reorder bookmarks where feasible. | M2/M4 | Not Started |  |
| F09-06 | PDF bookmarks | Click to navigate. | M2/M4 | Not Started |  |
| F09-07 | PDF bookmarks | Preserve bookmarks when saving. | M2/M4 | Not Started |  |
| F09-08 | PDF bookmarks | Export them as standard PDF bookmarks where compatible. | M2/M4 | Not Started |  |
| F10-01 | PDF page manipulation | Select page thumbnails. | M3 | Implemented | Click thumbnails; chrome shows selection |
| F10-02 | PDF page manipulation | Multi-select pages. | M3 | Tested | Ctrl+click / Ctrl+A via `PageSelection` |
| F10-03 | PDF page manipulation | Select ranges. | M3 | Tested | Shift+click / Shift+↑↓ via `PageSelection` |
| F10-04 | PDF page manipulation | Reorder pages by dragging thumbnails. | M3 | Tested | Thumbnail drag-drop + `PageReorder` helper |
| F10-05 | PDF page manipulation | Move multiple pages as one selection. | M3 | Implemented | Move ↑/↓ shifts contiguous selection |
| F10-06 | PDF page manipulation | Delete pages. | M3 | Tested | `IPdfPageEditor.DeletePagesAsync` + toolbar |
| F10-07 | PDF page manipulation | Duplicate pages. | M3 | Tested | `DuplicatePagesAsync` + Dup toolbar |
| F10-08 | PDF page manipulation | Insert blank page. | M3 | Tested | `InsertBlankPageAsync` + Blank toolbar |
| F10-09 | PDF page manipulation | Insert PDF file. | M3 | Implemented | Explorer PDF → thumbnail sidebar calls `InsertPagesAsync` |
| F10-10 | PDF page manipulation | Insert pages from another PDF. | M3 | Tested | `InsertPagesAsync` covered by Pdf.Tests |
| F10-11 | PDF page manipulation | Append PDF. | M3 | Implemented | Insert at `PageCount` |
| F10-12 | PDF page manipulation | Prepend PDF. | M3 | Implemented | Insert at index 0 |
| F10-13 | PDF page manipulation | Insert at arbitrary position. | M3 | Tested | Insert index parameter |
| F10-14 | PDF page manipulation | Extract selected pages. | M3 | Tested | `ExtractPagesAsync` returns new `IPdfDocument` |
| F10-15 | PDF page manipulation | Save extracted pages as new PDF. | M3 | Tested | `SaveAsync` + Extract save picker |
| F10-16 | PDF page manipulation | Split PDF. | M3 | Tested | `SplitDocumentAsync` + Split toolbar → folder |
| F10-17 | PDF page manipulation | Merge PDFs. | M3 | Tested | `MergeDocumentsAsync` + Merge file picker |
| F10-18 | PDF page manipulation | Rotate selected pages left. | M3 | Tested | Toolbar ⟲ → `-90°` |
| F10-19 | PDF page manipulation | Rotate selected pages right. | M3 | Tested | Toolbar ⟳ → `+90°` |
| F10-20 | PDF page manipulation | Batch rotation. | M3 | Tested | Multi-select rotate uses same editor path |
| F10-21 | PDF page manipulation | Crop selected page. | M3 | Tested | `CropPagesAsync` / Crop toolbar dialog |
| F10-22 | PDF page manipulation | Apply same crop to multiple pages. | M3 | Tested | Multi-select + SetCropBox / apply-all |
| F10-23 | PDF page manipulation | Change page order. | M3 | Implemented | `ReorderPagesAsync` + Move ↑/↓ |
| F10-24 | PDF page manipulation | Copy/paste pages between documents. | M3 | Implemented | DnD + Ctrl+C/V via `PdfPageClipboard` |
| F11-01 | Preview-style PDF drag-and-drop workflows | one page | M3 | Implemented | Thumbnail CanDrag + page payload |
| F11-02 | Preview-style PDF drag-and-drop workflows | multiple contiguous pages | M3 | Implemented | Multi-select drag uses selection set |
| F11-03 | Preview-style PDF drag-and-drop workflows | multiple noncontiguous pages | M3 | Implemented | Noncontiguous selection preserved in payload |
| F11-04 | Preview-style PDF drag-and-drop workflows | insertion before/after any page | M3 | Implemented | Drop Y half chooses before/after |
| F11-05 | Preview-style PDF drag-and-drop workflows | visible insertion indicator | M3 | Implemented | Orange before/after border highlight |
| F11-06 | Preview-style PDF drag-and-drop workflows | insert entire PDF at drop location. | M3 | Implemented | Explorer `.pdf` StorageItems → insert |
| F11-07 | Preview-style PDF drag-and-drop workflows | create a new PDF containing those selected pages. | M3 | Implemented | Deferred StorageItems extract on drag-out |
| F11-08 | Preview-style PDF drag-and-drop workflows | different windows | M3 | Implemented | File → New Window; per-window workspace + shared page drag registry |
| F11-09 | Preview-style PDF drag-and-drop workflows | different tabs | M3 | Implemented | Cross-tab insert via `PdfPageDragRegistry` |
| F12-01 | PDF crop | Rectangular page crop. | M3 | Implemented | CropBox rectangle via margins / absolute box |
| F12-02 | PDF crop | Visual crop handles. | M3 | Implemented | Interactive overlay handles + Apply/Cancel chrome |
| F12-03 | PDF crop | Numeric crop dimensions. | M3 | Implemented | L/T/R/B inset dialog |
| F12-04 | PDF crop | Units: | M3 | Implemented | pt / in / cm / mm via `PdfLengthUnits` |
| F12-05 | PDF crop | Apply to current page. | M3 | Implemented | Selection defaults to current page |
| F12-06 | PDF crop | Apply to selected pages. | M3 | Tested | Multi-select crop |
| F12-07 | PDF crop | Apply to all pages. | M3 | Implemented | Dialog checkbox |
| F12-08 | PDF crop | Undo crop. | M3 | Implemented | Via `PdfPageEditHistory` snapshots |
| F12-09 | PDF crop | Preserve underlying PDF content if using non-destructive CropBox changes. | M3 | Tested | Sets CropBox only |
| F12-10 | PDF crop | Optional permanent crop/export function. | M3 | Tested | `PermanentCropPagesAsync` + Export cropped… |
| F13-01 | PDF annotations | Highlight | M4 | Tested | `AddTextMarkupAsync(Highlight)` + toolbar |
| F13-02 | PDF annotations | Underline | M4 | Tested | `AddTextMarkupAsync(Underline)` + toolbar |
| F13-03 | PDF annotations | Strikethrough | M4 | Tested | `AddTextMarkupAsync(StrikeOut)` + toolbar |
| F13-04 | PDF annotations | Freehand ink | M4 | Tested | `AddInkAsync` + Ink draw mode |
| F13-05 | PDF annotations | Lines | M4 | Tested | `AddShapeAsync(Line)` via 2-point ink (no PDFium SetLine) |
| F13-06 | PDF annotations | Arrows | M4 | Tested | `AddShapeAsync(Arrow)` ink shaft + head |
| F13-07 | PDF annotations | Rectangles | M4 | Tested | `AddShapeAsync(Rectangle)` + Rect draw mode |
| F13-08 | PDF annotations | Rounded rectangles | M4 | Tested | `AddShapeAsync(RoundedRectangle)` via Square + `FPDFAnnot_SetBorder` radii |
| F13-09 | PDF annotations | Ellipses | M4 | Tested | `AddShapeAsync(Ellipse)` + Ellipse draw mode |
| F13-10 | PDF annotations | Polygons | M4 | Tested | `AddPolygonAsync` click-to-place vertices + Polygon toolbar |
| F13-11 | PDF annotations | Callouts | M4 | Tested | `AddCalloutAsync` FreeText+ink pointer; Callout draw mode |
| F13-12 | PDF annotations | Text boxes | M4 | Tested | `AddTextBoxAsync` FreeText + TextBox toolbar |
| F13-13 | PDF annotations | Sticky notes | M4 | Tested | Same as F15-01 `AddStickyNoteAsync` |
| F13-14 | PDF annotations | Speech-bubble-like callouts | M4 | Tested | `AddShapeAsync(SpeechBubble)` + Bubble draw mode (shape outline; FreeText callout remains F13-11) |
| F13-15 | PDF annotations | Stamps, optionally | M4 | Tested | Same path as signatures (`AddStampAsync`) |
| F13-16 | PDF annotations | Signatures | M4 | Tested | Stamp via `AddStampAsync` + Sign toolbar |
| F13-17 | PDF annotations | Freeform shapes | M4 | Tested | `AddFreeformAsync` closed ink; Freeform draw mode |
| F13-18 | PDF annotations | Annotation selection tool | M4 | Tested | Click annot on page / sidebar; drag moves |
| F13-19 | PDF annotations | move | M4 | Tested | `MoveAsync` API + drag on page |
| F13-20 | PDF annotations | resize | M4 | Tested | Selection handles → `MoveAsync` new bounds |
| F13-21 | PDF annotations | rotate where appropriate | M4 | Tested | `RotateAsync` 90° CW for stamp/ink/shapes/FreeText; sticky & text markup unsupported |
| F13-22 | PDF annotations | duplicate | M4 | Tested | `DuplicateAsync` offset clone + sidebar Dup |
| F13-23 | PDF annotations | delete | M4 | Tested | `RemoveAsync` by page/annot index |
| F13-24 | PDF annotations | cut | M4 | Implemented | Sidebar Cut / Ctrl+X; removes source on paste |
| F13-25 | PDF annotations | copy | M4 | Implemented | Sidebar Copy / Ctrl+C when annot selected |
| F13-26 | PDF annotations | paste | M4 | Implemented | Sidebar Paste / Ctrl+V; `DuplicateAsync` + optional cut-remove |
| F13-27 | PDF annotations | multi-select | M4 | Implemented | Ctrl+click toggle; sidebar Extended; bulk delete/move |
| F13-28 | PDF annotations | group where useful | M4 | Tested | Sidebar Group/Ungroup; `GlyphGroup` key; select/move together |
| F13-29 | PDF annotations | change border color | M4 | Tested | Set on create + `SetColorAsync` |
| F13-30 | PDF annotations | change fill color | M4 | Tested | `SetFillColorAsync` (InteriorColor) + sidebar Fill; also on shape create |
| F13-31 | PDF annotations | change opacity | M4 | Tested | `SetOpacityAsync` + sidebar Opacity slider |
| F13-32 | PDF annotations | change line thickness | M4 | Tested | Create + sidebar Width → `SetBorderWidthAsync` (ink/shapes/text box) |
| F13-33 | PDF annotations | change line style | M4 | Tested | Line/Arrow ink: Solid/Dashed/Dotted via segmented strokes |
| F13-34 | PDF annotations | change arrowheads | M4 | Tested | Arrow tool: Open / Filled / Diamond ink heads |
| F13-35 | PDF annotations | change font | M4 | Implemented | TextBox/Callout Font combo → DA resource (Helv/TiRo/Cour + bold/italic) |
| F13-36 | PDF annotations | change font size | M4 | Implemented | TextBox/Callout dialog NumberBox → `fontSizePoints` |
| F13-37 | PDF annotations | change text color | M4 | Implemented | TextBox/Callout dialog StrokePresets → `textColor` / DA |
| F13-38 | PDF annotations | change text alignment | M4 | Blocked | PDFium has no `FPDFAnnot_SetNumberValue` for FreeText `/Q` (Quadding) |
| F14-01 | Highlight workflow | Select text and apply highlight. | M4 | Implemented | Selection quads → Highlight toolbar |
| F14-02 | Highlight workflow | Persistent highlight mode: | M4 | Implemented | Highlight toggles mode; Esc exits; color remembered |
| F14-03 | Highlight workflow | Multiple highlight colors. | M4 | Tested | Yellow/Green/Pink/Blue/Orange picker |
| F14-04 | Highlight workflow | Change existing highlight color. | M4 | Tested | Sidebar Color → presets / `SetColorAsync` |
| F14-05 | Highlight workflow | Underline selection. | M4 | Implemented | Underline toolbar |
| F14-06 | Highlight workflow | Strikethrough selection. | M4 | Implemented | Strike toolbar |
| F14-07 | Highlight workflow | Remove markup. | M4 | Implemented | Sidebar Delete + `RemoveAsync` |
| F14-08 | Highlight workflow | Annotation sidebar showing all: | M4 | Implemented | Sidebar lists highlight/underline/strike |
| F14-09 | Highlight workflow | Click annotation in sidebar to jump to it. | M4 | Implemented | Selection jumps to annotation page |
| F15-01 | Notes | Add sticky note. | M4 | Tested | `AddStickyNoteAsync` + Note toolbar |
| F15-02 | Notes | Enter note text. | M4 | Implemented | ContentDialog on add |
| F15-03 | Notes | Collapse note. | M4 | Implemented | Sidebar Collapse hides sticky popup overlay |
| F15-04 | Notes | Expand note. | M4 | Implemented | Sidebar Expand / select note shows contents popup on page |
| F15-05 | Notes | Move note icon. | M4 | Tested | `MoveAsync` API; drag UI later |
| F15-06 | Notes | Change note color. | M4 | Tested | Create picker + sidebar Color → StickyNotePresets / `SetColorAsync` |
| F15-07 | Notes | Edit. | M4 | Implemented | Sidebar Edit → `SetContentsAsync` (notes/text boxes/callouts) |
| F15-08 | Notes | Delete. | M4 | Implemented | Sidebar Delete |
| F15-09 | Notes | Show note author. | M4 | Implemented | Sidebar label includes `/T` author when set |
| F15-10 | Notes | Configurable annotation author name. | M4 | Implemented | Author button + sticky-note dialog; written to `/T` |
| F15-11 | Notes | Optional date/time metadata. | M4 | Implemented | Sticky notes set `/CreationDate` and `/M` on create |
| F15-12 | Notes | Show all notes in sidebar. | M4 | Implemented | Annotations list includes notes |
| F15-13 | Notes | Print notes optionally. | M4 | Implemented | Annotations → Export notes → printable `.txt` via `PdfNotesExport` |
| F16-01 | Text boxes and callouts | Add text box anywhere. | M4 | Tested | `AddTextBoxAsync` + TextBox toolbar |
| F16-02 | Text boxes and callouts | Type arbitrary text over PDF. | M4 | Implemented | Contents via dialog |
| F16-03 | Text boxes and callouts | Move it. | M4 | Tested | `MoveAsync` API |
| F16-04 | Text boxes and callouts | Resize it. | M4 | Tested | `MoveAsync` with new bounds |
| F16-05 | Text boxes and callouts | Rotate where appropriate. | M4 | Tested | Sidebar Rotate → `RotateAsync` swaps FreeText bounds around center |
| F16-06 | Text boxes and callouts | Font family. | M4 | Implemented | Helvetica/Times/Courier via DA (`PdfFreeTextFont`) |
| F16-07 | Text boxes and callouts | Font size. | M4 | Implemented | `fontSizePoints` + TextBox/Callout dialog NumberBox |
| F16-08 | Text boxes and callouts | Bold. | M4 | Implemented | Dialog Bold → HeBo/TiBo/CoBo in DA |
| F16-09 | Text boxes and callouts | Italic. | M4 | Implemented | Dialog Italic → HeOb/TiIt/CoOb in DA |
| F16-10 | Text boxes and callouts | Underline. | M4 | Tested | `SetUnderlineAsync` + `GlyphUnderline` + companion ink stroke; TextBox/Callout checkbox |
| F16-11 | Text boxes and callouts | Text color. | M4 | Implemented | DA RGB from `textColor` + TextBox/Callout picker |
| F16-12 | Text boxes and callouts | Background/fill color. | M4 | Implemented | `fillColor` → InteriorColor; TextBox dialog picker |
| F16-13 | Text boxes and callouts | Border. | M4 | Tested | Border color + width on create |
| F16-14 | Text boxes and callouts | Opacity. | M4 | Implemented | Sidebar Opacity → `SetOpacityAsync` |
| F16-15 | Text boxes and callouts | Alignment. | M4 | Blocked | Same Quadding limitation as F13-38 |
| F16-16 | Text boxes and callouts | Callout pointer. | M4 | Tested | Ink pointer from tip to nearest box edge |
| F16-17 | Text boxes and callouts | Move pointer separately from text box. | M4 | Tested | Sidebar Tip → click page; `SetCalloutTipAsync` rebuilds ink pointer |
| F17-01 | Shapes | line | M4 | Tested | Same as F13-05 Line draw mode |
| F17-02 | Shapes | arrow | M4 | Tested | Same as F13-06 Arrow draw mode |
| F17-03 | Shapes | rectangle | M4 | Tested | Same as F13-07 Rect draw mode |
| F17-04 | Shapes | rounded rectangle | M4 | Tested | Same as F13-08 Round draw mode |
| F17-05 | Shapes | ellipse | M4 | Tested | Same as F13-09 Ellipse draw mode |
| F17-06 | Shapes | polygon | M4 | Tested | `AddPolygonAsync` click vertices / Enter to close; Freeform remains freehand |
| F17-07 | Shapes | star | M4 | Tested | `AddShapeAsync(Star)` closed ink path + Star draw mode |
| F17-08 | Shapes | speech bubble/callout | M4 | Tested | `AddShapeAsync(SpeechBubble)` closed ink path + Bubble toolbar |
| F17-09 | Shapes | translucent highlight rectangle | M4 | Tested | `AddShapeAsync(HighlightRectangle)` + Area toolbar; translucent fill |
| F17-10 | Shapes | magnification/loupe annotation, optionally | M4 | Tested | `AddShapeAsync(Loupe)` Circle + Contents=Loupe; Loupe toolbar; select shows 3× crop popup |
| F17-11 | Shapes | resize | M4 | Tested | Same as F13-20 selection handles → `MoveAsync` |
| F17-12 | Shapes | rotate | M4 | Tested | Ink shapes/lines/arrows via stroke point rotation; square/circle via bounds |
| F17-13 | Shapes | move | M4 | Tested | Same as F13-19 drag / `MoveAsync` |
| F17-14 | Shapes | duplicate | M4 | Tested | Same as F13-22 `DuplicateAsync` |
| F17-15 | Shapes | multi-select | M4 | Implemented | Same as F13-27 Ctrl+click / Extended list |
| F17-16 | Shapes | border color | M4 | Implemented | Stroke picker when entering Rect/Ellipse/Line/Arrow |
| F17-17 | Shapes | border width | M4 | Implemented | Width picker with stroke color dialog |
| F17-18 | Shapes | line style | M4 | Tested | Shape stroke dialog: Solid/Dashed/Dotted for Line/Arrow |
| F17-19 | Shapes | fill color | M4 | Implemented | Semi-transparent fill from stroke hue |
| F17-20 | Shapes | opacity | M4 | Implemented | Sidebar Opacity → `SetOpacityAsync` |
| F17-21 | Shapes | shape-specific adjustment handles | M4 | Tested | Line/Arrow show endpoint handles (`p0`/`p1`); `SetLineEndpointsAsync` + `GlyphLineEnds` |
| F18-01 | Freehand drawing | Mouse drawing. | M4 | Tested | Ink draw mode |
| F18-02 | Freehand drawing | Stroke color. | M4 | Implemented | Stroke picker when entering Ink mode |
| F18-03 | Freehand drawing | Stroke width. | M4 | Implemented | Width picker (1–8 pt) with color dialog |
| F18-04 | Freehand drawing | Stroke opacity. | M4 | Implemented | Sidebar Opacity → `SetOpacityAsync` on ink |
| F18-05 | Freehand drawing | Eraser. | M4 | Implemented | Eraser toolbar mode: click annotation to remove (ink preferred, padded hit) |
| F18-06 | Freehand drawing | Undo stroke. | M4 | Implemented | Ctrl+Z undoes last ink/freeform/polygon via `_strokeUndoStack` |
| F18-07 | Freehand drawing | Select/move completed strokes. | M4 | Implemented | Ink annots use selection tool + `MoveAsync` |
| F18-08 | Freehand drawing | Delete stroke. | M4 | Implemented | Sidebar Delete / `RemoveAsync` on ink annot |
| F18-09 | Freehand drawing | recognize rough: | M4 | Tested | `PdfStrokeShapeRecognizer` line/rect/ellipse/triangle |
| F18-10 | Freehand drawing | offer cleaned-up shape or original stroke. | M4 | Implemented | Dialog after ink/freeform when shape recognized |
| F19-01 | PDF signatures | mouse | M4 | Tested | Draw mode → BGRA raster → library + stamp |
| F19-02 | PDF signatures | webcam photographing a signature on paper | M4 | Implemented | Sign → Webcam (`MediaCapture` preview + capture); `SignaturePaperKeying` keys near-white paper; stamp + library |
| F19-03 | PDF signatures | imported transparent signature image | M4 | Tested | Import PNG/JPEG → BGRA stamp |
| F19-04 | PDF signatures | save signature | M4 | Tested | `FileSignatureLibrary.SaveAsync` |
| F19-05 | PDF signatures | name signature | M4 | Tested | Named on save |
| F19-06 | PDF signatures | delete signature | M4 | Tested | `DeleteAsync` |
| F19-07 | PDF signatures | reorder signatures | M4 | Implemented | Library dialog ↑/↓ → `ISignatureLibrary.ReorderAsync` |
| F19-08 | PDF signatures | local storage | M4 | Tested | `%LocalAppData%\Glyph\signatures` |
| F19-09 | PDF signatures | optional application-specific cloud sync later | M4 | Deferred | Explicitly later |
| F19-10 | PDF signatures | insert | M4 | Implemented | Sign toolbar |
| F19-11 | PDF signatures | resize | M4 | Tested | Bounds on insert / `MoveAsync` |
| F19-12 | PDF signatures | move | M4 | Tested | `MoveAsync` |
| F19-13 | PDF signatures | rotate where appropriate | M4 | Tested | Stamp rotate via BGRA 90° + bounds swap (`RotateAsync`) |
| F19-14 | PDF signatures | duplicate | M4 | Tested | Sidebar Dup / `DuplicateAsync` clones stamp image + offset bounds |
| F19-15 | PDF signatures | delete | M4 | Implemented | Sidebar Delete |
| F19-16 | PDF signatures | preserve transparency | M4 | Tested | BGRA alpha channel |
| F20-01 | PDF forms | text fields | M4 | Tested | `IPdfFormStore.SetTextValueAsync` + Form toolbar |
| F20-02 | PDF forms | multiline fields | M4 | Implemented | Same text path; AcceptsReturn in edit dialog |
| F20-03 | PDF forms | checkboxes | M4 | Tested | `SetCheckBoxAsync` sets `/V`+`/AS` |
| F20-04 | PDF forms | radio buttons | M4 | Tested | `SetRadioButtonAsync` mutual exclusion by `/T` |
| F20-05 | PDF forms | dropdowns | M4 | Tested | Combo `/Opt` via PdfPig + Form picker |
| F20-06 | PDF forms | combo boxes | M4 | Tested | `SetTextValueAsync` + option list UI |
| F20-07 | PDF forms | list boxes | M4 | Tested | `SetTextValueAsync` + `/Opt` picker |
| F20-08 | PDF forms | buttons where applicable | M4 | Tested | PushButton listed; URI `/A` via PdfPig; Form Overlay/Edit activates (Launcher) |
| F20-09 | PDF forms | signatures where supported | M4 | Implemented | Sig widgets listed; Form Overlay/Edit places stamp in field bounds (visual, not PKCS#7) |
| F20-10 | PDF forms | tab-order navigation | M4 | Tested | `FocusAdjacentAsync` + Form dialog Next |
| F20-11 | PDF forms | form-filling mode that overlays text fields manually. | M4 | Implemented | Form → Overlay draws clickable field boxes; Tab/Enter/Esc |
| F20-12 | PDF forms | automatic font sizing | M4 | Tested | `SetTextValueAsync` rewrites text `/DA` to `0 Tf`; `PdfFormDefaultAppearance` |
| F20-13 | PDF forms | remember recently entered values, optionally | M4 | Tested | `IFormValueHistory` JSON store; text edit dialog Recent values list |
| F20-14 | PDF forms | user-defined profile for: | M4 | Tested | Name/Address/Email/Phone via `IFormAutofillProfileStore` |
| F20-15 | PDF forms | optional AutoFill from that application profile | M4 | Tested | Form → AutoFill matches field names; skips non-empty |
| F21-01 | Redaction | Mark text for redaction. | M7 | Implemented | Context menu / Redact → Mark selection → `MarkTextRegion` |
| F21-02 | Redaction | Mark rectangular areas for redaction. | M7 | Implemented | Redact draw mode + region mark → `MarkRectangle` |
| F21-03 | Redaction | Preview pending redactions. | M7 | Implemented | Pending marks drawn on dedicated overlay canvas |
| F21-04 | Redaction | Remove pending redaction. | M7 | Implemented | Click pending mark to remove; Apply dialog Clear marks; `RemovePending` API|
| F21-05 | Redaction | Apply redactions permanently. | M7 | Implemented | `ApplyAsync` with confirm dialog |
| F21-06 | Redaction | Remove underlying text/image data, not merely cover it visually. | M7 | Tested | Black page object + remove intersecting text/images; search empty after apply |
| F21-07 | Redaction | Warn before permanent application. | M7 | Implemented | ContentDialog confirm before `ApplyAsync` |
| F21-08 | Redaction | Sanitize associated hidden content where practical. | M7 | Tested | Apply removes intersecting annotations via `RemoveIntersectingAnnotations` |
| F21-09 | Redaction | Option to remove: | M7 | Tested | Apply dialog checkboxes: intersecting annotations, embedded attachments, Info metadata (Info patcher). Layers N/A (no OCG API). |
| F21-10 | Redaction | Search and redact matching text, later/advanced. | M7 | Implemented | Redact → Mark find matches from current Find query |
| F22-01 | PDF annotation flattening | highlights | M4 | Tested | `FlattenAsync` via `FPDFPage_Flatten` |
| F22-02 | PDF annotation flattening | notes as configured | M4 | Tested | Same flatten path |
| F22-03 | PDF annotation flattening | shapes | M4 | Tested | Same flatten path |
| F22-04 | PDF annotation flattening | signatures | M4 | Tested | Stamp annots included in `FlattenAsync` |
| F22-05 | PDF annotation flattening | text boxes | M4 | Tested | Same flatten path |
| F22-06 | PDF annotation flattening | drawings | M4 | Tested | Ink strokes included in flatten |
| F23-01 | PDF security | Open encrypted PDFs. | M7 | Tested | Open with password prompt via `OpenPdfWithPasswordAsync` / fixture test||
| F23-02 | PDF security | Create password-protected PDFs. | M7 | Blocked | Needs ADR-015 (PDFium has no write-encrypt API) |
| F23-03 | PDF security | Set document-open password. | M7 | Blocked | Needs ADR-015 |
| F23-04 | PDF security | Set permissions/owner password where PDF standard permits. | M7 | Blocked | Needs ADR-015 |
| F23-05 | PDF security | Restrict: printing/editing/copying/annotation/page extraction | M7 | Blocked | Needs ADR-015 (permission write) |
| F23-06 | PDF security | Change existing permissions where credentials permit. | M7 | Blocked | Needs ADR-015 |
| F23-07 | PDF security | Remove protection when authorized. | M7 | Blocked | Needs ADR-015 |
| F23-08 | PDF security | Display encryption information. | M7 | Implemented | Info dialog + status "Encrypted"; `IPdfDocumentInfoService` permissions||
| F23-09 | PDF security | Warn about limitations of PDF permission enforcement. | M7 | Implemented | Info dialog notes PDF permission flags are advisory||
| F24-01 | PDF optimization and compression | Lossless | M7 | Implemented | Optimize dialog preset — full rewrite via `FPDF_NO_INCREMENTAL` |
| F24-02 | PDF optimization and compression | High quality | M7 | Implemented | Optimize preset → 200 DPI target when above 300 |
| F24-03 | PDF optimization and compression | Balanced | M7 | Tested | Optimize preset → 150 DPI; service tests |
| F24-04 | PDF optimization and compression | Small file | M7 | Tested | Optimize preset → 96 DPI + strip attachments |
| F24-05 | PDF optimization and compression | Custom | M7 | Implemented | Optimize dialog Custom: above/target DPI, strip attachments, preserve mono |
| F24-06 | PDF optimization and compression | downsample images above selected DPI | M7 | Tested | `DownsampleAboveDpi` + PDFium `SetBitmap` resize |
| F24-07 | PDF optimization and compression | target DPI | M7 | Tested | `TargetDpi` on presets |
| F24-08 | PDF optimization and compression | JPEG quality | M7 | Blocked | `IPdfImageJpegEncoder`/Magick wired; `LoadJpegFileInline` FILEACCESS crashes under PDFiumCore — SetBitmap fallback |
| F24-09 | PDF optimization and compression | preserve monochrome content | M7 | Implemented | Skip 1-bpp images when PreserveMonochrome |
| F24-10 | PDF optimization and compression | compress streams | M7 | Implemented | Optimize measures/saves with `FPDF_NO_INCREMENTAL` full rewrite |
| F24-11 | PDF optimization and compression | subset fonts where appropriate | M7 | Deferred | ADR-016 — no PDFium font-subset API |
| F24-12 | PDF optimization and compression | remove unused objects | M7 | Implemented | Best-effort via `FPDF_NO_INCREMENTAL` full rewrite after edits (ADR-016) |
| F24-13 | PDF optimization and compression | optimize object structure | M7 | Implemented | Same full-rewrite path as F24-12 (ADR-016) |
| F24-14 | PDF optimization and compression | linearize / Fast Web View | M7 | Deferred | ADR-016 — no PDFium linearize flag |
| F24-15 | PDF optimization and compression | remove metadata optionally | M7 | Tested | Custom Optimize → Remove metadata via Info dict patcher |
| F24-16 | PDF optimization and compression | estimate output size before saving where feasible | M7 | Tested | Optimize → Estimate via SaveToBytes + eligible image heuristic |
| F25-01 | PDF metadata | title | M7 | Implemented | Info dialog Title from `FPDF_GetMetaText`||
| F25-02 | PDF metadata | author | M7 | Implemented | Info dialog Author||
| F25-03 | PDF metadata | subject | M7 | Implemented | Info dialog Subject||
| F25-04 | PDF metadata | keywords | M7 | Implemented | Info dialog Keywords||
| F25-05 | PDF metadata | creator | M7 | Implemented | Info dialog Creator||
| F25-06 | PDF metadata | producer | M7 | Implemented | Info dialog Producer||
| F25-07 | PDF metadata | created date | M7 | Implemented | Info dialog CreationDate||
| F25-08 | PDF metadata | modified date | M7 | Implemented | Info dialog ModDate||
| F25-09 | PDF metadata | page count | M7 | Implemented | Info dialog page count||
| F25-10 | PDF metadata | PDF version | M7 | Tested | Info dialog `%PDF-x.y` header via `ReadPdfVersion` |
| F25-11 | PDF metadata | page dimensions | M7 | Tested | Info dialog page 0 width×height in points |
| F25-12 | PDF metadata | file size | M7 | Implemented | Info dialog file size||
| F25-13 | PDF metadata | encryption status | M7 | Implemented | Info dialog + status encryption flag||
| F25-14 | PDF metadata | permissions | M7 | Implemented | Info dialog decoded permission flags||
| F25-15 | PDF metadata | embedded fonts, optionally | M7 | Tested | Info dialog lists fonts from page text objects (first 32 pages) |
| F25-16 | PDF metadata | title | M7 | Tested | Info → Edit… writes `/Title` via incremental Info patch |
| F25-17 | PDF metadata | author | M7 | Tested | Info → Edit… `/Author` |
| F25-18 | PDF metadata | subject | M7 | Tested | Info → Edit… `/Subject` |
| F25-19 | PDF metadata | keywords | M7 | Tested | Info → Edit… `/Keywords` |
| F26-01 | Image viewing | JPEG/JPG | M5 | Implemented | Magick.NET decoder |
| F26-02 | Image viewing | PNG | M5 | Implemented | Magick.NET decoder |
| F26-03 | Image viewing | GIF | M5 | Implemented | Magick.NET decoder (first frame) |
| F26-04 | Image viewing | BMP | M5 | Implemented | Magick.NET decoder |
| F26-05 | Image viewing | TIFF | M5 | Implemented | Magick.NET decoder |
| F26-06 | Image viewing | WebP | M5 | Implemented | Magick.NET decoder |
| F26-07 | Image viewing | HEIF/HEIC where codecs are available | M5 | Tested | Magick.NET opens `.heic`/`.heif` when delegates present; Open picker + folder nav |
| F26-08 | Image viewing | AVIF where practical | M5 | Tested | Magick.NET decode; Open picker + `MagickImageDecoderFormatTests` |
| F26-09 | Image viewing | ICO | M5 | Tested | Magick.NET decoder + open picker |
| F26-10 | Image viewing | JPEG 2000 where practical | M5 | Tested | Magick.NET decode `.jp2`/`.j2k`; Open picker + decoder tests |
| F26-11 | Image viewing | fast opening | M5 | Implemented | `GetPixelsAsync(maxEdge)` caps decode (256–8192) so large files open without full decode |
| F26-12 | Image viewing | zoom | M5 | Implemented | ImageDocumentView ± zoom |
| F26-13 | Image viewing | pan | M5 | Implemented | ScrollViewer pan |
| F26-14 | Image viewing | fit image | M5 | Implemented | Fit toolbar |
| F26-15 | Image viewing | actual size | M5 | Implemented | 100% toolbar |
| F26-16 | Image viewing | fullscreen | M5 | Implemented | Fullscreen toolbar → MainWindow.ToggleFullscreen |
| F26-17 | Image viewing | next/previous image | M5 | Implemented | ◀/▶ + ImageFolderNavigator |
| F26-18 | Image viewing | image list sidebar | M5 | Implemented | Folder ListView in ImageDocumentView |
| F26-19 | Image viewing | open group of images together | M5 | Implemented | Open With picker PickMultipleFilesAsync |
| F26-20 | Image viewing | slideshow | M5 | Implemented | Slideshow toolbar: 3s loop through folder; Esc / Stop show; resumes across sibling opens via `ViewState.IsSlideshowActive` |
| F26-21 | Image viewing | drag-and-drop navigation | M5 | Implemented | Shell drop opens images (`DropHost`); horizontal swipe on image → prev/next in folder |
| F26-22 | Image viewing | high-resolution image support | M5 | Implemented | Same maxEdge progressive decode; zoom re-decodes up to 8192 edge |
| F26-23 | Image viewing | alpha transparency | M5 | Implemented | BGRA32 decode via Magick → WriteableBitmap |
| F26-24 | Image viewing | HDR display where Windows/display stack supports it | M5 | Not Started |  |
| F26-25 | Image viewing | color-managed display | M5 | Not Started |  |
| F27-01 | Animated images | play | M8 | Not Started |  |
| F27-02 | Animated images | pause | M8 | Not Started |  |
| F27-03 | Animated images | restart | M8 | Not Started |  |
| F27-04 | Animated images | next frame | M8 | Not Started |  |
| F27-05 | Animated images | previous frame | M8 | Not Started |  |
| F27-06 | Animated images | timeline/frame number | M8 | Not Started |  |
| F27-07 | Animated images | loop | M8 | Not Started |  |
| F27-08 | Animated images | inspect individual frames | M8 | Not Started |  |
| F27-09 | Animated images | extract frame | M8 | Not Started |  |
| F27-10 | Animated images | save selected frame as image | M8 | Not Started |  |
| F28-01 | Image selection tools | Rectangular selection. | M5 | Implemented | Select toolbar → drag rectangle overlay |
| F28-02 | Image selection tools | Elliptical selection. | M5 | Tested | Select → Ellipse shape; extract/clear/move use oval mask |
| F28-03 | Image selection tools | Freeform lasso. | M5 | Not Started |  |
| F28-04 | Image selection tools | Smart lasso. | M5 | Not Started |  |
| F28-05 | Image selection tools | Select all. | M5 | Implemented | Select → All / Ctrl+A |
| F28-06 | Image selection tools | Invert selection. | M5 | Not Started |  |
| F28-07 | Image selection tools | Deselect. | M5 | Implemented | Deselect / Esc |
| F28-08 | Image selection tools | Move selected pixels. | M5 | Tested | Drag inside selection / arrow keys → `MoveRectAsync` |
| F28-09 | Image selection tools | Copy. | M5 | Implemented | Copy sel / Ctrl+C → clipboard PNG via `ExtractRectAsync` |
| F28-10 | Image selection tools | Cut. | M5 | Implemented | Cut sel / Ctrl+X → copy + `ClearRectAsync` |
| F28-11 | Image selection tools | Paste. | M5 | Tested | Paste / Ctrl+V → `PasteRectAsync` at selection origin |
| F28-12 | Image selection tools | Delete selection. | M5 | Tested | Del sel → `ClearRectAsync` transparent |
| F28-13 | Image selection tools | Crop to selection. | M5 | Implemented | Crop sel → `CropAsync` |
| F29-01 | Smart object/background selection | Smart Lasso. | M8 | Not Started |  |
| F29-02 | Smart object/background selection | Edge-aware selection. | M8 | Not Started |  |
| F29-03 | Smart object/background selection | Background removal. | M8 | Not Started |  |
| F29-04 | Smart object/background selection | Subject extraction. | M8 | Not Started |  |
| F29-05 | Smart object/background selection | Automatic subject detection. | M8 | Not Started |  |
| F29-06 | Smart object/background selection | Remove background. | M8 | Not Started |  |
| F29-07 | Smart object/background selection | Preserve transparent background. | M8 | Not Started |  |
| F29-08 | Smart object/background selection | Offer conversion to transparency-capable format if source format cannot support alpha. | M8 | Not Started |  |
| F29-09 | Smart object/background selection | Undo. | M8 | Not Started |  |
| F29-10 | Smart object/background selection | Copy extracted subject. | M8 | Not Started |  |
| F29-11 | Smart object/background selection | Save extracted subject as separate image. | M8 | Not Started |  |
| F30-01 | Image crop | Interactive crop box. | M5 | Implemented | Drag rectangle overlay (Crop…) |
| F30-02 | Image crop | Free aspect ratio. | M5 | Implemented | Free drag selection |
| F30-03 | Image crop | Original aspect ratio. | M5 | Tested | Crop… aspect dropdown → Original (`ImageCropAspect`) |
| F30-04 | Image crop | Common presets: | M5 | Tested | Crop… aspect: 1:1, 4:3, 3:2, 16:9 (+ Free/Original) |
| F30-05 | Image crop | Numeric width/height. | M5 | Implemented | Crop x,y,w,h text box |
| F30-06 | Image crop | Apply crop. | M5 | Implemented | Crop / Apply crop → MagickImageProcessor.CropAsync |
| F30-07 | Image crop | Undo. | M5 | Tested | Undo / Ctrl+Z via `CaptureCheckpoint`/`RestoreCheckpoint` (crop and other edits) |
| F30-08 | Image crop | Non-destructive editing internally until save where practical. | M5 | Implemented | Edits mutate in-memory Magick image; disk unchanged until Save |
| F31-01 | Image resizing | Adjust width. | M5 | Implemented | Resize dialog width (px) |
| F31-02 | Image resizing | Adjust height. | M5 | Implemented | Resize dialog height (px) |
| F31-03 | Image resizing | Lock aspect ratio. | M5 | Implemented | Resize dialog lock checkbox |
| F31-04 | Image resizing | Percentage scaling. | M5 | Implemented | Resize dialog scale % |
| F31-05 | Image resizing | Pixel units. | M5 | Implemented | Width/height in pixels |
| F31-06 | Image resizing | Physical units. | M5 | Tested | Resize dialog Units: Pixels / Inches / Centimeters |
| F31-07 | Image resizing | DPI/PPI. | M5 | Tested | Resize dialog DPI + `ImageResizeOptions.DensityDpi` |
| F31-08 | Image resizing | Resampling toggle. | M5 | Tested | Resize dialog Resampling combo (Auto/Nearest/Bilinear/Bicubic) |
| F31-09 | Image resizing | Resampling algorithm options, possibly: | M5 | Tested | Nearest / Bilinear / Bicubic via Magick FilterType |
| F31-10 | Image resizing | Preserve aspect ratio. | M5 | Implemented | Same as lock aspect |
| F31-11 | Image resizing | Estimated resulting dimensions. | M5 | Implemented | Live result preview in dialog |
| F31-12 | Image resizing | Estimated file size. | M5 | Implemented | Preview shows ~raw BGRA MB estimate |
| F31-13 | Image resizing | Batch resize selected images. | M5 | Not Started |  |
| F32-01 | Image orientation | Rotate left 90°. | M5 | Tested | MagickImageProcessor.RotateAsync |
| F32-02 | Image orientation | Rotate right 90°. | M5 | Tested | ImageDocumentView ⟳ + RotateAsync(90) |
| F32-03 | Image orientation | Rotate 180°. | M5 | Tested | ImageDocumentView 180° + RotateAsync(180) |
| F32-04 | Image orientation | Flip horizontal. | M5 | Tested | Flip H + FlipHorizontalAsync |
| F32-05 | Image orientation | Flip vertical. | M5 | Tested | Flip V + FlipVerticalAsync |
| F32-06 | Image orientation | Batch operations on selected images. | M5 | Not Started |  |
| F32-07 | Image orientation | Respect EXIF orientation. | M5 | Tested | MagickImageDecoder AutoOrient on open |
| F32-08 | Image orientation | Option to normalize EXIF orientation into pixels. | M5 | Tested | Orient toolbar + NormalizeOrientationAsync |
| F33-01 | Image color adjustments | Auto Levels | M5 | Tested | Adjust dialog → `AutoLevels` → Magick `AutoLevel` |
| F33-02 | Image color adjustments | Exposure | M5 | Implemented | Adjust dialog Brightness (Magick BrightnessContrast) |
| F33-03 | Image color adjustments | Contrast | M5 | Tested | Adjust dialog → AdjustAsync Contrast |
| F33-04 | Image color adjustments | Highlights | M5 | Tested | Adjust dialog Highlights → tone CLUT |
| F33-05 | Image color adjustments | Shadows | M5 | Tested | Adjust dialog Shadows → tone CLUT |
| F33-06 | Image color adjustments | Saturation | M5 | Tested | Adjust dialog → AdjustAsync Saturation |
| F33-07 | Image color adjustments | Temperature | M5 | Tested | Adjust dialog Temperature → ColorMatrix RGB gain |
| F33-08 | Image color adjustments | Tint | M5 | Tested | Adjust dialog Tint → ColorMatrix green/magenta |
| F33-09 | Image color adjustments | Sharpness | M5 | Tested | Adjust dialog Sharpness → Magick `Sharpen` |
| F33-10 | Image color adjustments | Sepia | M5 | Tested | Adjust dialog Sepia → Magick `SepiaTone` |
| F33-11 | Image color adjustments | Black point / levels | M5 | Not Started |  |
| F33-12 | Image color adjustments | Gamma where useful | M5 | Not Started |  |
| F33-13 | Image color adjustments | Reset individual adjustment | M5 | Not Started |  |
| F33-14 | Image color adjustments | Reset all | M5 | Implemented | Adjust dialog Reset |
| F33-15 | Image color adjustments | live preview | M5 | Not Started |  |
| F33-16 | Image color adjustments | histogram | M5 | Not Started |  |
| F34-01 | Image markup | mouse drawing | M5 | Tested | Draw toolbar → non-destructive overlay; Flatten / Save bakes via `FlattenMarkupAsync` |
| F34-02 | Image markup | shapes | M5 | Tested | Draw → Rectangle/Ellipse overlay; FlattenMarkupAsync |
| F34-03 | Image markup | arrows | M5 | Tested | Draw → Line/Arrow overlay with head wings |
| F34-04 | Image markup | text | M5 | Not Started |  |
| F34-05 | Image markup | callouts | M5 | Not Started |  |
| F34-06 | Image markup | signatures | M5 | Not Started |  |
| F34-07 | Image markup | selection | M5 | Implemented | Reuses Select / F28 tools while drawing remains overlay |
| F34-08 | Image markup | crop | M5 | Implemented | Reuses Crop… / Crop sel |
| F34-09 | Image markup | rotate | M5 | Implemented | Reuses rotate L/R/180 toolbar |
| F34-10 | Image markup | Save/export to a flat image format, or | M5 | Implemented | Save/Export prompts to flatten pending strokes |
| F34-11 | Image markup | user explicitly flattens. | M5 | Tested | Flatten toolbar → `FlattenMarkupAsync` |
| F35-01 | Image format conversion | PNG | M5 | Tested | MagickImageEncoder → PNG |
| F35-02 | Image format conversion | JPEG | M5 | Tested | →JPEG toolbar + SaveAsAsync |
| F35-03 | Image format conversion | WebP | M5 | Tested | Convert dialog → WebP |
| F35-04 | Image format conversion | TIFF | M5 | Tested | Convert dialog → TIFF |
| F35-05 | Image format conversion | BMP | M5 | Tested | Convert dialog → BMP |
| F35-06 | Image format conversion | GIF | M5 | Tested | Convert dialog → GIF (first frame) |
| F35-07 | Image format conversion | HEIC/HEIF where supported | M5 | Tested | Convert → HEIC (`ImageEncodeFormat.Heic`); skips when Magick lacks codec |
| F35-08 | Image format conversion | AVIF | M5 | Tested | Convert dialog → AVIF + quality |
| F35-09 | Image format conversion | JPEG 2000 | M5 | Tested | Convert dialog → JPEG 2000 |
| F35-10 | Image format conversion | PDF | M5 | Not Started |  |
| F35-11 | Image format conversion | JPEG quality | M5 | Tested | ImageEncodeOptions.Quality + →JPEG dialog |
| F35-12 | Image format conversion | WebP quality/lossless | M5 | Tested | Convert dialog Quality / Lossless WebP |
| F35-13 | Image format conversion | AVIF quality | M5 | Tested | Convert dialog Quality slider for AVIF |
| F35-14 | Image format conversion | TIFF compression | M5 | Not Started |  |
| F35-15 | Image format conversion | preserve/remove alpha | M5 | Not Started |  |
| F35-16 | Image format conversion | preserve/remove metadata | M5 | Not Started |  |
| F35-17 | Image format conversion | color profile handling | M5 | Not Started |  |
| F36-01 | Batch image operations | resize | M8 | Not Started |  |
| F36-02 | Batch image operations | rotate | M8 | Not Started |  |
| F36-03 | Batch image operations | flip | M8 | Not Started |  |
| F36-04 | Batch image operations | convert format | M8 | Not Started |  |
| F36-05 | Batch image operations | export | M8 | Not Started |  |
| F36-06 | Batch image operations | strip metadata | M8 | Not Started |  |
| F36-07 | Batch image operations | change color profile | M8 | Not Started |  |
| F36-08 | Batch image operations | rename, optionally | M8 | Not Started |  |
| F37-01 | Image metadata | dimensions | M5 | Tested | GetMetadataAsync PixelWidth/Height |
| F37-02 | Image metadata | pixel count | M5 | Implemented | Derived from dimensions |
| F37-03 | Image metadata | DPI | M5 | Tested | Density → DpiX/DpiY |
| F37-04 | Image metadata | bit depth | M5 | Implemented | Magick Depth |
| F37-05 | Image metadata | color space | M5 | Implemented | Magick ColorSpace |
| F37-06 | Image metadata | ICC profile | M5 | Implemented | HasIccProfile flag |
| F37-07 | Image metadata | file format | M5 | Implemented | FormatName |
| F37-08 | Image metadata | compression | M5 | Implemented | Magick Compression |
| F37-09 | Image metadata | file size | M5 | Tested | FileSizeBytes from path |
| F37-10 | Image metadata | camera make/model | M5 | Tested | EXIF Make/Model |
| F37-11 | Image metadata | lens information | M5 | Implemented | EXIF LensModel when present |
| F37-12 | Image metadata | exposure | M5 | Tested | EXIF ExposureTime |
| F37-13 | Image metadata | aperture | M5 | Tested | EXIF FNumber |
| F37-14 | Image metadata | ISO | M5 | Tested | EXIF ISOSpeedRatings |
| F37-15 | Image metadata | focal length | M5 | Tested | EXIF FocalLength |
| F37-16 | Image metadata | capture date | M5 | Tested | EXIF DateTimeOriginal |
| F37-17 | Image metadata | orientation | M5 | Implemented | EXIF Orientation |
| F37-18 | Image metadata | GPS coordinates | M5 | Tested | GPSLatitude/Longitude |
| F37-19 | Image metadata | EXIF | M5 | Tested | Magick ExifProfile |
| F37-20 | Image metadata | IPTC | M5 | Tested | Magick IptcProfile → Title/Caption/Keywords/Copyright |
| F37-21 | Image metadata | XMP where available | M5 | Implemented | Magick XmpProfile presence + dc:title/description/subject/rights/Rating |
| F37-22 | Image metadata | title | M5 | Tested | IPTC Title/Headline, else XMP/EXIF |
| F37-23 | Image metadata | description | M5 | Tested | IPTC Caption, else XMP/EXIF ImageDescription |
| F37-24 | Image metadata | keywords | M5 | Tested | IPTC Keyword list / XMP subject |
| F37-25 | Image metadata | copyright | M5 | Tested | IPTC CopyrightNotice / XMP rights / EXIF Copyright |
| F37-26 | Image metadata | rating, optionally | M5 | Implemented | XMP Rating when present |
| F38-01 | GPS metadata | display latitude/longitude. | M5/M8 | Tested | Meta dialog GPS rows |
| F38-02 | GPS metadata | Copy coordinates. | M5/M8 | Implemented | Copy GPS button |
| F38-03 | GPS metadata | Open in default/browser mapping service. | M5/M8 | Implemented | Open map → OpenStreetMap |
| F38-04 | GPS metadata | Remove GPS metadata. | M5/M8 | Tested | RemoveGpsMetadataAsync |
| F38-05 | GPS metadata | Optional embedded map later. | M5/M8 | Not Started |  |
| F39-01 | Color management | Detect embedded ICC profile. | M8 | Not Started |  |
| F39-02 | Color management | Honor embedded profile while displaying. | M8 | Not Started |  |
| F39-03 | Color management | Assign ICC profile. | M8 | Not Started |  |
| F39-04 | Color management | Convert between profiles. | M8 | Not Started |  |
| F39-05 | Color management | Use monitor profile. | M8 | Not Started |  |
| F39-06 | Color management | Soft-proof through another ICC profile. | M8 | Not Started |  |
| F39-07 | Color management | Toggle soft proof. | M8 | Not Started |  |
| F39-08 | Color management | Gamut-warning option, advanced. | M8 | Not Started |  |
| F39-09 | Color management | Rendering intent selection: | M8 | Not Started |  |
| F40-01 | Clipboard integration | PDF text → text | M1/M5 | Not Started |  |
| F40-02 | Clipboard integration | PDF region → bitmap | M1/M5 | Not Started |  |
| F40-03 | Clipboard integration | image selection → image | M1/M5 | Not Started |  |
| F40-04 | Clipboard integration | whole image → image | M1/M5 | Not Started |  |
| F40-05 | Clipboard integration | recognized OCR text → text | M1/M5 | Not Started |  |
| F40-06 | Clipboard integration | annotation where possible | M1/M5 | Not Started |  |
| F40-07 | Clipboard integration | image from clipboard into image document | M1/M5 | Not Started |  |
| F40-08 | Clipboard integration | image clipboard → create new image | M1/M5 | In Progress | New from Clipboard creates image document |
| F40-09 | Clipboard integration | text into annotation/text field | M1/M5 | Not Started |  |
| F40-10 | Clipboard integration | file paths where appropriate | M1/M5 | Not Started |  |
| F41-00 | Screenshot workflow | (see FEATURES.md §41) | M1/M5 | Not Started | |
| F42-01 | Scanner support | Discover connected scanners. | M8 | Not Started |  |
| F42-02 | Scanner support | Flatbed scanner. | M8 | Not Started |  |
| F42-03 | Scanner support | Automatic document feeder. | M8 | Not Started |  |
| F42-04 | Scanner support | Duplex feeder. | M8 | Not Started |  |
| F42-05 | Scanner support | Color. | M8 | Not Started |  |
| F42-06 | Scanner support | Grayscale. | M8 | Not Started |  |
| F42-07 | Scanner support | Black and white. | M8 | Not Started |  |
| F42-08 | Scanner support | Resolution/DPI. | M8 | Not Started |  |
| F42-09 | Scanner support | Paper size. | M8 | Not Started |  |
| F42-10 | Scanner support | Auto crop. | M8 | Not Started |  |
| F42-11 | Scanner support | Auto straighten. | M8 | Not Started |  |
| F42-12 | Scanner support | Brightness/contrast where hardware supports it. | M8 | Not Started |  |
| F42-13 | Scanner support | Scan one page. | M8 | Not Started |  |
| F42-14 | Scanner support | Scan multiple pages. | M8 | Not Started |  |
| F42-15 | Scanner support | Scan directly into new PDF. | M8 | Not Started |  |
| F42-16 | Scanner support | Insert scanned pages into existing PDF. | M8 | Not Started |  |
| F42-17 | Scanner support | Scan multiple photos separately from a flatbed where detection is practical. | M8 | Not Started |  |
| F43-01 | Webcam/camera import | select webcam | M8 | Not Started |  |
| F43-02 | Webcam/camera import | capture image | M8 | Not Started |  |
| F43-03 | Webcam/camera import | crop result | M8 | Not Started |  |
| F43-04 | Webcam/camera import | insert into document | M8 | Not Started |  |
| F44-01 | Printing | print current page | M8 | Not Started |  |
| F44-02 | Printing | print selected pages | M8 | Not Started |  |
| F44-03 | Printing | print page range | M8 | Not Started |  |
| F44-04 | Printing | print all pages | M8 | Not Started |  |
| F44-05 | Printing | print selected images | M8 | Not Started |  |
| F44-06 | Printing | copies | M8 | Not Started |  |
| F44-07 | Printing | collate | M8 | Not Started |  |
| F44-08 | Printing | duplex | M8 | Not Started |  |
| F44-09 | Printing | printer selection | M8 | Not Started |  |
| F44-10 | Printing | paper size | M8 | Not Started |  |
| F44-11 | Printing | orientation | M8 | Not Started |  |
| F44-12 | Printing | margins | M8 | Not Started |  |
| F44-13 | Printing | scale | M8 | Not Started |  |
| F44-14 | Printing | actual size | M8 | Not Started |  |
| F44-15 | Printing | fit to printable area | M8 | Not Started |  |
| F44-16 | Printing | fill page | M8 | Not Started |  |
| F44-17 | Printing | pages per sheet | M8 | Not Started |  |
| F44-18 | Printing | auto rotate | M8 | Not Started |  |
| F44-19 | Printing | center | M8 | Not Started |  |
| F44-20 | Printing | print annotations | M8 | Not Started |  |
| F44-21 | Printing | print notes optionally | M8 | Not Started |  |
| F44-22 | Printing | grayscale | M8 | Not Started |  |
| F44-23 | Printing | Windows printer properties integration | M8 | Not Started |  |
| F45-01 | Exporting | output format | M5-M9 | Implemented | PDF Export → PNG/JPEG/WebP/TIFF/BMP; image Convert/Export |
| F45-02 | Exporting | destination | M5-M9 | Implemented | FileSavePicker / FolderPicker for multi-page |
| F45-03 | Exporting | quality | M5-M9 | Implemented | JPEG/WebP quality slider on PDF Export + image JPEG export |
| F45-04 | Exporting | compression | M5-M9 | Implemented | WebP lossless option; codec defaults for PNG/JPEG/AVIF |
| F45-05 | Exporting | dimensions | M5-M9 | Implemented | PDF Export render DPI control |
| F45-06 | Exporting | metadata preservation | M5-M9 | Tested | PDF Info Title/Author → image Title/Artist on page export |
| F45-07 | Exporting | color profile | M5-M9 | Tested | PDF page Export embeds sRGB ICC (`EmbedSrgbProfile`; PNG `preserve-iCCP`); JP2 may drop profile |
| F45-08 | Exporting | transparency | M5-M9 | Implemented | PNG/WebP/TIFF/AVIF keep render alpha; JPEG/JP2/BMP/GIF flatten |
| F45-09 | Exporting | PDF security | M5-M9 | Blocked | Needs ADR-015 password-write |
| F45-10 | Exporting | annotation flattening | M5-M9 | Implemented | Raster page export renders annotations into pixels |
| F46-01 | Sharing and Windows integration | Windows Share UI where available | M9 | Not Started |  |
| F46-02 | Sharing and Windows integration | Open containing folder | M9 | Not Started |  |
| F46-03 | Sharing and Windows integration | Copy file path | M9 | Not Started |  |
| F46-04 | Sharing and Windows integration | Copy file | M9 | Not Started |  |
| F46-05 | Sharing and Windows integration | Send to default email workflow where practical | M9 | Not Started |  |
| F46-06 | Sharing and Windows integration | Nearby Share through Windows system facilities where available rather than custom implementation | M9 | Not Started |  |
| F46-07 | Sharing and Windows integration | OneDrive works naturally because files are ordinary filesystem objects | M9 | Not Started |  |
| F47-01 | External application integration | Open With... | M9 | Not Started |  |
| F47-02 | External application integration | Show in File Explorer | M9 | Not Started |  |
| F47-03 | External application integration | Open URL | M9 | Not Started |  |
| F47-04 | External application integration | Open location in browser/maps | M9 | Not Started |  |
| F47-05 | External application integration | Send via default mail application where possible | M9 | Not Started |  |
| F48-01 | File properties and inspector | dimensions | M5/M9 | Not Started |  |
| F48-02 | File properties and inspector | pages | M5/M9 | Not Started |  |
| F48-03 | File properties and inspector | metadata | M5/M9 | Not Started |  |
| F48-04 | File properties and inspector | security | M5/M9 | Not Started |  |
| F48-05 | File properties and inspector | fonts | M5/M9 | Not Started |  |
| F48-06 | File properties and inspector | annotations | M5/M9 | Not Started |  |
| F48-07 | File properties and inspector | file size | M5/M9 | Not Started |  |
| F48-08 | File properties and inspector | dimensions | M5/M9 | Not Started |  |
| F48-09 | File properties and inspector | color profile | M5/M9 | Not Started |  |
| F48-10 | File properties and inspector | metadata | M5/M9 | Not Started |  |
| F48-11 | File properties and inspector | EXIF | M5/M9 | Not Started |  |
| F48-12 | File properties and inspector | GPS | M5/M9 | Not Started |  |
| F48-13 | File properties and inspector | file size | M5/M9 | Not Started |  |
| F49-01 | Undo and redo | annotations | M1-M4 | Not Started |  |
| F49-02 | Undo and redo | drawing | M1-M4 | Not Started |  |
| F49-03 | Undo and redo | page insertion | M1-M4 | Not Started |  |
| F49-04 | Undo and redo | page deletion | M1-M4 | Not Started |  |
| F49-05 | Undo and redo | page ordering | M1-M4 | Not Started |  |
| F49-06 | Undo and redo | page rotation | M1-M4 | Not Started |  |
| F49-07 | Undo and redo | crop | M1-M4 | Not Started |  |
| F49-08 | Undo and redo | resizing | M1-M4 | Not Started |  |
| F49-09 | Undo and redo | image adjustments | M1-M4 | Not Started |  |
| F49-10 | Undo and redo | metadata editing | M1-M4 | Not Started |  |
| F49-11 | Undo and redo | form filling | M1-M4 | Not Started |  |
| F49-12 | Undo and redo | signature placement | M1-M4 | Not Started |  |
| F49-13 | Undo and redo | redaction before permanent application | M1-M4 | Not Started |  |
| F49-14 | Undo and redo | Ctrl+Z | M1-M4 | Not Started |  |
| F49-15 | Undo and redo | Ctrl+Y / Ctrl+Shift+Z | M1-M4 | Not Started |  |
| F50-01 | Autosave and recovery | edits remain in memory until Save. | M1/M9 | Not Started |  |
| F50-02 | Autosave and recovery | periodic crash-recovery snapshot. | M1/M9 | Not Started |  |
| F50-03 | Autosave and recovery | closing unsaved file prompts user. | M1/M9 | Not Started |  |
| F50-04 | Autosave and recovery | automatically save changes to original document. | M1/M9 | Not Started |  |
| F50-05 | Autosave and recovery | reopen recovered document after crash. | M1/M9 | Not Started |  |
| F50-06 | Autosave and recovery | never silently discard recovery data. | M1/M9 | Not Started |  |
| F50-07 | Autosave and recovery | remove recovery copy after successful save/close. | M1/M9 | Not Started |  |
| F51-01 | Optional version snapshots | optional automatic local snapshots. | M9 | Not Started |  |
| F51-02 | Optional version snapshots | show: | M9 | Not Started |  |
| F51-03 | Optional version snapshots | restore snapshot. | M9 | Not Started |  |
| F51-04 | Optional version snapshots | open snapshot as copy. | M9 | Not Started |  |
| F51-05 | Optional version snapshots | delete snapshots. | M9 | Not Started |  |
| F52-01 | Keyboard shortcuts | Ctrl+O — Open | M1/M9 | Not Started |  |
| F52-02 | Keyboard shortcuts | Ctrl+S — Save | M1/M9 | Not Started |  |
| F52-03 | Keyboard shortcuts | Ctrl+Shift+S — Save As | M1/M9 | Not Started |  |
| F52-04 | Keyboard shortcuts | Ctrl+P — Print | M1/M9 | Not Started |  |
| F52-05 | Keyboard shortcuts | Ctrl+W — Close tab/document | M1/M9 | Not Started |  |
| F52-06 | Keyboard shortcuts | Ctrl+Tab — Next tab | M1/M9 | Not Started |  |
| F52-07 | Keyboard shortcuts | Ctrl+Shift+Tab — Previous tab | M1/M9 | Not Started |  |
| F52-08 | Keyboard shortcuts | Ctrl+F — Find | M1/M9 | Not Started |  |
| F52-09 | Keyboard shortcuts | F3 / Shift+F3 — Next/previous result | M1/M9 | Not Started |  |
| F52-10 | Keyboard shortcuts | Ctrl+C — Copy | M1/M9 | Not Started |  |
| F52-11 | Keyboard shortcuts | Ctrl+X — Cut | M1/M9 | Not Started |  |
| F52-12 | Keyboard shortcuts | Ctrl+V — Paste | M1/M9 | Not Started |  |
| F52-13 | Keyboard shortcuts | Ctrl+A — Select all | M1/M9 | Not Started |  |
| F52-14 | Keyboard shortcuts | Ctrl+Z — Undo | M1/M9 | Not Started |  |
| F52-15 | Keyboard shortcuts | Ctrl+Y — Redo | M1/M9 | Not Started |  |
| F52-16 | Keyboard shortcuts | Ctrl++ — Zoom in | M1/M9 | Not Started |  |
| F52-17 | Keyboard shortcuts | Ctrl+- — Zoom out | M1/M9 | Not Started |  |
| F52-18 | Keyboard shortcuts | Ctrl+0 — Fit/actual-size behavior depending on design | M1/M9 | Not Started |  |
| F52-19 | Keyboard shortcuts | F11 — Full screen | M1/M9 | Not Started |  |
| F52-20 | Keyboard shortcuts | Delete — Delete selected annotation/page when appropriate | M1/M9 | Not Started |  |
| F52-21 | Keyboard shortcuts | arrow keys — navigation | M1/M9 | Not Started |  |
| F52-22 | Keyboard shortcuts | Page Up/Page Down — page navigation | M1/M9 | Not Started |  |
| F53-01 | Precision touchpad behavior | two-finger scroll | M1/M2 | Not Started |  |
| F53-02 | Precision touchpad behavior | pinch zoom on supported precision touchpads | M1/M2 | Not Started |  |
| F53-03 | Precision touchpad behavior | standard Windows touchpad gestures where they map naturally to application navigation | M1/M2 | Not Started |  |
| F54-01 | Toolbar customization | sidebar | M1/M9 | Not Started |  |
| F54-02 | Toolbar customization | previous | M1/M9 | Not Started |  |
| F54-03 | Toolbar customization | next | M1/M9 | Not Started |  |
| F54-04 | Toolbar customization | page number | M1/M9 | Not Started |  |
| F54-05 | Toolbar customization | zoom | M1/M9 | Not Started |  |
| F54-06 | Toolbar customization | fit page | M1/M9 | Not Started |  |
| F54-07 | Toolbar customization | fit width | M1/M9 | Not Started |  |
| F54-08 | Toolbar customization | search | M1/M9 | Not Started |  |
| F54-09 | Toolbar customization | markup | M1/M9 | Not Started |  |
| F54-10 | Toolbar customization | highlight | M1/M9 | Not Started |  |
| F54-11 | Toolbar customization | rotate | M1/M9 | Not Started |  |
| F54-12 | Toolbar customization | crop | M1/M9 | Not Started |  |
| F54-13 | Toolbar customization | signature | M1/M9 | Not Started |  |
| F54-14 | Toolbar customization | print | M1/M9 | Not Started |  |
| F54-15 | Toolbar customization | inspector | M1/M9 | Not Started |  |
| F54-16 | Toolbar customization | share | M1/M9 | Not Started |  |
| F54-17 | Toolbar customization | OCR | M1/M9 | Not Started |  |
| F54-18 | Toolbar customization | default toolbar | M1/M9 | Not Started |  |
| F54-19 | Toolbar customization | reset toolbar | M1/M9 | Not Started |  |
| F54-20 | Toolbar customization | compact icon mode | M1/M9 | Not Started |  |
| F55-01 | Preferences | theme | M1/M9 | In Progress | Theme setting persisted in settings.json |
| F55-02 | Preferences | restore previous session | M1/M9 | Not Started |  |
| F55-03 | Preferences | recent file count | M1/M9 | Not Started |  |
| F55-04 | Preferences | check for updates | M1/M9 | Not Started |  |
| F55-05 | Preferences | default page layout | M1/M9 | Not Started |  |
| F55-06 | Preferences | default zoom | M1/M9 | Not Started |  |
| F55-07 | Preferences | remember last page | M1/M9 | Not Started |  |
| F55-08 | Preferences | remember zoom | M1/M9 | Not Started |  |
| F55-09 | Preferences | open PDF in tabs/windows | M1/M9 | Not Started |  |
| F55-10 | Preferences | annotation author | M1/M9 | Not Started |  |
| F55-11 | Preferences | OCR behavior | M1/M9 | Not Started |  |
| F55-12 | Preferences | autosave behavior | M1/M9 | Not Started |  |
| F55-13 | Preferences | open multiple images in same window or separate windows | M1/M9 | Not Started |  |
| F55-14 | Preferences | 100% zoom meaning | M1/M9 | Not Started |  |
| F55-15 | Preferences | default interpolation | M1/M9 | Not Started |  |
| F55-16 | Preferences | color management | M1/M9 | Not Started |  |
| F55-17 | Preferences | animation autoplay | M1/M9 | Not Started |  |
| F55-18 | Preferences | default annotation colors | M1/M9 | Not Started |  |
| F55-19 | Preferences | default line width | M1/M9 | Not Started |  |
| F55-20 | Preferences | signature handling | M1/M9 | Not Started |  |
| F55-21 | Preferences | crash recovery interval | M1/M9 | Not Started |  |
| F55-22 | Preferences | local-only OCR preference | M1/M9 | Not Started |  |
| F55-23 | Preferences | clear recent files | M1/M9 | Not Started |  |
| F55-24 | Preferences | clear saved signatures | M1/M9 | Not Started |  |
| F55-25 | Preferences | strip metadata defaults | M1/M9 | Not Started |  |
| F56-01 | Accessibility | Windows UI Automation. | M9 | Not Started |  |
| F56-02 | Accessibility | Keyboard-accessible controls. | M9 | Not Started |  |
| F56-03 | Accessibility | Visible focus indicators. | M9 | Not Started |  |
| F56-04 | Accessibility | Screen-reader labels. | M9 | Not Started |  |
| F56-05 | Accessibility | High-contrast mode. | M9 | Not Started |  |
| F56-06 | Accessibility | Windows text scaling. | M9 | Not Started |  |
| F56-07 | Accessibility | Logical tab order. | M9 | Not Started |  |
| F56-08 | Accessibility | Descriptive names for toolbar icons. | M9 | Not Started |  |
| F56-09 | Accessibility | Custom description/alt text for images where PDF/image format supports it. | M9 | Not Started |  |
| F56-10 | Accessibility | Signature descriptions. | M9 | Not Started |  |
| F56-11 | Accessibility | Zoom without breaking UI layout. | M9 | Not Started |  |
| F57-01 | Performance behavior | very fast startup | M2+/M9 | Not Started |  |
| F57-02 | Performance behavior | fast first-page PDF display | M2+/M9 | Not Started |  |
| F57-03 | Performance behavior | render visible pages before off-screen pages | M2+/M9 | In Progress | Visible-page biased render + LRU cache |
| F57-04 | Performance behavior | asynchronous thumbnail generation | M2+/M9 | Not Started |  |
| F57-05 | Performance behavior | background text indexing | M2+/M9 | Not Started |  |
| F57-06 | Performance behavior | lazy OCR | M2+/M9 | Not Started |  |
| F57-07 | Performance behavior | GPU acceleration where appropriate | M2+/M9 | Not Started |  |
| F57-08 | Performance behavior | smooth scrolling | M2+/M9 | Not Started |  |
| F57-09 | Performance behavior | large-document virtualization | M2+/M9 | Not Started |  |
| F57-10 | Performance behavior | low memory usage | M2+/M9 | Not Started |  |
| F57-11 | Performance behavior | unload distant PDF pages | M2+/M9 | Not Started |  |
| F57-12 | Performance behavior | cancel long-running operations | M2+/M9 | In Progress | PDF OCR Cancel OCR + `CancellationToken`; PDF search cancel |
| F57-13 | Performance behavior | progress indicator for: | M2+/M9 | Not Started |  |
| F58-01 | Large-document handling | PDFs with thousands of pages. | M2+/M9 | In Progress | Page virtualization via on-demand render + cache |
| F58-02 | Large-document handling | Very large raster images. | M2+/M9 | Not Started |  |
| F58-03 | Large-document handling | Progressive rendering. | M2+/M9 | Not Started |  |
| F58-04 | Large-document handling | Avoid loading entire PDF rasterized into memory. | M2+/M9 | Not Started |  |
| F58-05 | Large-document handling | Efficient page cache. | M2+/M9 | Not Started |  |
| F58-06 | Large-document handling | Search indexing in background. | M2+/M9 | Not Started |  |
| F58-07 | Large-document handling | Partial OCR. | M2+/M9 | Not Started |  |
| F58-08 | Large-document handling | Cancelable operations. | M2+/M9 | In Progress | PDF search cancel + PDF/image OCR Cancel OCR |
| F59-01 | Multi-document workflow | multiple tabs | M1/M3 | Implemented | Tab strip + per-tab document hosts |
| F59-02 | Multi-document workflow | multiple windows | M1/M3 | Implemented | File → New Window; per-window `WorkspaceState` |
| F59-03 | Multi-document workflow | drag tabs between windows | M1/M3 | Not Started |  |
| F59-04 | Multi-document workflow | drag PDF pages between documents | M1/M3 | Implemented | Cross-tab/window insert via `PdfPageDragRegistry` |
| F59-05 | Multi-document workflow | drag images between compatible contexts | M1/M3 | Deferred | Needs M5 image editor |
| F59-06 | Multi-document workflow | copy/paste between documents | M1/M3 | Implemented | Ctrl+C/V pages via `PdfPageClipboard` |
| F59-07 | Multi-document workflow | side-by-side windows using Windows Snap | M1/M3 | Implemented | Multi-window shell; Snap is OS-native |
| F59-08 | Multi-document workflow | maintain independent undo history for each document | M1/M3 | Implemented | Per-view `PdfPageEditHistory` |
| F59-09 | Multi-document workflow | retain per-document page/zoom position | M1/M3 | Implemented | Per-document `PdfViewState` persistence |
| F60-01 | Context-sensitive commands | Copy | M1/M3 | Not Started |  |
| F60-02 | Context-sensitive commands | Highlight | M1/M3 | Not Started |  |
| F60-03 | Context-sensitive commands | Underline | M1/M3 | Not Started |  |
| F60-04 | Context-sensitive commands | Strikethrough | M1/M3 | Not Started |  |
| F60-05 | Context-sensitive commands | Search | M1/M3 | Not Started |  |
| F60-06 | Context-sensitive commands | Style | M1/M3 | Not Started |  |
| F60-07 | Context-sensitive commands | Duplicate | M1/M3 | Not Started |  |
| F60-08 | Context-sensitive commands | Delete | M1/M3 | Not Started |  |
| F60-09 | Context-sensitive commands | Copy | M1/M3 | Not Started |  |
| F61-01 | Non-destructive editing where practical | PDF annotations | M3-M5 | Not Started |  |
| F61-02 | Non-destructive editing where practical | image markup | M3-M5 | Not Started |  |
| F61-03 | Non-destructive editing where practical | crops | M3-M5 | Tested | CropBox-only until optional permanent export |
| F61-04 | Non-destructive editing where practical | adjustments | M3-M5 | Not Started |  |
| F61-05 | Non-destructive editing where practical | signatures | M3-M5 | Not Started |  |
| F61-06 | Non-destructive editing where practical | shapes | M3-M5 | Not Started |  |
| F61-07 | Non-destructive editing where practical | text | M3-M5 | Not Started |  |
| F62-01 | Supported output formats | PDF | M5/M7 | Implemented | Save / Extract / OCR→PDF / cropped export |
| F62-02 | Supported output formats | PNG | M5/M7 | Tested | Image export + PDF page Export (`WriteBgraAsync`) |
| F62-03 | Supported output formats | JPEG | M5/M7 | Tested | Image JPEG quality export + PDF page Export |
| F62-04 | Supported output formats | WebP | M5/M7 | Tested | Image Convert + PDF page Export |
| F62-05 | Supported output formats | TIFF | M5/M7 | Tested | Image Convert + PDF page Export |
| F62-06 | Supported output formats | BMP | M5/M7 | Tested | Image Convert + PDF page Export |
| F62-07 | Supported output formats | HEIF/HEIC | M5/M7 | Deferred | Magick build lacks HEIF encode delegate in CI/dev snapshots |
| F62-08 | Supported output formats | AVIF | M5/M7 | Tested | Image Convert + PDF page Export |
| F62-09 | Supported output formats | GIF | M5/M7 | Tested | Image Convert + PDF page Export |
| F62-10 | Supported output formats | JPEG 2000 | M5/M7 | Tested | Image Convert + PDF page Export |
| F63-01 | Explicit exclusions | all touchscreen-specific interaction | n/a | Deferred | Intentionally out of scope per FEATURES.md §63 |
| F63-02 | Explicit exclusions | all stylus/pen interaction | n/a | Deferred | Intentionally out of scope per FEATURES.md §63 |
| F63-03 | Explicit exclusions | Windows Ink | n/a | Deferred | Intentionally out of scope per FEATURES.md §63 |
| F63-04 | Explicit exclusions | pressure-sensitive pen input | n/a | Deferred | Intentionally out of scope per FEATURES.md §63 |
| F63-05 | Explicit exclusions | touch-display pinch/pan gestures | n/a | Deferred | Intentionally out of scope per FEATURES.md §63 |
| F63-06 | Explicit exclusions | Force Touch-specific drawing | n/a | Deferred | Intentionally out of scope per FEATURES.md §63 |
| F63-07 | Explicit exclusions | Vision Pro integration | n/a | Deferred | Intentionally out of scope per FEATURES.md §63 |
| F63-08 | Explicit exclusions | Spatial Preview | n/a | Deferred | Intentionally out of scope per FEATURES.md §63 |
| F63-09 | Explicit exclusions | macOS Continuity Camera protocol | n/a | Deferred | Intentionally out of scope per FEATURES.md §63 |
| F63-10 | Explicit exclusions | AirDrop | n/a | Deferred | Intentionally out of scope per FEATURES.md §63 |
| F63-11 | Explicit exclusions | FaceTime | n/a | Deferred | Intentionally out of scope per FEATURES.md §63 |
| F63-12 | Explicit exclusions | Apple Maps dependency | n/a | Deferred | Intentionally out of scope per FEATURES.md §63 |
| F63-13 | Explicit exclusions | Apple Mail integration | n/a | Deferred | Intentionally out of scope per FEATURES.md §63 |
| F63-14 | Explicit exclusions | iCloud signature synchronization | n/a | Deferred | Intentionally out of scope per FEATURES.md §63 |
| F63-15 | Explicit exclusions | macOS document-versioning UI | n/a | Deferred | Intentionally out of scope per FEATURES.md §63 |
| F63-16 | Explicit exclusions | macOS file-locking behavior | n/a | Deferred | Intentionally out of scope per FEATURES.md §63 |
| F63-17 | Explicit exclusions | Quartz Filters as a named system | n/a | Deferred | Intentionally out of scope per FEATURES.md §63 |
| F63-18 | Explicit exclusions | ColorSync-specific UI | n/a | Deferred | Intentionally out of scope per FEATURES.md §63 |
| F63-19 | Explicit exclusions | macOS title-bar document proxy menu | n/a | Deferred | Intentionally out of scope per FEATURES.md §63 |
| F63-20 | Explicit exclusions | direct camera memory-card photo importer, at least initially | n/a | Deferred | Intentionally out of scope per FEATURES.md §63 |
| F63-21 | Explicit exclusions | full 3D scene editor | n/a | Deferred | Intentionally out of scope per FEATURES.md §63 |
| F63-22 | Explicit exclusions | USD scene hierarchy | n/a | Deferred | Intentionally out of scope per FEATURES.md §63 |
| F63-23 | Explicit exclusions | USDZ authoring | n/a | Deferred | Intentionally out of scope per FEATURES.md §63 |
| F63-24 | Explicit exclusions | GLTF/STL/OBJ editing | n/a | Deferred | Intentionally out of scope per FEATURES.md §63 |
| F63-25 | Explicit exclusions | cameras/lights/materials | n/a | Deferred | Intentionally out of scope per FEATURES.md §63 |
| F63-26 | Explicit exclusions | 3D animation | n/a | Deferred | Intentionally out of scope per FEATURES.md §63 |
| F63-27 | Explicit exclusions | ray tracing | n/a | Deferred | Intentionally out of scope per FEATURES.md §63 |
| F63-28 | Explicit exclusions | Gaussian splat editing | n/a | Deferred | Intentionally out of scope per FEATURES.md §63 |
| F63-29 | Explicit exclusions | Vision Pro spatial export workflows | n/a | Deferred | Intentionally out of scope per FEATURES.md §63 |
| F64-00 | Resulting application scope | Overall product framing (six integrated tools + low-friction workflows) | all | Not Started | Guiding summary, not a discrete shippable checkbox |

_Generated from FEATURES.md top-level bullets. Total tracked rows: 879._
