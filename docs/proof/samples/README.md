# Interactive verify samples

Tiny fixtures for Windows Explorer double-click / Open With checks
([`../INTERACTIVE_VERIFY.md`](../INTERACTIVE_VERIFY.md)).

| File | Use |
| --- | --- |
| `sample.pdf` | 2-page PDF — default-app + M2 viewer / M3 DnD (open twice or duplicate) |
| `sample.png` | Image — default-app + shell tabs with a PDF |

```powershell
./scripts/interactive-verify.ps1 -DownloadArtifact
# After sideload, double-click:
explorer.exe docs\proof\samples\sample.pdf
explorer.exe docs\proof\samples\sample.png
```
