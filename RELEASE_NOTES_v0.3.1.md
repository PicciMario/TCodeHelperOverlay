# TCode Launchpad v0.3.1

## Highlights

- Remote data loading: transaction data is now downloaded from GitHub and cached locally instead of being bundled with the executable.
- Multi-monitor support: the launcher overlay always opens on the screen where the cursor was when the hotkey was pressed.
- Pip-Boy UI theme: full phosphor-green retro-futuristic visual overhaul with CRT scanlines, semi-transparent background, and animated glow on selected rows.

## New Features

### Remote data cache
- `data.json` is fetched from `https://raw.githubusercontent.com/PicciMario/TCodeHelperOverlay/refs/heads/master/data.json`.
- Cache is stored at `%LOCALAPPDATA%\TCodeLaunchpad\Cache\data.json`.
- Cache age is checked every time the launcher UI is shown; re-download is triggered automatically if older than 24 hours.
- If a download fails, the existing cached file is kept and used without disruption.
- Bottom-right corner shows full cache file path and cache age.
- Clicking the age text forces an immediate re-download.

### Multi-monitor fix
- `GetCursorPos` is called at hotkey time to capture which screen the mouse is on.
- The overlay window is repositioned and resized to that monitor's bounds before becoming visible.
- Works for both hotkey activation and tray-icon activation.

### Pip-Boy UI theme
- Deep phosphor-green radial gradient background with semi-transparent panels.
- All text uses a monospaced terminal font (Cascadia Mono / Consolas fallback).
- CRT scanline pattern overlaid across the full window.
- Selected result row glows with a bright green border and a pulsing opacity animation.

## Validation

- Build: `dotnet build TCodeLaunchpad.sln -c Release`
- Tests: `dotnet test TCodeLaunchpad.sln`
- Tests passed: 3/3

## Release Assets

Upload these files to GitHub Release `v0.3.1`:

- `TCodeLaunchpad-v0.3.1-win-x64.zip` (self-contained executable bundle)
- Optional: this markdown file content as release description
