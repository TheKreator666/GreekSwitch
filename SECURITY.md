# Security and privacy

GreekSwitch installs a **global keyboard hook**, which is the same mechanism keyloggers use. This
page explains what it captures, what it doesn't, and why.

## What it captures, and where that goes

| Captured | Why | Where it goes |
|---|---|---|
| Virtual-key code and Shift / Caps Lock / Ctrl / Alt / Win state of each **physical** key press | To re-read the current word in both layouts | An in-memory queue, then an in-memory word buffer. The buffer is cleared at every word boundary, click, focus change, caret key or shortcut. |
| That a mouse button was pressed (not where) | A click may move the caret, so the word is forgotten | Nowhere; it only clears the buffer |
| The foreground app's executable name (e.g. `notepad.exe`) | To honour the exclusion list | Memory only |
| Whether the focused control is a password field (UI Automation) | To go passive in password fields | Memory only |

While GreekSwitch is disabled, paused, in an excluded app, or in a password field, the hook
**does not enqueue key presses at all**.

## What it doesn't do

- **No network access of any kind.** No telemetry, no update checks, no crash reporting. The
  code makes no network calls.
- **Keystrokes and typed words are never written to disk.** That includes the log. Log events
  carry languages, lengths, reasons and exception types (e.g. `Replacement made, lang GR→EN,
  length 11`), and never text. A unit test (`NoTextInLogsTests`) runs sentences, corrections
  and an undo through the pipeline with a capturing logger, and fails if any typed or produced
  word appears. Exception messages are left out of the log as well.
- **Injected input is ignored.** Events flagged `LLKHF_INJECTED`, including GreekSwitch's own
  `SendInput` output, are never processed.
- The only debug path that can log text is compiled in **Debug builds only** (`#if DEBUG`), and
  even then it needs the explicit `debugLogText` setting.

## What it writes to disk

| File | Contents |
|---|---|
| `%APPDATA%\GreekSwitch\settings.json` | Settings, the exclusion list and the **never-convert list**. These are words you chose to keep (via undo or Settings), and the only words ever stored. |
| `%LOCALAPPDATA%\GreekSwitch\greekswitch.log` (+ `.1`) | Event log without text, rolled at 1 MB |
| `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\GreekSwitch` | Only if you turn on "Start with Windows" |

## What it sends to other apps

When it corrects a word, GreekSwitch uses `SendInput` to send the Backspaces and the corrected
word to the focused window, and posts `WM_INPUTLANGCHANGEREQUEST` to switch that window's
layout. It does nothing else to other processes.

## Why antivirus may flag it

A global keyboard hook combined with `SendInput` looks like a keylogger or macro tool to
heuristic scanners. The release exe is unsigned. Build it yourself from source (`build.ps1`) if
you'd rather not trust a binary.

## Reporting a vulnerability

Please report security issues privately through the repository's *Security → Report a
vulnerability* (GitHub private advisory) rather than in a public issue.
