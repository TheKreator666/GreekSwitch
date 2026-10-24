# Manual test checklist

The hooks only react to real keyboard input (injected input is ignored by design), so these
checks need a person at a keyboard. Run them on a clean Windows 10/11 x64 machine with the
Greek and English (US) layouts installed, using `publish/GreekSwitch.exe`.

Tip: make the active layout visible in the taskbar, so you can see GreekSwitch switch it.

Before a manual pass, `tools/EndToEndDriver` runs a quick automated smoke test in Notepad
(wrong-layout sentences, both convert hotkeys, undo, clipboard restore) against a Debug build in
test mode; see the comment at the top of its `Program.cs`. It doesn't replace the checks below.

## 0. Start-up

- [ ] Double-click `GreekSwitch.exe`. The "αb" tray icon appears (grey, then blue within about 2 s).
- [ ] Hovering over it shows "GreekSwitch: on".
- [ ] Starting the exe a second time shows "already running" and exits.
- [ ] `%LOCALAPPDATA%\GreekSwitch\greekswitch.log` contains `GR layout verified … 0 mismatch(es)`,
      the same line for EN, and `GreekSwitch started`.
- [ ] `%APPDATA%\GreekSwitch\settings.json` exists with the defaults.

## 1. Notepad (the reference sentence)

- [ ] Switch to Greek and type `Θέλω να φτιάξω ένα `. Nothing changes.
- [ ] Still in Greek, type the keys for "application" followed by Space (you see `αππλιψατιον`).
      It becomes `application ` and the layout switches to EN.
- [ ] Without switching, type the keys P O Y and Space. `poy` becomes `που ` and the layout
      switches to GR.
- [ ] Type `θα κάνει πράξεις ` normally.
- [ ] Final text: `Θέλω να φτιάξω ένα application που θα κάνει πράξεις`.
- [ ] In EN, type `kalhm;era ` (the `;` key is the tonos key). It becomes `καλημέρα `.
- [ ] In GR, type the keys for `hello world`. You get `hello world`.
- [ ] `hello`, `the`, `ok`, `C#`, `user@mail.com`, `v2` and `myVariable` typed in EN stay unchanged.
- [ ] `I love ελλάδα` (switching layouts yourself) stays unchanged.
- [ ] End a wrong-layout word with `.` or `,`. The punctuation is kept after the corrected word.

### Earlier words (milestone 11)

- [ ] With the **English** layout active and nothing corrected yet, type `den u;elv na ;eruv `
      (the keys of "δεν θέλω να έρθω"). It becomes `δεν θέλω να έρθω `, and the first word is
      fixed at the same moment as the second.
- [ ] Repeat, then press Backspace right after `θέλω` is fixed. **Both** words go back
      (`den u;elv`), and the layout returns to English.
- [ ] Type `den, u;elv `. Only `u;elv` is fixed: punctuation ends the run.
- [ ] Type `den `, click elsewhere in the same line, type `u;elv `. Only `u;elv` is fixed.

## 2. Microsoft Word

- [ ] Repeat the reference sentence. Corrections land correctly, and Word's own autocorrect
      doesn't mangle the result.
- [ ] Undo (section 7) works in Word.

## 3. Chrome (or Edge)

- [ ] In a normal text field (e.g. a search box or a textarea), the reference sentence is corrected.
- [ ] In the address bar, `google.com` typed with the wrong layout behaves sensibly. The part
      after the `.` is never touched.

## 4. Teams / Slack (chat apps)

- [ ] Type a wrong-layout word followed by Space. It's corrected.
- [ ] Type a wrong-layout word followed by **Enter**. The message is sent as typed and nothing
      is backspaced afterwards (default `correctOnEnterAndTab: false`).

## 5. Password field

- [ ] Open a login page (or the Windows credential prompt, or a Chrome password field) and type
      a password containing wrong-layout "words" plus Space.
- [ ] Nothing is corrected, and no layout switch happens.
- [ ] The log shows `Key capture off (passive)` when the password field gets focus, and no
      `Replacement made`.
- [ ] Back in a normal field, corrections work again.

## 6. Excluded app (terminal)

- [ ] In Windows Terminal, cmd or PowerShell, type wrong-layout words. Nothing changes.
- [ ] In VS Code, the same.
- [ ] Remove `cmd.exe` from the exclusion list in Settings, and corrections now happen in cmd.
      Restore it ("Restore default exclusions").

## 7. Undo

- [ ] In Notepad (GR active), type `αππλιψατιον ` → `application `.
- [ ] Press **Backspace** immediately. The text is back to `αππλιψατιον`, and the layout is GR again.
- [ ] Type Space, then `αππλιψατιον ` once more. It's left alone this time.
- [ ] Settings → "Never convert" lists `αππλιψατιον`. Remove it, press OK, and it's corrected again.
- [ ] After a correction, type another letter and then Backspace. That's an ordinary Backspace
      (no undo).

## 8. Tray menu

- [ ] Untick **Enabled**. The icon turns grey and nothing is corrected. Tick it again, and
      corrections are back.
- [ ] **Pause for 5 min**. The icon turns grey and the menu shows a countdown. "Resume" ends
      the pause early, or it ends by itself after 5 minutes.
- [ ] **Settings → Start with Windows**: on writes `HKCU\…\Run\GreekSwitch` and off removes
      it. Off is the default.
- [ ] **Exit**. The tray icon disappears, typing anywhere is unaffected, and the process has ended.

## 10. Robustness (milestone 12)

