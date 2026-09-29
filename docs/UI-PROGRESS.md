# UI checkpoints: cedexadvancesui

Base: `cedxadnacedexe` at `6e8d262a8c8b09ac3d03a68c44cb49e7c8d43f4e`.

The approved reference is the older CEDX screenshot: midnight blue surfaces, blue/violet glass panels, teal users and primary actions, blue IP addresses, magenta remote IDs, larger hostnames.

## Targets

1. **Complete, Windows validation passed (`cbeb1e3`, run `36573115411`):** remove unused white client border. Apply the Window style explicitly and remove the 20-DIP root margin. Screenshots must no longer override production margins. Test the shown maximized window and render at 1920 x 1080.
2. **Complete, Windows validation passed (`7c484d1`, run `36573607274`):** restore the reference palette through reusable resources, readable text sizes, visible keyboard focus and consistent dark controls.
3. **Complete, Windows validation passed (`c1e1a57`, run `36574605778`):** compact useful tiles, recognizable icons, real filtered inventory indicators, wide and compact layout checks, refreshed screenshots and README.

Each target is a separate commit on this branch. Database, import and assignment behavior remain covered by the existing regression suite. No uploaded screenshots or real inventory details are committed.

## Target 3 details

Filter-aware metrics and OS bar chart use actual visible scans. Unknown disk capacity hides the free-space meter; a missing AnyDesk ID never means offline. Disk warnings use the configured threshold. Tiles restore copy-user/copy-IP actions and magenta remote IDs. Navigation highlights the current scope. Full-HD and 125% logical sizing are captured without changing production margins.

## Same-report comparison follow-up

4. **Complete, Windows validation passed (`e45cc0a`, run `36592611488`):** read raw scans/history as explicit UTF-8 byte sequences from SQLite. Legacy monitor EDID strings contain embedded NUL characters; the previous text read stopped there and hid every subsequent field. Existing TEXT rows and content hashes are preserved. Synthetic regressions cover current scans, unchanged reimport, assignment, history and backup.
5. **Complete, Windows validation passed (`57040c4`, run `36636854399`):** compact label/value inspector, omit empty assignment rows, restore manufacturer, monitor, OS activation/dates, network mode/MAC and software summaries, reduce tile/chrome height.

6. **Complete, Windows validation passed (`57040c4`, run `36636854399`):** end-to-end WPF reproduction showed truncation also occurred during native TEXT writes. Store UTF-8 BLOB parameters and compare stored bytes when a scan hash is unchanged. Reimport repairs truncated current/history rows while preserving identity, assignment and revision count. Recovery of an already truncated row needs its original TXT. The earlier read-only fix was insufficient; 37 Core checks and the real WPF workflow now pass.
7. **Implemented, Windows validation pending:** keep summary metrics in a single row at Full-HD 125% scaling, fit three OS chart entries without a needless scrollbar, label C: values as free space, and keep license masking consistent in the compact overview. Documentation screenshots follow after validation.
