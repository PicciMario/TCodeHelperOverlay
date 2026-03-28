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

- `data.json` in workspace root
- Runtime resolution checks:
1. current working directory (`data.json`)
1. executable folder (`data.json`)
1. workspace root (development fallback)

## Notes

- Press `Ctrl+Space` to toggle launcher.
- If `Ctrl+Space` is already in use by another app, the launcher still runs from tray menu.
- Press `Enter` to copy selected SAP code to clipboard.
- If blur is unavailable on the OS, the app falls back to a translucent overlay.

## Troubleshooting

- If double-clicking the exe appears to do nothing, check the Windows system tray: the app starts hidden by design.
- Double-clicking the exe again now activates the already-running instance.
- Ensure `data.json` is either:
1. in the same folder as `TCodeLaunchpad.App.exe`
1. in your current working directory when launching from terminal
- If `Ctrl+Space` does not open the launcher, the hotkey is likely reserved by another app. Use the tray icon menu (`Open`).