- [ ] **Remote Desktop / Citrix:** connect with `mstsc` (or Citrix Workspace) and type
      wrong-layout words in a remote Notepad. Nothing is corrected, and the log shows
      `Key capture off (passive): excluded app`.
- [ ] **App run as administrator:** start Notepad with *Run as administrator* and type a
      wrong-layout word. It is not corrected, and the log shows `SendInput delivered 0 of …`.
      Nothing is half-typed.
- [ ] **UWP / Store app** (e.g. Sticky Notes or the Mail app): corrections work, or are skipped
      cleanly if the layout switch is ignored. The log then shows `trying the layout hotkey`
      and, if that fails too, `switch … not visible in time`.
- [ ] **Google Docs** in Chrome: corrections work. Docs draws its own text, so UI Automation
      may not expose it; corrections then rely on the other guards.
- [ ] **Word with AutoCorrect on:** type `teh ` in the English layout. Word fixes it to `the`
      itself, and GreekSwitch does nothing to it. Then type a wrong-layout word that Word also
      autocorrects. The correction is skipped with `the text before the caret is not what was
      typed`, and no wrong characters are deleted.
- [ ] **Grammarly** (if installed): accept a Grammarly suggestion right after a word.
      GreekSwitch never deletes Grammarly's text.
- [ ] **Windows text suggestions** (Settings → Time & language → Typing → Show text
      suggestions when typing on the physical keyboard): accepting a suggestion is never
      undone or mangled.
- [ ] **NVDA or Narrator running:** corrections still work, and the screen reader isn't flooded
      (it may read the corrected word).
- [ ] **High-DPI, two monitors with different scaling:** the tray icon is sharp on both, and the
      Settings dialog is readable on both.
- [ ] **Password field, fail closed:** click into a password field and type immediately.
      Nothing is corrected, even in the first moment before UI Automation answers.
- [ ] **Lock and unlock** (Win+L): no word typed before locking is corrected after unlocking.
- [ ] **Sleep and resume:** after resuming, corrections still work (the log shows
      `Resumed from sleep: reinstalling hooks`).
- [ ] **Health:** the tray tooltip normally says `GreekSwitch: on`. Only if the keyboard hook
      stops receiving input after three reinstalls does it turn red with
      `GreekSwitch: error, see the log`.

## 11. Hotkeys and first run (milestone 13)

- [ ] **First run:** close GreekSwitch, rename `%APPDATA%\GreekSwitch\settings.json`, start it.
      The welcome dialog shows ✓ for both layouts, the capture summary and the three hotkeys.
      *Start with Windows* is unticked. OK closes it, and it doesn't come back on the next start.
      Put the old settings.json back afterwards.
- [ ] **First run, missing layout** (a test account with only English): the dialog shows
      ✗ for Greek and how to add it.
- [ ] **Convert last word:** in a new Notepad tab (EN active), type `to ` (kept: it's an English
      word) and press **Ctrl+Alt+Space**. It becomes `το ` and the layout is GR. Backspace right
      after puts `to` back.
- [ ] Convert last word **mid-word**: type `kalhm` and press Ctrl+Alt+Space. It becomes `καλημ`;
      keep typing in Greek and nothing else changes.
- [ ] **Learning:** type `to ` in EN again and convert it with the hotkey, this time without
      undoing (undo also forgets what the hotkey taught). Switch to EN and type `to ` once more:
      it is converted automatically. Settings shows the learned count; *Forget learned words*
      clears it.
- [ ] **Convert selection:** copy some text of your own (e.g. `keep me`), then in Notepad
      select `Kalhm;era sas, ti k;aneis?` and press **Ctrl+Alt+Shift+Space**. It becomes
      `Καλημέρα σας, τι κάνεις;`, the layout is GR, and Ctrl+V pastes `keep me` again.
- [ ] Convert selection in **Word** (formatting of the paste is plain text) and **Chrome**
      (a text area). Ctrl+Shift+C must not open the developer tools.
- [ ] Convert selection with **nothing selected**: nothing is pasted and the clipboard is
      unchanged (log: `nothing was copied`).
- [ ] With **clipboard history** on (Win+V): after Convert selection, the converted text is not
      in the history; the original selection is (the app copied it). Your earlier entry isn't
      duplicated.
- [ ] Convert selection in a **password field** or in **Windows Terminal**: nothing happens.
- [ ] **Toggle:** Ctrl+Alt+Shift+G turns GreekSwitch off (grey icon, *Enabled* unticked) and
      on again.
- [ ] **Hotkey already taken:** register Ctrl+Alt+Space in another program first (e.g. an
      AutoHotkey script), then start GreekSwitch. Settings lists it as already used; the log says
      `Hotkey for 'Convert last word' is already used by another program`.
- [ ] **Changing hotkeys:** set Convert last word to `F9` in Settings; F9 converts, Ctrl+Alt+Space
      no longer does. An invalid entry (`Ctrl+Alt`) or `Q` alone is refused with a message.
- [ ] **Alt menu:** after Ctrl+Alt+Space in Notepad, the menu bar is not activated.
- [ ] **Flash:** a correction flashes the tray icon green; with *Flash* unticked in Settings, it doesn't.
- [ ] **Accidental Caps Lock** (tick it in Settings): with Caps Lock on, type `hELLO ` (Shift on
      the first letter). It becomes `Hello ` and Caps Lock is off. Unticked (the default), it stays.

## 9. Privacy spot checks

- [ ] After a session of typing, search the log for words you typed. None appear.
- [ ] With a firewall/network monitor (e.g. Resource Monitor → Network), GreekSwitch makes no
      connections.
