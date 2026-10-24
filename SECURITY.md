# Security and privacy

GreekSwitch installs a **global keyboard hook**, which is the same mechanism keyloggers use. This
page explains what it captures, what it doesn't, and why.

## What it captures, and where that goes

| Captured | Why | Where it goes |
|---|---|---|
| Virtual-key code and Shift / Caps Lock / Ctrl / Alt / Win state of each **physical** key press | To re-read the current word in both layouts | An in-memory queue, then an in-memory word buffer. The buffer is **wiped** (overwritten, not just emptied) at every word boundary, click, focus change, caret key or shortcut. |
| The keys of the last 1–3 completed words of the current run (same field, separated only by spaces) | So that after a confident correction the words just before it can be fixed too ("den θέλω" → "δεν θέλω") | Memory only. Wiped on any reset, punctuation, Enter/Tab, a Backspace into earlier text, or when a word leaves the 3-word window. |
| The keys of the last completed word and its delimiter | So the *Convert last word* hotkey can convert it after you've pressed Space | Memory only. Wiped on any reset (click, focus change, caret key, shortcut) and replaced by the next word. |
| The selected text, only when you press the *Convert selection* hotkey | To convert it to the other layout | Copied by the app itself (GreekSwitch sends Ctrl+C), converted in memory, pasted back (Ctrl+V), and then your previous clipboard content is put back. Never stored or logged. See *The clipboard* below. |
| Which control had keyboard focus for each key (a window handle) | So a correction is only typed into the control the word was typed into | Memory only |
| That a mouse button was pressed (not where) | A click may move the caret, so the word is forgotten | Nowhere; it only clears the buffer |
| The foreground app's executable name (e.g. `notepad.exe`) | To honour the exclusion list | Memory only |
| Whether the focused control is a password field (UI Automation, on a separate thread; capture stays off until it answers) | To go passive in password fields | Memory only |
| Right before a correction: the characters just before the caret in the focused control, as many as the correction will delete (UI Automation TextPattern, where the control supports it) | To check that no other tool (AutoCorrect, text suggestions, Grammarly) changed the word since it was typed; if it did, the correction is skipped | Compared with the expected text, then dropped. Never stored or logged. |

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
  machine, under another account, or from a backup of the file. The *Convert last word* hotkey
  learns the same way: a word you convert by hotkey is remembered as a hash on an
  "always convert" list (or, if GreekSwitch had converted it, on the never-convert list).
  Settings → *Forget learned words* deletes both lists.
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
| `%APPDATA%\GreekSwitch\settings.json` | Settings, the exclusion list, the hotkeys, the never-convert words **you typed into Settings** (readable, so you can edit them), the **hashes** of words learned from undo and from the *Convert last word* hotkey, and the DPAPI-encrypted key for those hashes |
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

- **Only over the text that was typed.** Where the control supports UI Automation's TextPattern,
  the text before the caret is read and compared first; if another tool rewrote the word, nothing
  is sent.
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

It also registers its three hotkeys with `RegisterHotKey` (on the UI thread, not in the hook).
Windows then delivers those key combinations to GreekSwitch instead of the focused app.

### The clipboard

Only the *Convert selection* hotkey touches the clipboard, and only when GreekSwitch is
capturing in that window (not in password fields, password managers or excluded apps). It
waits until you release the hotkey, never sends Ctrl+C to a terminal (where it would interrupt
the running command), and:

1. keeps a copy of what's on the clipboard (every format it can read back);
2. sends Ctrl+C; the app copies the selection;
3. converts the text in memory and puts it on the clipboard **marked to stay out of clipboard
   history and cloud clipboard** (`CanIncludeInClipboardHistory` = 0,
   `CanUploadToCloudClipboard` = 0, `ExcludeClipboardContentFromMonitorProcessing`);
4. sends Ctrl+V (trailing spaces are typed as Space presses, because some apps drop them from
   a paste), waits half a second for the app to paste, and switches the layout;
5. puts your previous clipboard content back (with the same markers, so it isn't added to the
   history a second time).

One copy is outside GreekSwitch's control: in step 2 *the app* writes the selected text to the
clipboard, without those markers. With clipboard history (Win+V) or a clipboard manager turned
on, the original selection therefore appears there, just as if you had pressed Ctrl+C yourself.
Content that can't be read back (some delay-rendered or private formats) can't be restored.

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
