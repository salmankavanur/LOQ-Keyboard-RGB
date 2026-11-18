# LOQ Keyboard RGB

A small native Windows app that controls the 4‑zone RGB keyboard backlight on the
Lenovo LOQ 15IRX9 (83DV). It should also work on other Lenovo LOQ/Legion laptops that
use the same ITE 4‑zone controller.

## Run

`bin\LOQKeyboardRGB.exe`. You don't need to install anything or run it as admin.

## Features

- Effects: Off, Static, Breath, Wave (left→right / right→left), Smooth (colour cycle)
- 4 speeds and 2 brightness levels
- A colour for each zone. Click a zone on the live preview or click the zone buttons.
  Ctrl+click selects several zones. Double‑click a zone, or click a selected zone button again, to open the colour picker.
- Quick colour swatches, hex input, and "Copy to all zones"
- Profiles: save, load and delete. It comes with 6 presets.
- Tray icon that shows the current colours, with quick effect, brightness and profile menus
- Start with Windows (it starts hidden in the tray)
- Re-applies your lighting after sleep and after you unlock the PC
- About screen (ⓘ button in the header, or **About** in the tray menu)

Developed by **Salman MP**, Full Stack Developer, DevOps Engineer and Tech Consultant
(Malappuram, Keralam, India).

## Command line (for shortcuts / hotkeys)

```
LOQKeyboardRGB.exe --toggle            lights on/off
LOQKeyboardRGB.exe --off | --on
LOQKeyboardRGB.exe --profile "Ocean"
LOQKeyboardRGB.exe --color FF3300      all zones, static
```

These commands run without opening a window. Exit code 0 means OK, 1 means the keyboard
wasn't found, and 2 means a bad argument.

## How it works

The keyboard controller is an ITE USB HID device (`048D:C993`). Its HID collection on usage
page `0xFF89` takes a 33-byte feature report:

```
CC 16 <effect> <speed 1-4> <brightness 0-2> <R G B zone1..zone4> 00 <wave LTR> <wave RTL> 00…
effect: 01 static, 03 breath, 04 wave, 06 smooth   (brightness 0 = off)
```

Settings and profiles are stored in `%APPDATA%\LOQ Keyboard RGB\`.

## Build

Run `build.cmd`. It uses the C# compiler that comes with Windows (.NET Framework 4), so you don't need an SDK.

## Notes

- Fn+Space still changes brightness in hardware. The app keeps working after you use it.
- Lenovo Vantage can sometimes reset the lighting when it starts or when you change power modes.
  If that happens, turn off Vantage's keyboard lighting control, or click any setting in the app to apply it again.
