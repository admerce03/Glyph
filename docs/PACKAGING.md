# Glyph packaging (Milestone 9)

Authoritative associations deferral: [ADR-012](DECISIONS.md#adr-012--file-associations-deferred-to-packaging).  
Policy flags: `PackagingDeferredPolicy` (unit-tested).

## Distribution model

| Mode | When | How |
| --- | --- | --- |
| **Unpackaged** (default) | Day-to-day F5 / `dotnet run` | `WindowsPackageType=None` (ADR-006) |
| **Test-signed MSIX** | CI artifact + Developer Mode sideload | `scripts/publish-msix.ps1 -TestSign` → `scripts/install-msix-test.ps1` |
| **Production / Store** | Product decision (escalation) | Trusted cert or Partner Center; not wired in CI |

Open, drag-drop, and recent files work unpackaged. Explorer double-click / default-app assignment remain **Deferred** (F01-06/07) until a Windows machine verifies sideload defaults.

## What is shipped

1. **Single-project MSIX** — `src/Glyph.App/Package.appxmanifest` + conditional `GenerateAppxPackageOnBuild` when `-p:GlyphPackage=MSIX`.
2. **File type declarations** — `uap:FileTypeAssociation` for `.pdf` and common image extensions (`PackageFileAssociationDeclaration`).
3. **CI package** — Windows workflow publishes test-signed `Glyph.App_*.msix` + `Glyph.CI.TestSign.cer` (artifact `glyph-msix-layout`).
4. **Sideload helper** — `scripts/install-msix-test.ps1` trusts the CI cert, runs `Add-AppxPackage`, and probes installed `uap:FileType` entries (`-VerifyOnly`, `-ProbeUserDefaults`, `-OpenDefaultApps` supported).

## Publish (Windows)

```powershell
# Unsigned layout / tooling smoke
./scripts/publish-msix.ps1 -Configuration Release -Runtime win-x64

# Ephemeral CN=Glyph self-signed cert + .cer (CI / Developer Mode)
./scripts/publish-msix.ps1 -Configuration Release -Runtime win-x64 -TestSign
```

Output: `artifacts/msix/` (`Glyph.App_*.msix`; with `-TestSign`, also `Glyph.CI.TestSign.cer`).

## Sideload verify checklist (Windows interactive)

On a machine with **Developer Mode** enabled:

```powershell
./scripts/install-msix-test.ps1 -PackageDir artifacts/msix -Force
# later / without reinstall:
./scripts/install-msix-test.ps1 -VerifyOnly -ProbeUserDefaults -OpenDefaultApps
```

Then manually confirm:

1. Settings → Apps → Default apps lists Glyph for `.pdf` / declared image types (or Open with → Glyph).
2. Double-click a sample `.pdf` and a sample image opens in Glyph.
3. Re-run `-VerifyOnly` after reboot if associations look stale.

Until those steps pass on a real Windows host, keep F01-06/07 **Deferred**.

Full interactive proof checklist (associations + M1/M2 screenshots + M3 §11 DnD
recording + Store signing / ADR-015 escalate): [`INTERACTIVE_VERIFY.md`](INTERACTIVE_VERIFY.md).

## Production / Store signing (open)

Test certs are for CI and Developer Mode only. Closing the M9 installer gate for clean-machine installs needs one of:

| Option | Notes |
| --- | --- |
| **A. Code-signing cert** | EV/OV Authenticode; sign the `.msix` outside CI secrets or via approved secret store |
| **B. Microsoft Store** | Partner Center submission; Store handles distribution signing |
| **C. Stay sideload-only for Preview** | Document Developer Mode requirement; keep associations Deferred |

Escalate before wiring production secrets or changing Publisher identity.

## Related paths

| Path | Role |
| --- | --- |
| `src/Glyph.App/Package.appxmanifest` | Identity, capabilities, file types |
| `scripts/publish-msix.ps1` | Layout + optional `-TestSign`; copies `THIRD_PARTY_NOTICES.md` + vendor notices |
| `scripts/install-msix-test.ps1` | Trust cert, sideload, association probe |
| `THIRD_PARTY_NOTICES.md` | Redistributable attributions (PDFium, Magick.NET, …) |
| `src/Glyph.Core/Documents/PackagingDeferredPolicy.cs` | Shipped / deferred flags |
| Crash recovery | `%LocalAppData%\Glyph\recovery\` via `ICrashRecoveryStore` |
