# Astra UI checkpoints

Branch: `cedxadvuirefresh`, based on `cedexadvancesui` at `1831c053935806a30d6a8cbf7f22873eb9d27c3f`.

1. Branch and operating model: a second window launched with `astraui.bat`; classic launcher unchanged. Search people, accounts, emails and scan text, then connect using the existing AnyDesk command.
2. Review before import: TXT previews do not write to the database. File rename, validated edit with backup, recoverable removal, duplicate grouping and explicit approval. Separate Astra database and preferences avoid automatic classic-folder imports bypassing review.
3. Native WPF mission-control UI: search-first compact card grid, direct remote access, selected-PC evidence, technical inspector and assignment editor. Archived assets remain recoverable with history.
4. Windows tests, screenshots, documentation and final commit.

## Checkpoint commits

- `1768ddb`: branch and operating model documented.
- `d728fcd`: reviewed TXT staging, conflict checks, exact backups and recoverable file operations. Windows run `36652336211` passed.
- `4cbfc48`: alternative window, `astraui.bat`, explicit approval, search and source tools. Windows run `36682040902` passed both launchers and Astra integration checks.
- `3c76fbb`: petrol/cyan reference palette, compact cards, vector instruments and history. Validation caught a race between folder refresh and archive commands.
- `4a6d361`: serialize operations, retain failed edits in the editor and save appearance. Windows run `36682681062` passed: 45 Core checks plus classic/Astra WPF flows. Cards measured 259 DIP before the final spacing pass.
- `7f0ca9a`: final spacing, maximum single-result card width, clearer disabled remote buttons, scrolling compact navigation and assignment match evidence. Final [Windows run `36683152963`](https://github.com/amilapcsgit/cedx/actions/runs/36683152963) passed: 45 Core checks, both launchers, classic and Astra WPF flows. Cards measured 261 DIP; five synthetic screenshots cover Full HD, review, assignment, compact mode and 125% scaling.

No personal scans or uploaded screenshots are committed. Final documentation and screenshots are published after the successful code validation.
