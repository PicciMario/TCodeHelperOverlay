# TCode Launchpad

Native Windows launcher for SAP transaction-code lookup.

## Current Status

Implemented MVP includes:
- WPF native app shell (overlay + single search input + result list)
- Global hotkey registration (`Ctrl+Space`)
- Tray icon with menu (`Open`, `Hide`, `Reload Data`, `Exit`)
- Weighted ranking (`prefix first`, then `name/code > keywords > long description`)
- Keyboard flow (`Up/Down` selection, `Enter` copies code to clipboard, `Esc` hides)
- JSON repository mapped to existing `data.json`
- Single-instance process guard
- Core unit tests for ranking behavior

## Prerequisites

This project targets WPF on .NET 8.

Required locally:
- .NET SDK 8.0+

## Build

```powershell
dotnet restore TCodeLaunchpad.sln
dotnet build TCodeLaunchpad.sln -c Release
```

## Build Executable (Single File)

Create a self-contained Windows executable:

```powershell
dotnet publish src/TCodeLaunchpad.App/TCodeLaunchpad.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false
```

Output folder:
- `src/TCodeLaunchpad.App/bin/Release/net8.0-windows/win-x64/publish`

Executable:
- `src/TCodeLaunchpad.App/bin/Release/net8.0-windows/win-x64/publish/TCodeLaunchpad.App.exe`

Optional framework-dependent publish (smaller output, requires installed .NET runtime):

```powershell
dotnet publish src/TCodeLaunchpad.App/TCodeLaunchpad.App.csproj -c Release -r win-x64 --self-contained false
```

## Run

```powershell
dotnet run --project src/TCodeLaunchpad.App/TCodeLaunchpad.App.csproj
```

To run the published executable directly:

```powershell
./src/TCodeLaunchpad.App/bin/Release/net8.0-windows/win-x64/publish/TCodeLaunchpad.App.exe
```

## Data Source

- Remote source:
1. `https://raw.githubusercontent.com/PicciMario/TCodeHelperOverlay/refs/heads/master/data.json`
- Local user cache path (Windows best-practice, user-scoped):
1. `%LOCALAPPDATA%\TCodeLaunchpad\Cache\data.json`
- Download policy:
1. cache age is checked every time the launcher UI is shown
1. if cache is older than 24 hours, the app attempts a re-download
1. if download fails, existing cached file is still used

## Notes

- Press `Ctrl+Space` to toggle launcher.
- If `Ctrl+Space` is already in use by another app, the launcher still runs from tray menu.
- Press `Enter` to copy selected SAP code to clipboard.
- If blur is unavailable on the OS, the app falls back to a translucent overlay.
- Bottom-right debug text shows cache file full path and cache age.
- Click the cache age text to force an immediate refresh attempt.

## Troubleshooting

- If double-clicking the exe appears to do nothing, check the Windows system tray: the app starts hidden by design.
- Double-clicking the exe again now activates the already-running instance.
- Ensure internet access is available on first launch so the cache can be created.
- Cached data is stored in `%LOCALAPPDATA%\TCodeLaunchpad\Cache\data.json`.
- If `Ctrl+Space` does not open the launcher, the hotkey is likely reserved by another app. Use the tray icon menu (`Open`).
