# UI checkpoints: cedexadvancesui

Base: `cedxadnacedexe` at `6e8d262a8c8b09ac3d03a68c44cb49e7c8d43f4e`.

The approved reference is the older CEDX screenshot: midnight blue surfaces, blue/violet glass panels, teal users and primary actions, blue IP addresses, magenta remote IDs, larger hostnames.

## Targets

1. **Complete, Windows validation passed (`cbeb1e3`, run `36573115411`):** remove unused white client border. Apply the Window style explicitly and remove the 20-DIP root margin. Screenshots must no longer override production margins. Test the shown maximized window and render at 1920 x 1080.
2. **Implemented, Windows validation pending:** restore the reference palette through reusable resources, readable text sizes, visible keyboard focus and consistent dark controls.
3. **Pending:** compact useful tiles, recognizable icons, real filtered inventory indicators, wide and compact layout checks, refreshed screenshots and README.

Each target is a separate commit on this branch. Database, import and assignment behavior remain covered by the existing regression suite. No uploaded screenshots or real inventory details are committed.
