# umbra ◐

A tiny tray toggle for the Windows 10/11 light/dark theme, with an optional daily schedule.

One C# file. No dependencies, no installer, no background service. ~10 KB exe, ~10 MB RAM.

## Usage

- **Left click** the ◐ icon next to the clock: switch light ↔ dark
- **Right click**:
  - **Auto**: switch on schedule (default 07:00 light, 19:00 dark)
  - **Edit schedule…**: opens `umbra.ini`
  - **Start with Windows**

A manual toggle stays until the next scheduled switch.

## Build

The C# compiler ships with Windows, so you don't need to install anything:

```
build.cmd
```

This produces `umbra.exe`. Put it anywhere and run it.

## Config

`umbra.ini` is created next to the exe and re-read every 30 seconds:

```ini
light=07:00
dark=19:00
auto=1
```

Night ranges like `light=20:00`, `dark=06:00` also work.

## How it works

umbra writes `AppsUseLightTheme` and `SystemUsesLightTheme` under
`HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize`, then broadcasts
`WM_SETTINGCHANGE` so apps update right away.

> Windows 11 Quick Settings (Win+A) has no public API for custom tiles, so umbra lives in the tray.
> Tip: drag the icon out of the overflow menu (^) to keep it always visible.

## License

MIT
