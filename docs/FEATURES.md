# Windows Preview Adaptation — Complete Feature Specification

This document defines the target feature set for a Windows adaptation of macOS Preview.

Scope assumptions:

- The application will run only on conventional Windows desktop/laptop systems.
- Touchscreen, pen, stylus, Windows Ink, and touch-display-specific features are intentionally excluded.
- Precision touchpad gestures may be supported because they do not require a touchscreen display.
- Apple-specific ecosystem integrations are replaced with Windows-native equivalents where useful.
- macOS Preview's newer 3D/Vision Pro functionality is intentionally excluded.
- The application should preserve Preview's low-friction workflows rather than reproduce its exact macOS appearance.

---

## 1. Application and file handling

- Open files through:
  - File → Open
  - drag-and-drop onto the application
  - Windows Explorer double-click
  - Windows Explorer → Open With
  - recent files list
- Open multiple files simultaneously.
- Open multiple files:
  - as separate windows
  - as tabs in one window
  - as multiple items in one image-viewing window
- Reopen recently used files.
- Restore previously open tabs/windows after restart, optionally.
- Native Windows file associations.
- Configurable default associations for supported formats.
- Drag files from Explorer into an existing application window.
- Drag files from the application into Explorer where meaningful.
- Open files from:
  - local disks
  - network shares
  - OneDrive-synchronized folders
  - removable drives
- Normal Windows long-path and Unicode filename support.
- Read-only file detection.
- Warn when attempting to overwrite a read-only file.
- File → New from Clipboard.
- File → Duplicate.
- File → Save.
- File → Save As.
- File → Export.
- File → Rename.
- File → Move.
- File → Properties.
- Close:
  - current tab
  - current document
  - current window
  - all documents
- Unsaved-change prompt where appropriate.
- Optional autosave.
- Crash-recovery copy.
- Undo/redo history.
- Do not silently overwrite originals by default.

---

## 2. Main window and interface

- Standard Windows title bar.
- Menu bar or equivalent command interface.
- Customizable toolbar.
- Optional compact toolbar.
- Hide/show toolbar.
- Hide/show sidebar.
- Resizable sidebar.
- Resizable document area.
- Full-screen mode.
- Tabbed documents.
- Tear tab into separate window.
- Move tabs between windows.
- Reorder tabs.
- Context menus throughout.
- Dark mode.
- Light mode.
- Follow Windows system theme.
- High-DPI scaling.
- Multi-monitor support.
- Per-monitor DPI awareness.
- Mouse support.
- Precision touchpad gesture support where useful.
- Keyboard-first operation.
- Accessibility through Windows UI Automation.

---

## 3. Sidebar modes

A single contextual sidebar should support:

- Page thumbnails
- Table of contents
- Search results
- Bookmarks
- Highlights and annotations
- Image list
- Contact sheet
- Document attachments, if supported
- Metadata/properties where useful

Workflow:

- Switch sidebar mode without opening another window.
- Resize thumbnail size.
- Multi-select sidebar items.
- Shift-click range selection.
- Ctrl-click noncontiguous selection.
- Keyboard navigation.
- Drag selected items.
- Context menus.
- Sidebar selection controls the scope of many editing commands.

---

## 4. PDF viewing

- Open standard PDF files quickly.
- Render vector content accurately.
- Render embedded images.
- Render embedded fonts.
- Support transparency.
- Support rotated pages.
- Support PDFs containing different page sizes.
- Password-protected PDF opening.
- Continuous scrolling.
- Single-page mode.
- Two-page/facing-page mode.
- Optional cover-page behavior for facing pages.
- Page thumbnails.
- Page number navigation.
- Go to page.
- Previous page.
- Next page.
- First page.
- Last page.
- Mouse-wheel scrolling.
- Page Up/Page Down navigation.
- Fit page.
- Fit width.
- Actual size / 100%.
- Custom zoom percentage.
- Zoom in/out.
- Ctrl+mouse wheel zoom.
- Pinch-to-zoom on supported precision touchpads.
- Rectangular zoom-to-area.
- Magnifier/loupe tool.
- Presentation/slideshow mode.
- Remember last viewed page, optionally.
- Remember zoom/layout per document, optionally.

---

## 5. PDF table of contents and navigation

