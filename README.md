# LOQ Keyboard RGB

A small native Windows app that controls the 4‑zone RGB keyboard backlight on the
Lenovo LOQ 15IRX9 (83DV). It should also work on other Lenovo LOQ/Legion laptops that
use the same ITE 4‑zone controller.

Developed by **Salman MP**, Full Stack Developer, DevOps Engineer and Tech Consultant
(Malappuram, Keralam, India).

<p>
<img alt="C# 5" src="https://img.shields.io/badge/C%23-5-68217A?style=for-the-badge&logo=data%3Aimage%2Fsvg%2Bxml%3Bbase64%2CPHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAyNCAyNCIgZmlsbD0ibm9uZSIgc3Ryb2tlPSJ3aGl0ZSIgc3Ryb2tlLXdpZHRoPSIyLjQiIHN0cm9rZS1saW5lY2FwPSJyb3VuZCIgc3Ryb2tlLWxpbmVqb2luPSJyb3VuZCI%2BPHBhdGggZD0iTTggNmwtNiA2IDYgNk0xNiA2bDYgNi02IDZNMTQgNGwtNCAxNiIvPjwvc3ZnPg%3D%3D&logoColor=white" />
<img alt=".NET Framework 4.8" src="https://img.shields.io/badge/.NET%20Framework-4.8-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" />
<img alt="Windows Forms" src="https://img.shields.io/badge/Windows%20Forms-UI-0078D4?style=for-the-badge&logo=data%3Aimage%2Fsvg%2Bxml%3Bbase64%2CPHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAyNCAyNCIgZmlsbD0ibm9uZSIgc3Ryb2tlPSJ3aGl0ZSIgc3Ryb2tlLXdpZHRoPSIyLjIiPjxyZWN0IHg9IjIuNSIgeT0iNCIgd2lkdGg9IjE5IiBoZWlnaHQ9IjE2IiByeD0iMiIvPjxwYXRoIGQ9Ik0yLjUgOWgxOSIgLz48Y2lyY2xlIGN4PSI2IiBjeT0iNi41IiByPSIuNiIgZmlsbD0id2hpdGUiLz48L3N2Zz4%3D&logoColor=white" />
<img alt="GitHub Actions" src="https://img.shields.io/badge/GitHub%20Actions-CI/CD-2088FF?style=for-the-badge&logo=githubactions&logoColor=white" />
<img alt="Windows 10/11" src="https://img.shields.io/badge/Windows-10%20|%2011-0078D4?style=for-the-badge&logo=data%3Aimage%2Fsvg%2Bxml%3Bbase64%2CPHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAyNCAyNCIgZmlsbD0ibm9uZSIgc3Ryb2tlPSJ3aGl0ZSIgc3Ryb2tlLXdpZHRoPSIyLjIiPjxyZWN0IHg9IjIuNSIgeT0iNCIgd2lkdGg9IjE5IiBoZWlnaHQ9IjE2IiByeD0iMiIvPjxwYXRoIGQ9Ik0yLjUgOWgxOSIgLz48Y2lyY2xlIGN4PSI2IiBjeT0iNi41IiByPSIuNiIgZmlsbD0id2hpdGUiLz48L3N2Zz4%3D&logoColor=white" />
</p>

## Tech stack

