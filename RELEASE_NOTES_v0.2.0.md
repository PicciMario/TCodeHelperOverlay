# TCode Launchpad v0.2.0

## Highlights

- Added score-breakdown debug info in the details panel for the currently selected transaction.
- Moved debug info to a dedicated bottom area in the details panel with a smaller, darker style.
- Pressing Enter now copies `/n<transaction_code>` to clipboard.
- Added a tray toast notification after copy/hide to confirm clipboard content.

## Behavior Details

- Search scoring debug line now reports contribution buckets:
  - code prefix / code contains
  - name prefix / name contains
  - keyword exact / keyword contains
  - long description contains
  - prefix-hit flag and final score
- Copy flow now:
  1. selects active transaction code
  2. copies `/nCODE` to clipboard
  3. hides launcher
  4. shows confirmation toast

## Validation

- Build: `dotnet build TCodeLaunchpad.sln -c Debug`
- Tests: `dotnet test tests/TCodeLaunchpad.Core.Tests/TCodeLaunchpad.Core.Tests.csproj -c Debug`
- Tests passed: 3/3

## Release Assets

Upload these files to GitHub Release `v0.2.0`:

- `TCodeLaunchpad-v0.2.0-win-x64.zip` (self-contained executable bundle)
- Optional: this markdown file content as release description