- Read embedded PDF outlines/bookmarks.
- Hierarchical table of contents.
- Expand/collapse outline nodes.
- Click outline entry to navigate.
- Keyboard navigation.
- Preserve embedded outline while editing when possible.
- Show internal PDF links.
- Support clickable:
  - internal page links
  - web URLs
  - email links
- Back/forward navigation history within the document.

---

## 6. PDF search

- Full-text search.
- Case-insensitive search.
- Optional case-sensitive search.
- Exact phrase search.
- Any-word search.
- Search all occurrences.
- Search current PDF.
- Search all open PDFs.
- Highlight matches on pages.
- Results sidebar.
- Show contextual snippets around matches.
- Sort results by:
  - page order
  - relevance
- Next match.
- Previous match.
- Clear search.
- Click result to jump to it.
- Search OCR text where OCR has been generated.

---

## 7. PDF text interaction

- Text selection.
- Copy text.
- Select across lines.
- Select columns where practical.
- Select all text on page/document.
- Right-click selected text.
- Copy.
- Search selected text.
- OCR fallback on scanned PDFs.
- Preserve reasonable reading order during copy.
- Rectangular selection for copying a region as an image.
- Copy selected PDF region to clipboard as bitmap.
- Drag selected text where supported.

---

## 8. OCR / Live Text equivalent

For images and scanned PDFs:

- Detect text automatically or on demand.
- Select detected text directly over the image.
- Copy text.
- Copy all recognized text.
- Search recognized text.
- OCR current page.
- OCR selected pages.
- OCR entire PDF.
- OCR multiple images.
- Optionally embed OCR text layer into PDF.
- Preserve image underneath the OCR layer.

Recognize actionable text types:

- URLs
- email addresses
- phone numbers
- physical addresses
- dates
- times

Contextual actions:

- Open URL in default browser.
- Create email in default mail application.
- Copy phone number.
- Open address in user's default mapping/web service.
- Create calendar event through appropriate Windows/system mechanism where feasible.
- Search web.
- Copy recognized value.

---

## 9. PDF bookmarks

User-created bookmarks separate from the PDF's built-in TOC:

- Add bookmark at current page/location.
- Rename bookmark.
- Delete bookmark.
- List bookmarks in sidebar.
- Reorder bookmarks where feasible.
- Click to navigate.
- Preserve bookmarks when saving.
- Export them as standard PDF bookmarks where compatible.

---

## 10. PDF page manipulation

- Select page thumbnails.
- Multi-select pages.
- Select ranges.
- Reorder pages by dragging thumbnails.
- Move multiple pages as one selection.
- Delete pages.
- Duplicate pages.
- Insert blank page.
- Insert PDF file.
- Insert pages from another PDF.
- Append PDF.
- Prepend PDF.
- Insert at arbitrary position.
- Extract selected pages.
- Save extracted pages as new PDF.
- Split PDF.
- Merge PDFs.
- Rotate selected pages left.
- Rotate selected pages right.
- Batch rotation.
- Crop selected page.
- Apply same crop to multiple pages.
- Change page order.
- Copy/paste pages between documents.

---

## 11. Preview-style PDF drag-and-drop workflows

These should be treated as first-class features.

### Between two open PDFs

Drag thumbnail:

`PDF A → PDF B`

to copy/move the page into the target PDF.

Support:

- one page
- multiple contiguous pages
- multiple noncontiguous pages
- insertion before/after any page
- visible insertion indicator

### From Windows Explorer into PDF

Drag:

`another.pdf`

onto the thumbnail sidebar.

Result:

- insert entire PDF at drop location.

### From PDF to Windows Explorer

Drag selected thumbnail(s) out of the application.

Result:

- create a new PDF containing those selected pages.

### Within a PDF

Drag thumbnails to reorder them.

### Between tabs/windows

Allow page dragging even if source and destination PDFs are in:

- different windows
- different tabs

This behavior is central to the application.

---

## 12. PDF crop

- Rectangular page crop.
- Visual crop handles.
- Numeric crop dimensions.
- Units:
  - pixels where meaningful
  - inches
  - centimeters
  - millimeters
  - PDF points
