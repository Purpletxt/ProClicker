# ProClicker

An advanced auto clicker for Windows 11, with a modern Fluent-style interface.

## Download

Get the latest **ProClicker.exe** from the [Releases page](https://github.com/Purpletxt/ProClicker/releases/latest).
It's a single portable file with nothing to install. Put it in a normal folder like Desktop or Documents (not Program Files) so it can update itself.

Windows may show a SmartScreen warning the first time because the app isn't code-signed. Click **More info** and then **Run anyway**.

## Features

- **Clicker**: interval down to the millisecond, speed presets, random offset, left/right/middle button, single/double/triple clicks, hold time, repeat forever or a set number of times
- **Cursor position**: click at the cursor, a fixed spot, or cycle through a list of click points
- **Key presser**: auto-press any key, with Ctrl/Shift/Alt, its own timing and repeat
- **Record & play**: record mouse and keyboard, play back at any speed and loop
- **Global hotkeys**: F6 to F9 by default, all rebindable
- **Themes**: Light, Dark, Extra Dark, Ocean, Neon and Sakura, plus your Windows accent color
- **Profiles**: save and load setups
- **Extras**: tray mode, always on top, start delay, sounds, collapsible sidebar
- **Automatic updates**: checks GitHub Releases on startup and updates in one click

Settings and profiles are stored in `%APPDATA%\ProClicker`.

## Building from source

No tools to install. `build.bat` uses the C# compiler that comes with Windows (.NET Framework 4).

1. Download or clone this repository.
2. Double-click `build.bat`. It creates `ProClicker.exe`.

| File | What it is |
| --- | --- |
| `ProClicker.cs` | All of the app's code |
| `build.bat` | Builds the exe |
| `publish.bat` | Builds a release and computes its checksum (runs `publish.ps1`) |
| `logo.png` | App logo; build.bat turns it into `icon.ico` |

## Publishing an update

1. In `ProClicker.cs`, raise `AppInfo.Version` (for example `1.0.0` to `1.0.1`).
2. Double-click `publish.bat`.
3. On GitHub, open **Releases** and click **Draft a new release**. Use the tag `v1.0.1` (it must match the version) and attach both files from the `release` folder: `ProClicker.exe` and `ProClicker.exe.sha256`.
4. Click **Publish release**. Everyone using the app gets the update the next time they open it.
