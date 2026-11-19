# RPN Numpad

Turn your keyboard's numeric keypad into an HP-style **RPN (Reverse Polish Notation) calculator** on Windows.
Press a key combination and the numpad becomes a calculator. Press it again and the numpad goes back to normal.

![keys](https://img.shields.io/badge/toggle-Ctrl%2BAlt%2BNumpad%20Enter-f2b33d) ![platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-blue) ![license](https://img.shields.io/badge/license-MIT-green)

<img src="screenshot.png" alt="RPN Numpad calculator window" width="300" align="right">

While RPN mode is on, a small calculator window stays on top of other windows. It shows:

* a **tape** of the keys you've pressed (e.g. `5 ENTER 3 + 10 ENTER 4.2`)
* the **stack** (T, Z, Y and X, with the number you're keying in shown in large type)
* an **on-screen keypad** laid out like the numpad. Each key you press lights up. Gold *f* functions are printed above each key and blue *g* functions below it, like on an HP-15C.

The window never takes focus, so you can keep working in the app underneath. Press f then Enter to type the result into that app.

## Install and run

You need Windows 10 or 11. Nothing needs to be installed, because the app is built with the C# compiler that ships with Windows.

1. Download this repository (green **Code** button → **Download ZIP**), and unzip it.
2. Double-click **`build.cmd`**. It creates `RpnNumpad.exe` next to it.
3. Double-click **`RpnNumpad.exe`**. A tray icon appears.
4. Press **Ctrl + Alt + Numpad Enter** to turn RPN mode on or off.

To have it start automatically, right-click the tray icon and choose **Start with Windows**.

### If Windows blocks the exe

If Windows reports *"An Application Control policy has blocked this file"*, **Smart App Control** is blocking
unsigned programs that you build yourself. Your options:

1. Sign `RpnNumpad.exe` with a code-signing certificate.
2. Turn off Smart App Control (Windows Security → App & browser control → Smart App Control).
   Windows won't let you turn it back on afterwards without resetting or reinstalling Windows.
3. Load the app from Windows PowerShell, which runs it inside the signed `powershell.exe`:
   ```powershell
   powershell.exe -NoProfile -STA -Command "[Reflection.Assembly]::Load([IO.File]::ReadAllBytes('C:\path\to\RpnNumpad.exe')).EntryPoint.Invoke(`$null, @())"
   ```
   Use `powershell.exe` (Windows PowerShell 5.1), not `pwsh`. Option 3 works around a security setting, so whether to use it is up to you.
   With this option, the tray's *Start with Windows* setting won't work.

## Enable / disable

**Ctrl + Alt + Numpad Enter** switches RPN mode on and off. You can also double-click the tray icon.
When RPN mode is off, the numpad works normally.

## How RPN works

Instead of `5 + 3 =`, you enter the numbers first and then the operation: `5 Enter 3 +`.
Numbers wait on a four-level **stack**:

```
T   ← top (oldest)
Z
Y
X   ← the number you're typing, or the latest result
```

* **Enter** copies X up into Y so you can type the next number.
* **An operation** (`+ − × ÷`) works on Y and X, puts the answer in X, and moves Z and T down one level.

Example: `(5 + 3) × (10 − 4)` → `5 Enter 3 + 10 Enter 4 − ×` → **48**. You never need parentheses.

## Keys (RPN mode on)

| Key | Function |
|---|---|
| `0`–`9` `.` | key in a number |
| `Enter` | ENTER (copies X into Y and pushes the stack up) |
| `+ - * /` | Y op X |
| `NumLock` | **f** (gold) shift for the next key |
| `NumLock` `NumLock` | **g** (blue) shift for the next key |

Pressing NumLock repeatedly cycles: normal → **f** → **g** → normal. A shift applies to one key only.
While a shift is active, the on-screen keys show only that shift's functions.

Gold **f** functions (press NumLock once, then the key):

```
 NumLock     /          *          -
 → g         CLEAR*     COPY X     ← backspace

 7           8          9          +
 √x          x²         yˣ         CLx

 4           5          6
 1/x         %          LAST X

 1           2          3          Enter
 R↓          x⇄y        UNDO       TYPE X (types X into the active app)

 0                      .
 CHS                    EEX
```

\* CLEAR empties the stack and wipes the tape, so the next calculation starts fresh.

Blue **g** functions (press NumLock twice, then the key). The header shows whether angles are in DEG or RAD.

```
 NumLock     /          *          -
 cancel      DEG/RAD    π          10ˣ

 7           8          9          +
 SIN         COS        TAN        ʸ√x  (x-th root of y)

 4           5          6
 ASIN        ACOS       ATAN

 1           2          3          Enter
 LN          LOG        eˣ         INT  (integer part)

 0                      .
 n!                     |x|
```

## Good to know

* Ctrl/Alt/Win + numpad keys pass through, so Alt codes and app shortcuts still work in RPN mode.
* Works with NumLock on or off. In RPN mode NumLock is captured, so its light doesn't change.
* Invalid operations (÷0, √ of a negative, tan 90°, …) show a red message and leave the stack unchanged.
* The stack, the angle mode and the window position are saved between sessions (in `HKCU\Software\RpnNumpad`).
* Drag the window to move it. Right-click it or the tray icon for *Key map*, *Start with Windows* and *Exit*.

## How it works

`RpnNumpad.cs` is a single-file WinForms app. A low-level keyboard hook (`WH_KEYBOARD_LL`) intercepts numpad keys,
which are identified by scan code so they work regardless of NumLock state. The hook swallows those keys only while RPN mode is on.
The window uses `WS_EX_NOACTIVATE` so it never steals focus. The app has no network access and collects no data.

## License

[MIT](LICENSE). Free to use, change and share.
