# CEDX Asset Manager WPF Migration

## Summary

CEDX is being migrated from a Streamlit dashboard into a Windows-native WPF application named `CEDX Asset Manager`.

The first native slice creates:

- a .NET 8 solution,
- a WPF desktop shell,
- a parser-focused core library,
- a parser smoke console,
- async loading from local `.txt` asset files,
- a dense inventory `DataGrid`,
- left-side filters,
- right-side technical details,
- CSV export, copy commands, and AnyDesk URI launch.

The existing Python and PowerShell files are preserved.

## UI Rules

`Uncodixfy.md` is a hard constraint for all UI work.

The WPF app must remain a native operational console:

- no Streamlit UI patterns,
- no hero sections,
- no KPI-card-first dashboard,
- no decorative charts,
- no gradients,
- no glass panels,
- no oversized rounded surfaces,
- no marketing copy,
- no fake control-room visuals.

The default shell uses a graphite dark theme, fixed panels, native controls, simple borders, dense table rows, and `Bahnschrift`.

## Current Architecture

- `src/Cedx.Core`
  - `Models`: typed asset record and child models.
  - `Parsing`: `IAssetParser` and section-aware text parser.
  - `Services`: async file repository and asset folder locator.
- `src/Cedx.App`
  - WPF shell.
  - MVVM view model.
  - command helpers.
  - graphite theme resources.
  - settings defaults.
- `tools/Cedx.ParserSmoke`
  - validates parser behavior against a local asset folder.

The app uses `assets` when present and falls back to `Database` for compatibility with the current local data snapshot.

## Implemented Behavior

- Loads `.txt` asset files asynchronously.
- Reads UTF-8 first and falls back to Latin-1.
- Parses top-level identity, OS, hardware, network, software, security, SMB, disk, and WinRM sections.
- Preserves raw file content in memory.
- Preserves exact important identifiers as found in files.
- Filters in memory without reparsing files.
- Exports filtered core inventory fields to CSV.
- Provides copy actions for hostname, IP, user, AnyDesk ID, and WinRM command.
- Launches AnyDesk through `anydesk:<id>` when an ID exists.

## Next Slices

- Add persisted local JSON settings.
- Add light/system theme switching.
- Add sensitive-field reveal controls for Windows keys and credentials.
- Add full details tabs or expanders for raw file, disks, SMB credentials, printers, local users, and parse warnings.
- Add cancellable ping/Nmap scan service with safe concurrency.
- Add selected-asset text/JSON export.
- Add focused parser tests once a test project is introduced.