<p>
<img alt="C# 5" src="https://img.shields.io/badge/C%23-5-68217A?style=for-the-badge&logo=data%3Aimage%2Fsvg%2Bxml%3Bbase64%2CPHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAyNCAyNCIgZmlsbD0ibm9uZSIgc3Ryb2tlPSJ3aGl0ZSIgc3Ryb2tlLXdpZHRoPSIyLjQiIHN0cm9rZS1saW5lY2FwPSJyb3VuZCIgc3Ryb2tlLWxpbmVqb2luPSJyb3VuZCI%2BPHBhdGggZD0iTTggNmwtNiA2IDYgNk0xNiA2bDYgNi02IDZNMTQgNGwtNCAxNiIvPjwvc3ZnPg%3D%3D&logoColor=white" />
<img alt=".NET Framework 4.8" src="https://img.shields.io/badge/.NET%20Framework-4.8-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" />
<img alt="Windows Forms" src="https://img.shields.io/badge/Windows%20Forms-UI-0078D4?style=for-the-badge&logo=data%3Aimage%2Fsvg%2Bxml%3Bbase64%2CPHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAyNCAyNCIgZmlsbD0ibm9uZSIgc3Ryb2tlPSJ3aGl0ZSIgc3Ryb2tlLXdpZHRoPSIyLjIiPjxyZWN0IHg9IjIuNSIgeT0iNCIgd2lkdGg9IjE5IiBoZWlnaHQ9IjE2IiByeD0iMiIvPjxwYXRoIGQ9Ik0yLjUgOWgxOSIgLz48Y2lyY2xlIGN4PSI2IiBjeT0iNi41IiByPSIuNiIgZmlsbD0id2hpdGUiLz48L3N2Zz4%3D&logoColor=white" />
<img alt="GDI+" src="https://img.shields.io/badge/GDI+-graphics-2B7A78?style=for-the-badge&logo=data%3Aimage%2Fsvg%2Bxml%3Bbase64%2CPHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAyNCAyNCIgZmlsbD0ibm9uZSIgc3Ryb2tlPSJ3aGl0ZSIgc3Ryb2tlLXdpZHRoPSIyLjIiPjxyZWN0IHg9IjMiIHk9IjMiIHdpZHRoPSIxOCIgaGVpZ2h0PSIxOCIgcng9IjMiLz48Y2lyY2xlIGN4PSI5IiBjeT0iOSIgcj0iMiIgZmlsbD0id2hpdGUiLz48cGF0aCBkPSJNMjEgMTZsLTUtNS05IDEwIi8%2BPC9zdmc%2B&logoColor=white" />
<img alt="Win32 API" src="https://img.shields.io/badge/Win32%20API-P/Invoke-3A3A3A?style=for-the-badge&logo=data%3Aimage%2Fsvg%2Bxml%3Bbase64%2CPHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAyNCAyNCIgZmlsbD0ibm9uZSIgc3Ryb2tlPSJ3aGl0ZSIgc3Ryb2tlLXdpZHRoPSIyLjIiIHN0cm9rZS1saW5lY2FwPSJyb3VuZCI%2BPGNpcmNsZSBjeD0iMTIiIGN5PSIxMiIgcj0iMy4yIi8%2BPHBhdGggZD0iTTEyIDIuNXYzTTEyIDE4LjV2M00yLjUgMTJoM00xOC41IDEyaDNNNS4zIDUuM2wyLjEgMi4xTTE2LjYgMTYuNmwyLjEgMi4xTTUuMyAxOC43bDIuMS0yLjFNMTYuNiA3LjRsMi4xLTIuMSIvPjwvc3ZnPg%3D%3D&logoColor=white" />
<img alt="USB HID" src="https://img.shields.io/badge/USB%20HID-feature%20reports-E4572E?style=for-the-badge&logo=data%3Aimage%2Fsvg%2Bxml%3Bbase64%2CPHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAyNCAyNCIgZmlsbD0ibm9uZSIgc3Ryb2tlPSJ3aGl0ZSIgc3Ryb2tlLXdpZHRoPSIyLjIiIHN0cm9rZS1saW5lY2FwPSJyb3VuZCI%2BPHJlY3QgeD0iNiIgeT0iNiIgd2lkdGg9IjEyIiBoZWlnaHQ9IjEyIiByeD0iMS41Ii8%2BPHBhdGggZD0iTTkgMnY0TTE1IDJ2NE05IDE4djRNMTUgMTh2NE0yIDloNE0yIDE1aDRNMTggOWg0TTE4IDE1aDQiLz48L3N2Zz4%3D&logoColor=white" />
<img alt="WASAPI" src="https://img.shields.io/badge/WASAPI-audio%20capture-C2185B?style=for-the-badge&logo=data%3Aimage%2Fsvg%2Bxml%3Bbase64%2CPHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAyNCAyNCIgZmlsbD0ibm9uZSIgc3Ryb2tlPSJ3aGl0ZSIgc3Ryb2tlLXdpZHRoPSIyLjIiIHN0cm9rZS1saW5lY2FwPSJyb3VuZCIgc3Ryb2tlLWxpbmVqb2luPSJyb3VuZCI%2BPHBhdGggZD0iTTQgOWg0bDUtNHYxNGwtNS00SDR6Ii8%2BPHBhdGggZD0iTTE2LjUgOC41YTUgNSAwIDAgMSAwIDdNMTkgNmE4LjUgOC41IDAgMCAxIDAgMTIiLz48L3N2Zz4%3D&logoColor=white" />
<img alt="WMI" src="https://img.shields.io/badge/WMI-Lenovo%20events-6A1B9A?style=for-the-badge&logo=data%3Aimage%2Fsvg%2Bxml%3Bbase64%2CPHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAyNCAyNCIgZmlsbD0ibm9uZSIgc3Ryb2tlPSJ3aGl0ZSIgc3Ryb2tlLXdpZHRoPSIyLjIiIHN0cm9rZS1saW5lY2FwPSJyb3VuZCI%2BPGNpcmNsZSBjeD0iMTIiIGN5PSIxMiIgcj0iMy4yIi8%2BPHBhdGggZD0iTTEyIDIuNXYzTTEyIDE4LjV2M00yLjUgMTJoM00xOC41IDEyaDNNNS4zIDUuM2wyLjEgMi4xTTE2LjYgMTYuNmwyLjEgMi4xTTUuMyAxOC43bDIuMS0yLjFNMTYuNiA3LjRsMi4xLTIuMSIvPjwvc3ZnPg%3D%3D&logoColor=white" />
<img alt="PowerShell" src="https://img.shields.io/badge/PowerShell-tooling-012456?style=for-the-badge&logo=data%3Aimage%2Fsvg%2Bxml%3Bbase64%2CPHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAyNCAyNCIgZmlsbD0ibm9uZSIgc3Ryb2tlPSJ3aGl0ZSIgc3Ryb2tlLXdpZHRoPSIyLjQiIHN0cm9rZS1saW5lY2FwPSJyb3VuZCIgc3Ryb2tlLWxpbmVqb2luPSJyb3VuZCI%2BPHBhdGggZD0iTTQgN2w2IDUtNiA1TTEyIDE4aDgiLz48L3N2Zz4%3D&logoColor=white" />
<img alt="GitHub Actions" src="https://img.shields.io/badge/GitHub%20Actions-CI/CD-2088FF?style=for-the-badge&logo=githubactions&logoColor=white" />
<img alt="Git" src="https://img.shields.io/badge/Git-version%20control-F05032?style=for-the-badge&logo=git&logoColor=white" />
<img alt="Windows 10/11" src="https://img.shields.io/badge/Windows-10%20|%2011-0078D4?style=for-the-badge&logo=data%3Aimage%2Fsvg%2Bxml%3Bbase64%2CPHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAyNCAyNCIgZmlsbD0ibm9uZSIgc3Ryb2tlPSJ3aGl0ZSIgc3Ryb2tlLXdpZHRoPSIyLjIiPjxyZWN0IHg9IjIuNSIgeT0iNCIgd2lkdGg9IjE5IiBoZWlnaHQ9IjE2IiByeD0iMiIvPjxwYXRoIGQ9Ik0yLjUgOWgxOSIgLz48Y2lyY2xlIGN4PSI2IiBjeT0iNi41IiByPSIuNiIgZmlsbD0id2hpdGUiLz48L3N2Zz4%3D&logoColor=white" />
</p>

