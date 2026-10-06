# Glyph Windows interactive verify (M9 close gates)

Operator checklist for proof that cannot run on Linux CI. Complete these on a
Windows 10/11 machine with Developer Mode enabled, then attach artifacts to the
roadmap / matrix notes and flip Deferred rows where applicable.

Related: [`PACKAGING.md`](PACKAGING.md) (MSIX sideload), [`ROADMAP.md`](ROADMAP.md)
§ Remaining to close M9, ADR-012 / ADR-015 in [`DECISIONS.md`](DECISIONS.md).

## Quick start (Windows)

```powershell
# Download latest main CI x64 MSIX, sideload, probe UserChoice, open Default apps
./scripts/interactive-verify.ps1 -DownloadArtifact

# After setting Glyph as default, launch fixtures:
./scripts/interactive-verify.ps1 -VerifyOnly -OpenSamples

# Or use a package you already have / just published:
./scripts/interactive-verify.ps1 -PackageDir artifacts/msix
./scripts/interactive-verify.ps1 -PublishIfMissing   # local publish if missing
./scripts/interactive-verify.ps1 -StatusOnly         # which docs/proof files exist
```

Sample fixtures for double-click / DnD: [`docs/proof/samples/sample.pdf`](proof/samples/sample.pdf)
(2 pages) and [`docs/proof/samples/sample.png`](proof/samples/sample.png).

Then complete the capture steps below and drop files under [`docs/proof/`](proof/README.md).

## 0. Prerequisites

1. Download the latest `glyph-msix-layout` artifact from a green `main` CI run
   (or build locally: `./scripts/publish-msix.ps1 -Configuration Release -Runtime win-x64 -TestSign`).
   Prefer `./scripts/interactive-verify.ps1 -DownloadArtifact` when `gh` is available.
2. Confirm `artifacts/msix/Glyph.App_*.msix` and `Glyph.CI.TestSign.cer` exist.
3. Enable **Settings → Privacy & security → For developers → Developer Mode**.

## 1. MSIX sideload + Explorer associations (F01-06/07)

Windows CI already sideloads the test-signed package and probes installed `uap:FileType`
associations. This section is the remaining **interactive** Explorer default-app proof.

```powershell
./scripts/interactive-verify.ps1 -DownloadArtifact
# equivalent lower-level helpers:
./scripts/install-msix-test.ps1 -PackageDir artifacts/msix -Force
./scripts/install-msix-test.ps1 -VerifyOnly -ProbeUserDefaults -OpenDefaultApps
```

Then manually confirm:

| Step | Expected | Evidence |
| --- | --- | --- |
| Settings → Apps → Default apps | Glyph listed for `.pdf` and declared image types (or Open with → Glyph) | Screenshot |
| Double-click sample `.pdf` | Opens in Glyph | Screenshot or short clip — use `docs/proof/samples/sample.pdf` |
| Double-click sample `.png`/`.jpg` | Opens in Glyph | Screenshot or short clip — use `docs/proof/samples/sample.png` |
| Reboot + `-VerifyOnly` (optional) | Associations still present | Log snippet |

When all pass, update FEATURE_MATRIX F01-06/07 from Deferred → Tested with this
checklist date and attach paths under [`docs/proof/`](proof/README.md) (or PR description links).
`-ProbeUserDefaults` prints HKCU `UserChoice` ProgIds (informational); `-OpenDefaultApps`
opens Settings → Default apps.

## 2. M1 shell screenshot

With Glyph running (unpackaged `dotnet run` or sideloaded MSIX):

1. Open at least two documents in tabs (one PDF, one image if available).
2. Capture the main window showing menu bar, tabs, sidebar, and toolbar.
3. Save as `docs/proof/m1-shell-tabs.png` (or attach to the closing PR).

Check off ROADMAP M1 “Screenshot of shell with tabs”.

## 3. M2 viewer screenshots

1. Open a multi-page PDF; show continuous (or two-page) layout + Find hits if easy.
2. Capture zoom/fit chrome and page content.
3. Save as `docs/proof/m2-pdf-viewer.png`.

Check off ROADMAP M2 interactive screenshots pending.

## 4. M3 §11 cross-document DnD recording

1. Open two PDFs in separate tabs (or windows).
2. Record: thumbnail drag-reorder within a doc; drag a page to the other doc;
   Explorer → Glyph page insert if practical; Ctrl+C/V page paste.
3. Save as `docs/proof/m3-page-dnd.mp4` (keep under ~30s).

Check off ROADMAP M3 §11 screen recording pending / matrix F11 interactive note.

## 5. Production / Store signing (product decision)

Not a capture task — escalate before wiring secrets:

| Option | Notes |
| --- | --- |
| **A. Code-signing cert** | EV/OV Authenticode; sign `.msix` outside CI or via approved secrets |
| **B. Microsoft Store** | Partner Center; Store signs |
| **C. Sideload-only Preview** | Keep Developer Mode requirement; associations stay Deferred until §1 |

See [`PACKAGING.md`](PACKAGING.md) § Production / Store signing.

## 6. ADR-015 password-write (product decision)

Protect toolbar remains blocked until ADR-015 is Accepted:

- **A** — PdfSharp MIT write-encrypt (preferred)
- **C** — commercial SDK
- **D** — keep Blocked

Reply on the agent thread / issue with A, C, or D. No write-encrypt lands before that.
