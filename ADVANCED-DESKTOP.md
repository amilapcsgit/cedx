# CEDX advanced desktop

Branch: `cedxadnacedexe`, based on `cedxadvanced`.

## Start

1. Download or clone this branch and extract the complete repository.
2. Install the Microsoft **.NET 8 SDK for Windows x64** if not already installed.
3. Double-click **Build-and-Run-CEDX.bat**. The first build needs internet access for NuGet restore.
4. Choose the folder containing your collector TXT reports. CEDX remembers it for the next launch.

The executable and its supporting files are in `artifacts\cedxadnacedexe`. Keep this complete folder together. This self-contained Windows x64 package does not require the .NET runtime on the destination PC. Re-run the BAT after source updates; close the running application before rebuilding. Build errors are retained in `artifacts\build.log`. Use `--build-only` for unattended compilation.

## Workspace

- Alien/tech dark workspace with cyan, green, or violet accents, adjustable tile density, hidden/visible filters, and draggable detail-panel divider.
- The inspector moves below the asset list when horizontal space is limited. Tiles and the inventory table share the same selection and filter scope.
- Select an asset, choose a category and section, then search within the section. FIELDS exposes structured values, SECTION preserves the section text, SOURCE TXT preserves the full report.
- Accounts, administrators, license sources, activation, physical disks, adapters, IP configuration, firewall, Defender, Office diagnostics, printers and other collector extensions remain available, including unknown future `=== Section ===` blocks.
- Copy visible fields or export the displayed section as CSV. Ctrl+C also works on selected grid cells. Open original TXT launches Notepad.
- Full Windows product keys are masked in the inspector and detail export until explicitly revealed. Reveal resets when changing asset. The original TXT itself is not encrypted or redacted.
- Search uses all collected indexed fields with AND terms, and supports quoted phrases. Product keys are excluded from the search index. Unknown RAM/disk measurements have an explicit include/exclude control. Invalid numeric ranges show a validation message.
- Filtered counts and selection follow the visible assets. No results clears the inspector. Refresh preserves the selected asset when still visible.
- AnyDesk launch requires an ID and a locally registered AnyDesk handler. WinRM is a copyable command, not an automatically executed connection.
- Nmap is optional and must be installed separately. Scan operates only on valid IP addresses in the filtered set. Online/offline reflects Nmap discovery, not a guarantee of application availability. No scan runs automatically.
- F5 reloads reports; Ctrl+F focuses fleet search. File changes require Refresh.

Preferences are stored under `%LOCALAPPDATA%\cedx\workspace.json`. Reset layout restores appearance; deleting this file also resets the saved folder and window geometry. TXT files are read locally and are not uploaded by this application. Do not commit asset reports or license keys to the repository.

## Verification

`dotnet run --project tests/Cedx.Tests` verifies legacy/extended parsing, multiline records, nested Office banners, search, key masking, measurement filters and status notifications.

`dotnet run --project tests/Cedx.WpfSmoke` runs on Windows, exercises selection and responsive layout, and captures synthetic-data screenshots in `artifacts/ui-smoke`. It uses no real asset data. The GitHub workflow also runs the same BAT in build-only mode. These checks do not replace live AnyDesk/Nmap integration testing on your network.