- Apply to current page.
- Apply to selected pages.
- Apply to all pages.
- Undo crop.
- Preserve underlying PDF content if using non-destructive CropBox changes.
- Optional permanent crop/export function.

---

## 13. PDF annotations

Support standard PDF annotations where possible.

- Highlight
- Underline
- Strikethrough
- Freehand ink
- Lines
- Arrows
- Rectangles
- Rounded rectangles
- Ellipses
- Polygons
- Callouts
- Text boxes
- Sticky notes
- Speech-bubble-like callouts
- Stamps, optionally
- Signatures
- Freeform shapes
- Annotation selection tool

For annotations:

- move
- resize
- rotate where appropriate
- duplicate
- delete
- cut
- copy
- paste
- multi-select
- group where useful
- change border color
- change fill color
- change opacity
- change line thickness
- change line style
- change arrowheads
- change font
- change font size
- change text color
- change text alignment

---

## 14. Highlight workflow

- Select text and apply highlight.
- Persistent highlight mode:
  - turn on
  - every subsequent text selection becomes highlighted
- Multiple highlight colors.
- Change existing highlight color.
- Underline selection.
- Strikethrough selection.
- Remove markup.
- Annotation sidebar showing all:
  - highlights
  - underlines
  - strikethroughs
  - notes
- Click annotation in sidebar to jump to it.

---

## 15. Notes

- Add sticky note.
- Enter note text.
- Collapse note.
- Expand note.
- Move note icon.
- Change note color.
- Edit.
- Delete.
- Show note author.
- Configurable annotation author name.
- Optional date/time metadata.
- Show all notes in sidebar.
- Print notes optionally.

---

## 16. Text boxes and callouts

- Add text box anywhere.
- Type arbitrary text over PDF.
- Move it.
- Resize it.
- Rotate where appropriate.
- Font family.
- Font size.
- Bold.
- Italic.
- Underline.
- Text color.
- Background/fill color.
- Border.
- Opacity.
- Alignment.
- Callout pointer.
- Move pointer separately from text box.

---

## 17. Shapes

Include:

- line
- arrow
- rectangle
- rounded rectangle
- ellipse
- polygon
- star
- speech bubble/callout
- translucent highlight rectangle
- magnification/loupe annotation, optionally

Operations:

- resize
- rotate
- move
- duplicate
- multi-select
- border color
- border width
- line style
- fill color
- opacity
- shape-specific adjustment handles

---

## 18. Freehand drawing

- Mouse drawing.
- Stroke color.
- Stroke width.
- Stroke opacity.
- Eraser.
- Undo stroke.
- Select/move completed strokes.
- Delete stroke.

Optional Preview-like smart drawing:

- recognize rough:
  - circle
  - rectangle
  - line
  - arrow
  - triangle
  - common geometric shapes
- offer cleaned-up shape or original stroke.

No touchscreen, stylus, pen-pressure, Windows Ink, or Force Touch support is required.

---

## 19. PDF signatures

Create signature using:

- mouse
- webcam photographing a signature on paper
- imported transparent signature image

Signature library:

- save signature
- name signature
- delete signature
- reorder signatures
- local storage
- optional application-specific cloud sync later

Using signature:

- insert
- resize
- move
- rotate where appropriate
- duplicate
- delete
- preserve transparency

No dependency on iCloud, Apple devices, touchscreen input, or stylus input.

---

## 20. PDF forms

Support AcroForm PDFs:

- text fields
- multiline fields
- checkboxes
- radio buttons
- dropdowns
- combo boxes
- list boxes
- buttons where applicable
- signatures where supported
- tab-order navigation

For non-fillable PDFs:

- form-filling mode that overlays text fields manually.

Useful conveniences:

- automatic font sizing
- remember recently entered values, optionally
- user-defined profile for:
  - name
  - address
  - email
  - phone
- optional AutoFill from that application profile

No dependency on Apple Contacts.

---

## 21. Redaction

- Mark text for redaction.
- Mark rectangular areas for redaction.
- Preview pending redactions.
- Remove pending redaction.
- Apply redactions permanently.
- Remove underlying text/image data, not merely cover it visually.
- Warn before permanent application.
- Sanitize associated hidden content where practical.
- Option to remove:
  - metadata
  - annotations
  - embedded files
  - hidden layers
- Search and redact matching text, later/advanced.

