# KnobMixer

Control your Windows volume mixer with a single knob, even inside games.

Built for keyboards with a volume knob (Keychron, etc.). Turn it for global volume as normal. Click it to open a small overlay where you pick an app and turn the knob to change just that app's volume, so you can turn the game up and Discord down without leaving your game.

## How it works

| Action | What happens |
|---|---|
| Turn knob (overlay closed) | Normal global volume (Windows handles it) |
| Click knob | Open overlay: Master + every app playing sound |
| Turn knob | Choose which one to control |
| Click knob | Adjust the selected one (2% per notch) |
| Backspace | Back to choosing (or close, if already choosing) |
| Click knob (while adjusting) | Close overlay |
| Esc | Close overlay from anywhere |

The overlay opens at the top-centre of the monitor your active window is on, never steals focus, and ignores the mouse. It auto-closes after 5 seconds of inactivity.

## Requirements

- Windows 10/11
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- A knob that sends the standard media keys: **click = Volume Mute**, **turn = Volume Up/Down** (the default on most boards)

## Build and run

```
dotnet publish -c Release -o dist
dist\KnobMixer.exe
```

It lives in the system tray (knob icon). Right-click it for **Start with Windows** and **Quit**.

## Limitations

- **Exclusive-fullscreen games** can hide any overlay. Borderless / windowed-fullscreen is fine.
- **Admin windows:** Windows blocks keyboard hooks while an elevated window has focus, so the knob won't respond then unless KnobMixer is also run as admin.
- The click no longer mutes, since it opens the overlay instead.
- Apps only appear once they have an audio stream open (Discord shows up when you are in a call or it is making sound).
- Errors are logged to `%LocalAppData%\KnobMixer\log.txt`.

## How it's built

- `KeyboardHook.cs`: global low-level keyboard hook (works regardless of focus)
- `Controller.cs`: the click / turn / backspace state machine
- `AudioModel.cs`: per-app volume via [NAudio](https://github.com/naudio/NAudio) (Windows Core Audio sessions)
- `OverlayWindow.cs`: the no-focus, always-on-top WPF overlay

## License

MIT