| Technology | Used for |
|---|---|
| **C# 5 / .NET Framework 4.8** | The whole app. It's built with the C# compiler that comes with Windows, so you don't need an SDK |
| **Windows Forms + GDI+** | The dark UI, the animated keyboard preview, owner‑drawn controls, and the generated tray icons |
| **Win32 API (P/Invoke)** | HID and SetupAPI device access, global hotkeys, the shell hook (taskbar flashes), foreground‑app tracking, idle time, display power notifications, DWM dark title bar |
| **USB HID feature reports** | Talking to the ITE 4‑zone keyboard controller (`048D:C993`, 33‑byte report) |
| **WASAPI (Core Audio)** | Loopback capture of what the PC is playing, plus an FFT for the audio visualizer |
| **GDI** | Fast screen downscaling for the ambient effect |
| **WMI (System.Management)** | Lenovo Fn+Q power‑mode events (`LENOVO_GAMEZONE_SMART_FAN_MODE_EVENT`) |
| **Performance counters (System.Diagnostics)** | GPU load for the system monitor effect (the same "GPU Engine" counters Task Manager uses) |
| **PowerShell** | The icon generator (`tools/make-icon.ps1`) and the CI version stamping |
| **GitHub Actions** | Building on every push, and publishing releases on version tags |
| **Git** | Version control, one commit per feature |
| **Windows 10 / 11** | Target platform. Per‑user install, no admin rights needed |

