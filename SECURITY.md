# Security and privacy

GreekSwitch installs a **global keyboard hook**, which is the same mechanism keyloggers use. This
page explains what it captures, what it doesn't, and why.

## What it captures, and where that goes

| Captured | Why | Where it goes |
|---|---|---|
| Virtual-key code and Shift / Caps Lock / Ctrl / Alt / Win state of each **physical** key press | To re-read the current word in both layouts | An in-memory queue, then an in-memory word buffer. The buffer is **wiped** (overwritten, not just emptied) at every word boundary, click, focus change, caret key or shortcut. |
| Which control had keyboard focus for each key (a window handle) | So a correction is only typed into the control the word was typed into | Memory only |
| That a mouse button was pressed (not where) | A click may move the caret, so the word is forgotten | Nowhere; it only clears the buffer |
| The foreground app's executable name (e.g. `notepad.exe`) | To honour the exclusion list | Memory only |
| Whether the focused control is a password field (UI Automation) | To go passive in password fields | Memory only |

While GreekSwitch is disabled, paused, in an excluded app, or in a password field, the hook
**does not enqueue key presses at all**. Password managers (KeePass, KeePassXC, 1Password,
Bitwarden, Dashlane, NordPass, Enpass, RoboForm, LastPass, Proton Pass) are **always**
excluded. That list is built in, so editing `settings.json` can't remove them.

## What it doesn't do

- **No network access of any kind.** No telemetry, no update checks, no crash reporting. The
  code makes no network calls.
- **Keystrokes and typed words are never written to disk.** That includes the log. Log events
  carry languages, lengths, reasons and exception types (e.g. `Replacement made, lang GR→EN,
  length 11`), and never text. A unit test (`NoTextInLogsTests`) runs sentences, corrections
  and an undo through the pipeline with a capturing logger, and fails if any typed or produced
  word appears. Exception messages are left out of the log as well.
- **Undo never stores the undone word as text.** An undone "word" could be a password typed into
  a box that UI Automation can't identify as a password box (some web pages, games, login
  forms that show the password). Undo therefore remembers only an HMAC-SHA256 hash of it. The
  HMAC key is random per installation and stored in `settings.json` **encrypted with Windows
  DPAPI** for your user account. So the hashes can't be tested against guesses on another
  machine, under another account, or from a backup of the file. Settings → *Forget learned
  words* deletes them.
- **No crash dumps.** GreekSwitch excludes itself from Windows Error Reporting at start-up, so a
  crash doesn't produce a report or dump of its memory. This doesn't stop an administrator who
  has configured system-wide dump collection, and memory isn't encrypted while running.
- **Injected input is ignored.** Events flagged `LLKHF_INJECTED`, including GreekSwitch's own
  `SendInput` output, are never processed. Debug builds have an end-to-end test mode, enabled
  only by the environment variable `GREEKSWITCH_TEST_ACCEPT_INJECTED=1`, that also processes
  keys injected by *other* programs. Release builds don't contain the switch.
- The only debug path that can log text is compiled in **Debug builds only** (`#if DEBUG`), and
  even then it needs the explicit `debugLogText` setting.
- **Nothing is unpacked to disk at run time.** The exe contains only managed code and data. No
  native libraries are extracted to a temp folder where another program could tamper with them
  between runs.

## What it writes to disk

| Location | Contents |
|---|---|
| `%APPDATA%\GreekSwitch\settings.json` | Settings, the exclusion list, the never-convert words **you typed into Settings** (readable, so you can edit them), the **hashes** of words learned from undo, and the DPAPI-encrypted key for those hashes |
| `%LOCALAPPDATA%\GreekSwitch\greekswitch.log` (+ `.1`) | Event log without text, rolled at 1 MB |
| `HKCU\Software\Microsoft\Windows\Windows Error Reporting\ExcludedApplications` | The exe name, so crashes aren't reported or dumped |
| `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\GreekSwitch` | Only if you turn on "Start with Windows" |

Any program running under your account can edit `settings.json`, for example to remove
exclusions. Such a program could equally run its own keylogger, so this adds no new access;
but it's why the password-manager exclusions are built in.

## What it sends to other apps

When it corrects a word, GreekSwitch posts `WM_INPUTLANGCHANGEREQUEST` to switch the focused
window's layout. It then uses `SendInput` to send Backspaces and the key presses that retype
the word. It does nothing else to other processes. Safeguards:

- **Only into the same control.** If keyboard focus has moved since the word was typed (a popup,
  another window), the correction is cancelled. This is checked before the layout switch and
  again just before typing.
- **Never Enter or Tab into a terminal or IDE.** A correction ended by Enter retypes Enter,
  which in a terminal would run the corrected text as a new command. Such corrections are never
  made in terminals (Windows Terminal, console windows, cmd, PowerShell, WSL, PuTTY, mintty and
  others) or IDEs with integrated terminals (VS Code, Visual Studio, JetBrains IDEs), whatever
  the exclusion list and `correctOnEnterAndTab` say.
- **Never while you type ahead or hold Ctrl/Alt/Win.** Otherwise its Backspaces could delete
  your new letters, or trigger shortcuts.

## Verifying the exe

The release exe is **unsigned** (signing needs a code-signing certificate; the build has a
placeholder step for it). Instead, every build writes `GreekSwitch.exe.sha256` next to the exe.
To check a copy on another machine:

```powershell
Get-FileHash .\GreekSwitch.exe -Algorithm SHA256   # must match GreekSwitch.exe.sha256
```

Or build it yourself from source with `build.ps1`.

## Why antivirus may flag it

A global keyboard hook combined with `SendInput` looks like a keylogger or macro tool to
heuristic scanners, and unsigned binaries get less benefit of the doubt.

## Reporting a vulnerability

Please report security issues privately through the repository's *Security → Report a
vulnerability* (GitHub private advisory) rather than in a public issue.