---

## 22. PDF annotation flattening

Provide explicit:

`Flatten Annotations`

and export option:

`☑ Flatten annotations`

Flatten:

- highlights
- notes as configured
- shapes
- signatures
- text boxes
- drawings

Original annotations should remain editable unless user deliberately flattens/export-flattens.

---

## 23. PDF security

- Open encrypted PDFs.
- Create password-protected PDFs.
- Set document-open password.
- Set permissions/owner password where PDF standard permits.
- Restrict:
  - printing
  - editing
  - copying
  - annotation
  - page extraction
- Change existing permissions where credentials permit.
- Remove protection when authorized.
- Display encryption information.
- Warn about limitations of PDF permission enforcement.

---

## 24. PDF optimization and compression

Replace Quartz Filters with clear controls.

Preset modes:

- Lossless
- High quality
- Balanced
- Small file
- Custom

Custom controls:

- downsample images above selected DPI
- target DPI
- JPEG quality
- preserve monochrome content
- compress streams
- subset fonts where appropriate
- remove unused objects
- optimize object structure
- linearize / Fast Web View
- remove metadata optionally
- estimate output size before saving where feasible

---

## 25. PDF metadata

View:

- title
- author
- subject
- keywords
- creator
- producer
- created date
- modified date
- page count
- PDF version
- page dimensions
- file size
- encryption status
- permissions
- embedded fonts, optionally

Edit:

- title
- author
- subject
- keywords
- creator
- producer

---

## 26. Image viewing

Formats should include at minimum:

- JPEG/JPG
- PNG
- GIF
- BMP
- TIFF
- WebP
- HEIF/HEIC where codecs are available
- AVIF where practical
- ICO
- JPEG 2000 where practical

Potential additional formats through codecs/libraries.

Features:

- fast opening
- zoom
- pan
- fit image
- actual size
- fullscreen
- next/previous image
- image list sidebar
- open group of images together
- slideshow
- drag-and-drop navigation
- high-resolution image support
- alpha transparency
- HDR display where Windows/display stack supports it
- color-managed display

---

## 27. Animated images

For animated GIF/WebP/APNG where supported:

- play
- pause
- restart
- next frame
- previous frame
- timeline/frame number
- loop
- inspect individual frames
- extract frame
- save selected frame as image

This can be a later feature but remains part of the complete target.

---

## 28. Image selection tools

- Rectangular selection.
- Elliptical selection.
- Freeform lasso.
- Smart lasso.
- Select all.
- Invert selection.
- Deselect.
- Move selected pixels.
- Copy.
- Cut.
- Paste.
- Delete selection.
- Crop to selection.

---

## 29. Smart object/background selection

- Smart Lasso.
- Edge-aware selection.
- Background removal.
- Subject extraction.
- Automatic subject detection.
- Remove background.
- Preserve transparent background.
- Offer conversion to transparency-capable format if source format cannot support alpha.
- Undo.
- Copy extracted subject.
- Save extracted subject as separate image.

Prefer local processing.

---

## 30. Image crop

- Interactive crop box.
- Free aspect ratio.
- Original aspect ratio.
- Common presets:
  - 1:1
  - 4:3
  - 3:2
  - 16:9
- Numeric width/height.
- Apply crop.
- Undo.
- Non-destructive editing internally until save where practical.

---

## 31. Image resizing

- Adjust width.
- Adjust height.
- Lock aspect ratio.
- Percentage scaling.
- Pixel units.
- Physical units.
- DPI/PPI.
- Resampling toggle.
- Resampling algorithm options, possibly:
  - nearest-neighbor
  - bilinear
  - bicubic
  - Lanczos/high-quality
- Preserve aspect ratio.
- Estimated resulting dimensions.
- Estimated file size.
- Batch resize selected images.

---

## 32. Image orientation

- Rotate left 90°.
- Rotate right 90°.
- Rotate 180°.
- Flip horizontal.
- Flip vertical.
- Batch operations on selected images.
- Respect EXIF orientation.
- Option to normalize EXIF orientation into pixels.

---

## 33. Image color adjustments

Include:

- Auto Levels
- Exposure
- Contrast
- Highlights
- Shadows
- Saturation
- Temperature
- Tint
- Sharpness
- Sepia
- Black point / levels
- Gamma where useful
- Reset individual adjustment
- Reset all
- live preview
- histogram

