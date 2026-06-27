# chromata

A lightweight Windows 11 screen colour picker. Press a global shortcut, the screen
freezes (Snipping Tool style), you click a pixel, and its hex colour is copied to the
clipboard.

## How it works

Chromata runs as a small **resident tray app**. It registers its global hotkey
(`Ctrl+Alt+C`) in-process, so activation is instant. On the hotkey (or a double-click of
the tray icon) it:

1. Captures the whole virtual desktop into a frozen image (all monitors).
2. Shows a full-screen overlay with a crosshair and a magnifier loupe (live hex / `rgb()`).
3. On **left-click**, copies `#RRGGBB` to the clipboard and shows a brief confirmation.
4. **Esc** / **right-click** / clicking away cancels.

The tray menu offers *Pick a colour*, *Recent colours*, *Settings…*,
*Check for updates…*, *Start with Windows*, and *Exit*.

## Settings

Open **Settings…** from the tray to configure:

- **Global shortcut** — rebind the pick hotkey (must include Ctrl or Alt). Applied
  immediately; reverts if the combo is already taken by another app.
- **Copy format** — what lands on the clipboard: `#RRGGBB` (upper/lower), `rgb(r, g, b)`,
  or `hsl(h, s%, l%)`.
- **Recent colours** — your last 16 picks, also available from the tray submenu (click to
  re-copy in the current format). Clearable.

Settings persist to `%LOCALAPPDATA%\Chromata\settings.json`.

## Known limitation: no Snip-style system hotkey

Snipping Tool's `Win+Shift+S` is owned by Windows itself, which launches the tool on
demand. A third-party app can't claim a system hotkey like that. The only no-background
alternative is a Start Menu shortcut with a "Shortcut key" — but Windows *polls* those and
cold-starts the app on each press, which is noticeably laggy (and limited to
`Ctrl+Alt+<key>`). We tried that approach and it was too sluggish, so Chromata instead
stays resident in the tray and registers the hotkey in-process for an instant response.
The trade-off is a small always-running process (and its tray icon).

## Install

Chromata is installed (and updated) via [Velopack](https://velopack.io). Build a release
and run the generated installer:

```powershell
.\scripts\publish.ps1 -Version 0.1.0   # -> releases\Chromata-win-Setup.exe
```

Run `releases\Chromata-win-Setup.exe` to install it for the current user. It self-updates
from then on, and uninstalls via **Windows Settings → Apps**. Use the tray menu's
**Start with Windows** to run it at login. Requires the .NET Windows Desktop Runtime (8.0+).

## Run during development

No install needed — run the latest build directly:

```powershell
dotnet run --project src
dotnet build -c Release        # or build via Chromata.sln
```

## Updates & releases

The tray's **Check for updates…** reads the GitHub Releases feed at `AppInfo.RepoUrl` and
self-updates. (Update/apply only works for builds installed via the Velopack installer, not
a plain `dotnet run`.)

`scripts\publish.ps1 -Version X.Y.Z` builds the installer + update artifacts into
`releases\`; upload them to a GitHub release tagged `vX.Y.Z`. Pushing a `v*` tag also runs
`.github/workflows/release.yml`, which builds and publishes the release automatically.

## Layout

```
chromata/
├─ Chromata.sln
├─ scripts/        publish.ps1   (Velopack release build)
└─ src/            project + WPF source
```
