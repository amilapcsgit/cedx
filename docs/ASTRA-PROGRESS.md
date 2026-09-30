# Astra UI checkpoints

Branch: `cedxadvuirefresh`, based on `cedexadvancesui` at `1831c053935806a30d6a8cbf7f22873eb9d27c3f`.

1. Branch and operating model: a second window launched with `astraui.bat`; classic launcher unchanged. Search people, accounts, emails and scan text, then connect using the existing AnyDesk command.
2. Review before import: TXT previews do not write to the database. File rename, validated edit with backup, recoverable removal, duplicate grouping and explicit approval. Separate Astra database and preferences avoid automatic classic-folder imports bypassing review.
3. Native WPF mission-control UI: search-first virtualized results, direct remote access, selected-PC evidence, technical inspector and assignment editor. Archived assets remain recoverable with history.
4. Windows tests, screenshots, documentation and final commit.

Progress: checkpoints 1, 2 and 3 implemented. TXT review service and classic regression suite passed Windows run `36652336211` (`d728fcd`). Alternative window, launcher and end-to-end tests are ready for Windows validation. No personal scans or uploaded screenshots will be committed.