Prefer non-destructive internal adjustment until save/export.

---

## 34. Image markup

Images should use the same markup tools as PDFs:

- mouse drawing
- shapes
- arrows
- text
- callouts
- signatures
- selection
- crop
- rotate

Unlike macOS Preview, avoid immediately baking annotations into the image while the document remains open.

Internally keep layers/edit state until:

- Save/export to a flat image format, or
- user explicitly flattens.

---

## 35. Image format conversion

Export between supported formats.

Examples:

- PNG
- JPEG
- WebP
- TIFF
- BMP
- GIF
- HEIC/HEIF where supported
- AVIF
- JPEG 2000
- PDF

Format-specific controls:

- JPEG quality
- WebP quality/lossless
- AVIF quality
- TIFF compression
- preserve/remove alpha
- preserve/remove metadata
- color profile handling

---

## 36. Batch image operations

Select multiple images and:

- resize
- rotate
- flip
- convert format
- export
- strip metadata
- change color profile
- rename, optionally

Show batch progress.

---

## 37. Image metadata

Display:

- dimensions
- pixel count
- DPI
- bit depth
- color space
- ICC profile
- file format
- compression
- file size
- camera make/model
- lens information
- exposure
- aperture
- ISO
- focal length
- capture date
- orientation
- GPS coordinates
- EXIF
- IPTC
- XMP where available

Edit selected useful fields:

- title
- description
- keywords
- copyright
- rating, optionally

---

## 38. GPS metadata

If GPS exists:

- display latitude/longitude.
- Copy coordinates.
- Open in default/browser mapping service.
- Remove GPS metadata.
- Optional embedded map later.

---

## 39. Color management

Use Windows-native ICC/WCS support.

- Detect embedded ICC profile.
- Honor embedded profile while displaying.
- Assign ICC profile.
- Convert between profiles.
- Use monitor profile.
- Soft-proof through another ICC profile.
- Toggle soft proof.
- Gamut-warning option, advanced.
- Rendering intent selection:
  - perceptual
  - relative colorimetric
  - absolute colorimetric
  - saturation

This can sit under advanced tools.

---

## 40. Clipboard integration

Support clipboard formats intelligently.

Copy:

- PDF text → text
- PDF region → bitmap
- image selection → image
- whole image → image
- recognized OCR text → text
- annotation where possible

Paste:

- image from clipboard into image document
- image clipboard → create new image
- text into annotation/text field
- file paths where appropriate

`Ctrl+V` with an image and no active compatible document should be able to create a new untitled image.

This makes Windows Snipping Tool integration effectively seamless.

---

## 41. Screenshot workflow

No full screenshot subsystem is required initially.

Support this Windows-native workflow:

1. `Win + Shift + S`
2. capture
3. open application
4. `Ctrl + V`
5. new untitled image appears

Optional later command:

`File → New from Screen Capture`

using Windows capture APIs.

---

## 42. Scanner support

Eventually support Windows scanner APIs.

- Discover connected scanners.
- Flatbed scanner.
- Automatic document feeder.
- Duplex feeder.
- Color.
- Grayscale.
- Black and white.
- Resolution/DPI.
- Paper size.
- Auto crop.
- Auto straighten.
- Brightness/contrast where hardware supports it.
- Scan one page.
- Scan multiple pages.
- Scan directly into new PDF.
- Insert scanned pages into existing PDF.
- Scan multiple photos separately from a flatbed where detection is practical.

---

## 43. Webcam/camera import

Primarily for signatures/document capture:

- select webcam
- capture image
- crop result
- insert into document

No attempt to reproduce Apple's Continuity Camera protocol.

---

## 44. Printing

PDF and images:

- print current page
- print selected pages
- print page range
- print all pages
- print selected images
- copies
- collate
- duplex
- printer selection
- paper size
- orientation
- margins
- scale
- actual size
- fit to printable area
- fill page
- pages per sheet
- auto rotate
- center
- print annotations
- print notes optionally
- grayscale
- Windows printer properties integration

---

## 45. Exporting

General export dialog should support:

- output format
- destination
- quality
- compression
- dimensions
- metadata preservation
- color profile
- transparency
- PDF security
- annotation flattening

