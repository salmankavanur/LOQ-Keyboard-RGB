# LOQ Keyboard RGB

A small native Windows app that controls the 4‑zone RGB keyboard backlight on the
Lenovo LOQ 15IRX9 (83DV). It should also work on other Lenovo LOQ/Legion laptops that
use the same ITE 4‑zone controller.

Developed by **Salman MP**, Full Stack Developer, DevOps Engineer and Tech Consultant
(Malappuram, Keralam, India).

## Install / run

Run `LOQKeyboardRGB.exe`. To install it for your user account, open **Settings → Installation → Install for this user**,
or run `LOQKeyboardRGB.exe --install`. Installing adds a Start menu entry and an uninstaller under
Windows Settings → Apps. You don't need admin rights.

## Lighting

- **Hardware effects:** Off, Static, Breath, Wave (left→right / right→left), Smooth colour cycle
- **Software effects** (rendered by the app about 30 times a second):
  - **Audio**: the zones react to whatever is playing, from bass (zone 1) to treble (zone 4)
  - **Ambient**: each zone follows the colour of the matching quarter of your screen
  - **Monitor**: CPU, RAM, GPU and battery shown green → yellow → red
  - **Drift / Fade / Strobe**: animations built from your four zone colours
- 4 speeds, 2 brightness levels, and a colour for each zone (swatches, picker, hex)
- A live animated preview. Click a zone to select it, Ctrl+click to select several, and double‑click to pick its colour.
- Profiles, with presets: Ocean, Sunset, Cyber, Rainbow Wave, Breathing Red, Pure White, Police, Music

## Automation (Settings)

| Feature | What it does |
|---|---|
| Notifications | Flashes when an app wants attention (its taskbar button flashes), and on low battery (20% / 10%) |
| Power & schedule | On battery: keep, dim or turn off. Turn off when idle or when the screen is off. Night profile between two times |
| Lenovo power mode | A profile for each Fn+Q mode (Quiet / Balanced / Performance) |
| App profiles | Uses a profile while a chosen app is in front, e.g. `valorant` → Rainbow Wave |
| Hotkeys | Ctrl+Alt+L on/off, Ctrl+Alt+←/→ profiles, Ctrl+Alt+↑/↓ brightness, Ctrl+Alt+E next effect (you can change all of these) |

Rules combine in this order: lights you turned off stay off, then idle / screen off, then battery,
then app profile, then power‑mode profile, then night profile, then your own lighting.
If you change the lighting yourself, any active automatic profile pauses until its next trigger.

## Command line

```
LOQKeyboardRGB.exe --toggle | --on | --off
LOQKeyboardRGB.exe --profile "Ocean"
LOQKeyboardRGB.exe --color FF3300            all zones, static
LOQKeyboardRGB.exe --flash 00FF00 --times 3  e.g. after a build: npm run build && LOQKeyboardRGB --flash 00FF00
LOQKeyboardRGB.exe --tray                    start hidden in the tray
LOQKeyboardRGB.exe --install | --uninstall
```

If the app is already running, the command goes to it. Exit code 0 means OK, 1 means the keyboard
wasn't found, and 2 means a bad argument.

## How it works

The keyboard controller is an ITE USB HID device (`048D:C993`). Its HID collection on usage page
`0xFF89` takes a 33‑byte feature report:

```
CC 16 <effect> <speed 1-4> <brightness 0-2> <R G B zone1..zone4> 00 <wave LTR> <wave RTL> 00…
effect: 01 static, 03 breath, 04 wave, 06 smooth   (brightness 0 = off)
```

Software effects send a stream of static‑colour reports. Audio uses WASAPI loopback capture and an FFT.
Ambient uses a GDI screen downscale. Power‑mode changes arrive as Lenovo's
`LENOVO_GAMEZONE_SMART_FAN_MODE_EVENT` WMI event. Settings and profiles are stored in `%APPDATA%\LOQ Keyboard RGB\`.

## Build

Run `build.cmd` (or `build.cmd path\to\out.exe`). It uses the C# compiler that comes with Windows
(.NET Framework 4), so you don't need an SDK. `tools\make-icon.ps1` regenerates `assets\app.ico`.

## Releases

GitHub Actions (`.github/workflows/build.yml`) builds every push and pull request. Pushing a tag such as
`v1.1.0` stamps that version and the repository into the exe and publishes a GitHub Release with the
exe attached. Release builds can check for updates from **About → Check for updates**. It only contacts
GitHub when you click it.

## Notes

- Fn+Space still changes brightness in hardware.
- Lenovo Vantage can sometimes reset the lighting when it starts or when you change power modes.
  The app applies your lighting again after sleep and unlock, and you can click any setting to apply it again.
