# Interactive proof artifacts (M9)

Drop Windows capture files here after following [`../INTERACTIVE_VERIFY.md`](../INTERACTIVE_VERIFY.md)
(or `./scripts/interactive-verify.ps1` on Windows).
Do not commit large binaries unless closing the interactive gate in a dedicated PR.

| File | Gate |
| --- | --- |
| `m1-shell-tabs.png` | M1 shell with tabs |
| `m2-pdf-viewer.png` | M2 PDF viewer |
| `m3-page-dnd.mp4` | M3 §11 cross-doc DnD (keep short) |

Also attach Explorer default-app screenshots for F01-06/07 when flipping those matrix rows.
Double-click fixtures: [`samples/`](samples/README.md) (`sample.pdf`, `sample.png`).

```powershell
./scripts/interactive-verify.ps1 -StatusOnly   # which expected files exist
./scripts/interactive-verify.ps1 -VerifyOnly -OpenSamples
```