Quick export shortcuts can exist for commonly used formats.

---

## 46. Sharing and Windows integration

Replace Apple Share/AirDrop with:

- Windows Share UI where available
- Open containing folder
- Copy file path
- Copy file
- Send to default email workflow where practical
- Nearby Share through Windows system facilities where available rather than custom implementation
- OneDrive works naturally because files are ordinary filesystem objects

Avoid Outlook-specific dependencies.

---

## 47. External application integration

Contextual commands:

- Open With...
- Show in File Explorer
- Open URL
- Open location in browser/maps
- Send via default mail application where possible

Respect user-selected Windows defaults.

---

## 48. File properties and inspector

Contextual inspector showing appropriate information for:

### PDF

- dimensions
- pages
- metadata
- security
- fonts
- annotations
- file size

### Images

- dimensions
- color profile
- metadata
- EXIF
- GPS
- file size

Selection-specific information should appear where useful.

---

## 49. Undo and redo

Comprehensive operation history for:

- annotations
- drawing
- page insertion
- page deletion
- page ordering
- page rotation
- crop
- resizing
- image adjustments
- metadata editing
- form filling
- signature placement
- redaction before permanent application

Support:

- Ctrl+Z
- Ctrl+Y / Ctrl+Shift+Z

History should be document-specific.

---

## 50. Autosave and recovery

Use Windows-appropriate semantics.

Default behavior:

- edits remain in memory until Save.
- periodic crash-recovery snapshot.
- closing unsaved file prompts user.

Optional preference:

- automatically save changes to original document.

Recovery:

- reopen recovered document after crash.
- never silently discard recovery data.
- remove recovery copy after successful save/close.

---

## 51. Optional version snapshots

Rather than reproducing macOS Browse All Versions:

- optional automatic local snapshots.
- show:
  - timestamp
  - thumbnail
  - file size
- restore snapshot.
- open snapshot as copy.
- delete snapshots.

This should be optional and lower priority.

---

## 52. Keyboard shortcuts

At minimum:

- Ctrl+O — Open
- Ctrl+S — Save
- Ctrl+Shift+S — Save As
- Ctrl+P — Print
- Ctrl+W — Close tab/document
- Ctrl+Tab — Next tab
- Ctrl+Shift+Tab — Previous tab
- Ctrl+F — Find
- F3 / Shift+F3 — Next/previous result
- Ctrl+C — Copy
- Ctrl+X — Cut
- Ctrl+V — Paste
- Ctrl+A — Select all
- Ctrl+Z — Undo
- Ctrl+Y — Redo
- Ctrl++ — Zoom in
- Ctrl+- — Zoom out
- Ctrl+0 — Fit/actual-size behavior depending on design
- F11 — Full screen
- Delete — Delete selected annotation/page when appropriate
- arrow keys — navigation
- Page Up/Page Down — page navigation

Shortcuts should be configurable eventually.

---

## 53. Precision touchpad behavior

Touchscreen and pen input are excluded, but ordinary laptop touchpads may still be supported.

- two-finger scroll
- pinch zoom on supported precision touchpads
- standard Windows touchpad gestures where they map naturally to application navigation

No touchscreen gestures, stylus input, pen pressure, Windows Ink, or Force Touch behavior is required.

---

## 54. Toolbar customization

Allow users to add/remove/reorder common commands such as:

- sidebar
- previous
- next
- page number
- zoom
- fit page
- fit width
- search
- markup
- highlight
- rotate
- crop
- signature
- print
- inspector
- share
- OCR

Provide:

- default toolbar
- reset toolbar
- compact icon mode

---

## 55. Preferences

### General

- theme
- restore previous session
- recent file count
- check for updates

### PDFs

- default page layout
- default zoom
- remember last page
- remember zoom
- open PDF in tabs/windows
- annotation author
- OCR behavior
- autosave behavior

### Images

- open multiple images in same window or separate windows
- 100% zoom meaning
- default interpolation
- color management
- animation autoplay

### Editing

- default annotation colors
- default line width
- signature handling
- crash recovery interval

### Privacy

- local-only OCR preference
- clear recent files
- clear saved signatures
- strip metadata defaults

---

## 56. Accessibility

