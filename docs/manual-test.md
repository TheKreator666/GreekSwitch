# Manual test checklist

The hooks only react to real keyboard input (injected input is ignored by design), so these
checks need a person at a keyboard. Run them on a clean Windows 10/11 x64 machine with the
Greek and English (US) layouts installed, using `publish/GreekSwitch.exe`.

Tip: make the active layout visible in the taskbar, so you can see GreekSwitch switch it.

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

## 9. Privacy spot checks

- [ ] After a session of typing, search the log for words you typed. None appear.
- [ ] With a firewall/network monitor (e.g. Resource Monitor → Network), GreekSwitch makes no
      connections.