## Supported keyboards

| Keyboard | Status |
|---|---|
| 4‑zone RGB (ITE `048D:C9xx`, usage page `0xFF89`, 33‑byte report), e.g. LOQ 15IRX9 | ✅ Supported (developed and tested on this laptop) |
| Per‑key RGB ("Spectrum", 960‑byte report), some Legion models | ⚠️ Detected, not supported yet. The app shows this in its status line |
| White‑only backlight | ❌ Not supported. Lenovo's driver controls it (Fn+Space) |

The other keyboard types are left out on purpose: their protocols couldn't be tested on this hardware,
and sending untested commands to keyboard firmware isn't safe.

## Download & install

Get the latest version from **[Releases](https://github.com/salmankavanur/LOQ-Keyboard-RGB/releases/latest)**:

- **`LOQKeyboardRGB-Setup-<version>.exe`** (recommended): installs for your user account with no admin rights.
  It adds a Start menu entry, an optional desktop shortcut and *Start with Windows*, and an uninstaller in
  Windows Settings → Apps. Running a newer setup updates the app in place and keeps your settings.
- **`LOQKeyboardRGB-<version>-portable.exe`**: a single exe that runs from anywhere with no installation.

**Automatic updates** (from v1.1.0): about once a day the app checks GitHub Releases. While it's hidden in
the tray it downloads, verifies and installs a new version by itself, then restarts in the tray. While the
window is open it shows an **Update to vX** button instead. Every download is checked against its SHA‑256
checksum before it's installed, and your settings are kept. You can switch this off in **Settings → Updates**.

The files aren't code‑signed, so Windows SmartScreen may show *Windows protected your PC*.
Click **More info → Run anyway**. Each release lists SHA‑256 checksums so you can verify the files.

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
LOQKeyboardRGB.exe --exit                    close the running app (used by the setup)
LOQKeyboardRGB.exe --apply-update <file>     install a downloaded update (new setup or newer portable exe)
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

## Project layout

| File | Purpose |
|---|---|
| `src/Hid.cs` | Finding the keyboard and sending HID feature reports |
| `src/Model.cs` | Effects, the lighting state and the 33‑byte report, settings, profiles |
| `src/Engine.cs` | Lighting engine: sends hardware effects once, streams software effects, handles flashes |
| `src/Audio.cs`, `Ambient.cs`, `Monitor.cs`, `Animations.cs` | Software effects |
| `src/MainForm.cs`, `KeyboardView.cs` | Main window and the animated keyboard preview |
| `src/Settings.cs` | Settings window |
| `src/AppRules.cs`, `Hotkeys.cs`, `PowerMode.cs` | App profiles, global hotkeys, Lenovo Fn+Q mode |
| `src/Installer.cs`, `Updates.cs` | Per‑user install/uninstall, automatic updates from GitHub Releases |
| `src/About.cs`, `AssemblyInfo.cs` | About screen and developer details, version info |

## Build

Run `build.cmd` (or `build.cmd path\to\out.exe`). It uses the C# compiler that comes with Windows
(.NET Framework 4), so you don't need an SDK. `tools\make-icon.ps1` regenerates `assets\app.ico`.

## Releases

GitHub Actions (`.github/workflows/build.yml`) builds every push and pull request. Pushing a tag such as
`v1.1.0` stamps that version and the repository into the exe, builds the setup with Inno Setup
(`installer/setup.iss`), and publishes a GitHub Release with the setup, the portable exe and their checksums.

To build the setup locally: run `build.cmd`, then `ISCC.exe installer\setup.iss /DAppVersion=1.1.0`
(you need [Inno Setup 6](https://jrsoftware.org/isinfo.php)). The output goes to `dist\`. Release builds can check for updates from **About → Check for updates**. It only contacts
GitHub when you click it.

## Notes

- Fn+Space still changes brightness in hardware.
- Lenovo Vantage can sometimes reset the lighting when it starts or when you change power modes.
  The app applies your lighting again after sleep and unlock, and you can click any setting to apply it again.