- Windows UI Automation.
- Keyboard-accessible controls.
- Visible focus indicators.
- Screen-reader labels.
- High-contrast mode.
- Windows text scaling.
- Logical tab order.
- Descriptive names for toolbar icons.
- Custom description/alt text for images where PDF/image format supports it.
- Signature descriptions.
- Zoom without breaking UI layout.

No touchscreen-specific accessibility behavior is required.

---

## 57. Performance behavior

Part of Preview's appeal is responsiveness, so this should be treated as a feature.

- very fast startup
- fast first-page PDF display
- render visible pages before off-screen pages
- asynchronous thumbnail generation
- background text indexing
- lazy OCR
- GPU acceleration where appropriate
- smooth scrolling
- large-document virtualization
- low memory usage
- unload distant PDF pages
- cancel long-running operations
- progress indicator for:
  - OCR
  - export
  - compression
  - batch conversion
  - scanning

Opening a document should not wait for every thumbnail or search index to finish.

---

## 58. Large-document handling

- PDFs with thousands of pages.
- Very large raster images.
- Progressive rendering.
- Avoid loading entire PDF rasterized into memory.
- Efficient page cache.
- Search indexing in background.
- Partial OCR.
- Cancelable operations.

---

## 59. Multi-document workflow

The program should make working across files unusually easy.

- multiple tabs
- multiple windows
- drag tabs between windows
- drag PDF pages between documents
- drag images between compatible contexts
- copy/paste between documents
- side-by-side windows using Windows Snap
- maintain independent undo history for each document
- retain per-document page/zoom position

---

## 60. Context-sensitive commands

Commands should adapt to selection.

Example:

Selecting three PDF thumbnails and pressing Rotate rotates all three.

Selecting two images and choosing Resize applies batch resize.

Selecting text exposes:

- Copy
- Highlight
- Underline
- Strikethrough
- Search

Selecting an annotation exposes:

- Style
- Duplicate
- Delete
- Copy

This implicit selection-based behavior is important to keeping the application simple.

---

## 61. Non-destructive editing where practical

Internally retain editability until save/export for:

- PDF annotations
- image markup
- crops
- adjustments
- signatures
- shapes
- text

When exporting to a flat format, make the destructive operation explicit.

This improves on some of Preview's image behavior.

---

## 62. Supported output formats

Initial target:

### PDF

- PDF

### Raster

- PNG
- JPEG
- WebP
- TIFF
- BMP

Then add:

- HEIF/HEIC
- AVIF
- GIF
- JPEG 2000

according to codec/library practicality.

---

## 63. Explicitly excluded features

These are outside the intended product:

- all touchscreen-specific interaction
- all stylus/pen interaction
- Windows Ink
- pressure-sensitive pen input
- touch-display pinch/pan gestures
- Force Touch-specific drawing
- Vision Pro integration
- Spatial Preview
- macOS Continuity Camera protocol
- AirDrop
- FaceTime
- Apple Maps dependency
- Apple Mail integration
- iCloud signature synchronization
- macOS document-versioning UI
- macOS file-locking behavior
- Quartz Filters as a named system
- ColorSync-specific UI
- macOS title-bar document proxy menu
- direct camera memory-card photo importer, at least initially
- full 3D scene editor
- USD scene hierarchy
- USDZ authoring
- GLTF/STL/OBJ editing
- cameras/lights/materials
- 3D animation
- ray tracing
- Gaussian splat editing
- Vision Pro spatial export workflows

---

## 64. Resulting application scope

The finished application should function as six tightly integrated tools:

1. A fast PDF reader.
2. A PDF editor for page manipulation, forms, signing, markup, and redaction.
3. A lightweight image viewer/editor.
4. An OCR/document utility.
5. A file-conversion and batch-processing utility.
6. A scanner/document-capture utility.

The most important Preview behaviors to preserve are the low-friction ones:

- opening files almost instantly
- manipulating PDF pages directly through thumbnails
- dragging pages between documents
- using one contextual sidebar instead of separate tools
- performing operations on the current selection without wizards
- keeping ordinary markup/editing immediately accessible
- keeping the interface lightweight despite the broad feature set

These workflow behaviors should influence the design more strongly than reproducing Preview's exact visual appearance.
