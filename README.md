# Chromata

A lightweight screen colour picker for Windows 11. Hit a hotkey, the screen freezes, click
any pixel, and its hex colour is on your clipboard.

## Install

Download **Chromata-win-Setup.exe** from the
[latest release](https://github.com/ArcticGizmo/chromata/releases/latest) and run it. That's
it — it installs for the current user, self-updates, and uninstalls from
**Windows Settings → Apps**. Nothing else to install; the runtime is bundled.

Prefer no installer? Grab **Chromata-win-Portable.zip** from the same page and run
`Chromata.exe`.

## Picking a colour

Press **Ctrl+Alt+C** (or double-click the tray icon). The screen freezes with a crosshair
and a magnifier loupe showing the live colour, then:

- **Left-click** — copies the colour and shows a brief confirmation.
- **Esc**, **right-click**, or **click away** — cancels.

Every pick is also saved to **Recent colours** (your last 16) in the tray menu — click one
to re-copy it.

## Settings

Open **Settings…** from the tray icon to configure:

- **Global shortcut** — rebind the hotkey (must include Ctrl or Alt). Reverts if another
  app already owns the combo.
- **Copy format** — `#RRGGBB` (upper/lower), `rgb(r, g, b)`, or `hsl(h, s%, l%)`.
- **Recent colours** — view or clear your history.
- **Start with Windows** — launch Chromata at login.

Settings live in `%LOCALAPPDATA%\Chromata\settings.json`.

## Updates

**Check for updates…** in the tray menu pulls the newest release from GitHub and updates in
place. (Only installed builds update — a portable copy or a dev run won't.)

## Why it lives in the tray

Windows reserves Snipping Tool's `Win+Shift+S` for itself, so a third-party app can't claim
a system-wide hotkey like it. The alternative — a Start Menu shortcut key — cold-starts the
app on every press and feels sluggish. So Chromata instead stays resident in the tray and
registers its hotkey in-process, making activation instant. The cost is a small always-on
process and its tray icon.

---

## Development

Run straight from source — no install needed:

```powershell
dotnet run --project src
dotnet build -c Release      # or open Chromata.sln
```

### Cutting a release

`publish.bat` builds a self-contained installer plus update artifacts into `releases\`
using [Velopack](https://velopack.io):

```bat
publish.bat 0.1.0
```

Upload everything in `releases\` to a GitHub release tagged `v0.1.0`. Or just push a `v*`
tag — `.github/workflows/release.yml` builds and publishes the release automatically.

> Packing uses the `vpk` CLI (run via `dnx`). If it's missing:
> `dotnet tool install -g vpk`.
