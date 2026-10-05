# Glyph packaging notes (Milestone 9)

## Current distribution model

Glyph ships **unpackaged** during development (`WindowsPackageType=None`, ADR-006). Local runs use `dotnet build` / Visual Studio F5 against the WinUI project.

## MSIX / installer (target)

Finalize in a packaging pass:

1. Create an MSIX packaging project (or single-project MSIX) that references `Glyph.App`.
2. Declare file type associations for `.pdf` and common image extensions (see ADR-012).
3. Sign with a trusted certificate for clean-machine installs.
4. Optional: sparse package identity for unpackaged Association/Share enhancements.

Until that lands, Explorer double-click / Open With associations remain **Deferred** (ADR-012).

## Crash recovery paths

Recovery snapshots are stored under `%LocalAppData%\Glyph\recovery\` via `ICrashRecoveryStore` / `FileCrashRecoveryStore`. Successful Save should clear the matching snapshot when wired by the document host.

## Accessibility baseline

Shell chrome exposes `AutomationProperties.Name` on the header, main menu, sidebar, and recent-files list. Document panes should continue adding names for toolbar commands as they land.
