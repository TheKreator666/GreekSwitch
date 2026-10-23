# GreekSwitch

A Windows tray app that fixes words typed in the wrong keyboard layout, Greek vs English, as
you type. At each word boundary it re-reads the keys you pressed in both layouts. If the other
reading is clearly what you meant, it retypes the word and switches the layout for you.

```
You type:  Θέλω να φτιάξω ένα αππλιψατιον poy ua κάνει πράξεις
You get:   Θέλω να φτιάξω ένα application που θα κάνει πράξεις
```

Nothing is translated. GreekSwitch buffers *physical keys* (virtual-key codes), not characters,
so every word has a Greek reading and an English reading of the same keystrokes:

| Keys | Greek reading | English reading | Winner |
|---|---|---|---|
| A P P L I C A T I O N | αππλιψατιον | application | EN |
| P O Y | που | poy | GR |
| U A | θα | ua | GR |

## Running it

1. Install both a **Greek** and an **English** keyboard layout in Windows
   (Settings → Time & language → Language & region). GreekSwitch only switches between layouts
   you already have.
2. Run `GreekSwitch.exe`. It's a single self-contained file, so the target machine needs no
   .NET install (Windows 10/11 x64). An "αb" icon appears in the tray. It turns blue after
   about a second, once the dictionaries have loaded.
3. Type. Right-click the tray icon for the menu:
   - **Enabled ✓**: master on/off switch (saved).
   - **Pause for 5 min**: click again to resume early.
   - **Settings…**: also opens on a double-click of the icon.
   - **Exit**

> **The exe is unsigned.** Windows SmartScreen may warn on first run (*More info → Run anyway*).
> Some antivirus products flag *any* program that combines a global keyboard hook with
> `SendInput`, because keyloggers and macro tools use the same APIs. See [SECURITY.md](SECURITY.md)
> for exactly what GreekSwitch captures. The build has a placeholder step for code signing.

Only one copy runs at a time (named mutex). "Start with Windows" is **off by default**; when you
turn it on in Settings, GreekSwitch adds a `GreekSwitch` value under
`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.

## Undo

If GreekSwitch changes a word you meant as typed, press **Backspace immediately** after the
correction (before any other key). That:

1. puts back exactly what you typed (without the space or punctuation, which your Backspace
   just deleted),
2. switches the layout back, and
3. adds the word to the **never-convert list**, so it's left alone from now on.

Type Space and carry on. To forget a never-convert word, remove it in Settings. Any other key
after a correction (or a click, arrow key, focus change and so on) disarms undo, and Backspace
becomes an ordinary Backspace again.

## Settings

The Settings dialog edits `%APPDATA%\GreekSwitch\settings.json`. You can also edit the file by
hand while GreekSwitch is closed:

| Key | Default | Meaning |
|---|---|---|
| `enabled` | `true` | Master switch (same as the tray checkbox). |
| `minLength` | `3` | Words with fewer keys are only converted when the other reading is a dictionary word, the current one isn't, **and** the last few words were in the other language. This is what lets `ua` → `θα` happen mid-sentence. |
| `replaceThreshold` | `2.0` | Score margin needed to replace a word when no dictionary settles it. Higher means more cautious. |
| `dictionaryWeight` / `ngramWeight` / `contextWeight` | `2.0` / `1.0` / `0.3` | Scoring weights (see *How it decides*). File only; not in the dialog. |
| `correctOnEnterAndTab` | `false` | Also correct words ended by Enter/Tab. Off because by the time GreekSwitch sees the key, Enter may already have sent a chat message and Tab may have moved focus. |
| `excludedProcesses` | terminals, IDEs, password managers, game launchers and common games | Executable names where GreekSwitch is completely passive. The defaults include `WindowsTerminal.exe`, `cmd.exe`, `powershell.exe`, `pwsh.exe`, `Code.exe`, `devenv.exe` and `rider64.exe`. "Restore default exclusions" in the dialog brings the list back. |
| `neverConvert` | `[]` | Words never to convert (accent- and case-insensitive). Undo adds to it. |
| `debugLogText` | `false` | **Debug builds only**: log the words themselves. Compiled out of Release builds. |

Start with Windows lives in the registry, not in this file.

## How it decides

At each boundary (Space or punctuation that's the same in both layouts: `. , ! ? ( ) [ ] { } " -`)
the word's keys are decoded in both layouts. The Greek reading follows the real dead-key rules:
`;` + vowel → ά έ ή ί ό ύ ώ, Shift+`;` + ι/υ → ϊ ϋ, Shift+W + ι/υ → ΐ ΰ. Each reading is scored as

```
score = (isDictionaryWord ? dictionaryWeight : 0) + ngramWeight × trigramLogProb + contextPrior
```

- **Dictionary:** Hunspell (LibreOffice `el_GR` and `en_US`) via WeCantSpell. Greek matching
  ignores accents, case and σ/ς, so `καλημερα`, `ΚΑΛΗΜΕΡΑ` and `πράξεισ` all count.
- **Trigrams:** character trigram models for each language, averaged per character.
- **Context prior:** a small bonus toward the language of the last 3 accepted words.

The rules, in order:

1. **Skip code-like words**: digits or symbols such as `@ / _ #`, no letters at all, camelCase
   in both readings, or a word glued to a preceding `x.` (domains such as `mail.com`).
2. **Skip never-convert words.**
3. **Skip short words** (< `minLength` keys) unless dictionary and context both agree.
4. **Replace** if the current reading isn't a word but the other one is, **or** the other
   reading's combined score beats the current one by more than `replaceThreshold`. Because the
   margin uses the *combined* score, a valid current word effectively never loses on n-gram
   evidence alone.
5. Otherwise **keep**.

A Greek replacement gets a final ς at the end of the word. The corrector sends one `SendInput`
batch: Backspaces for the word and the delimiter, then the new word and delimiter as Unicode
key events. It then posts `WM_INPUTLANGCHANGEREQUEST` to switch the foreground window's layout.

The test suite runs samples of both dictionaries through the engine. It finds **0 false
positives** in about 4,500 correctly typed words. It corrects **100%** of sampled Greek words
typed with the English layout and **97.8%** of English words typed with the Greek layout. The
English misses are words whose Greek reading is also a Greek word, so leaving them alone is
correct (e.g. *India* → *Ινδια*).

## Privacy

There is no network access of any kind, no telemetry and no update checks. Keystrokes and words
are never written to disk. The only words ever stored are the ones on your never-convert list,
in `settings.json`. GreekSwitch is fully passive in password fields (detected with UI
Automation) and in excluded apps. Details: [SECURITY.md](SECURITY.md).

The event log is `%LOCALAPPDATA%\GreekSwitch\greekswitch.log` (rolled at 1 MB). It records
events such as `Replacement made, lang GR→EN, length 11, reason Dictionary`, but never the
text. A unit test enforces this.

## Known limitations

- **Typing very fast right after a correction.** Keys that land while the correction is being
  sent can interleave with it. The layout switch is asynchronous, so a key or two may still come
  out in the old layout.
- **Elevated windows** (run as administrator) can't receive input from a non-elevated
  GreekSwitch (Windows UIPI). The correction is skipped and logged. Run GreekSwitch elevated if
  you need it there.
- **Injected input is ignored** by design (that's how GreekSwitch ignores its own output). Text
  from on-screen keyboards, remote-desktop tools, AutoHotkey and similar won't be corrected.
- **Only Greek and English.** When any other layout is active, GreekSwitch stays out of the way.
- **Words valid in both languages are never changed**, and short words need context.
  `ok`/`οκ` and `India`/`Ινδια` stay as typed.
- **Password detection depends on UI Automation.** Apps that don't expose it (some games, some
  custom-drawn UIs) can't be detected; add them to the exclusion list.
- **Apps with their own autocorrect or autocomplete** (Word, some chat apps) may also react to
  the retyped text.
- **Enter/Tab-ended words aren't corrected** unless you turn on `correctOnEnterAndTab`.
- **Memory and size.** The full Greek Hunspell dictionary (about 830k forms) uses roughly
  180 MB of RAM and takes about 1 s to load at startup. The self-contained exe is about 75 MB.
- **Dead-key edge cases.** A dead key followed by Space types a lone accent in Greek; such words
  are left as they are.
- **Caps Lock** is tracked from key events after start-up. If it gets out of sync (rare, e.g.
  toggled from an on-screen keyboard), toggle it once.

## Building from source

Requires the .NET 8 SDK (or newer). Tests and the core library build on any OS, and the Windows
projects build anywhere thanks to `EnableWindowsTargeting`.

```bash
dotnet restore
dotnet build -c Release
dotnet test -c Release
dotnet publish src/GreekSwitch.App -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -o publish
```

Or run `./build.ps1` (Windows) or `./build.sh` (any OS). Both run all of the above and stop on
the first failing test. `-SkipPublish` / `--skip-publish` stop after the tests. GitHub Actions
(`.github/workflows/build.yml`, `windows-latest`) does the same and uploads `GreekSwitch.exe`
as an artifact. It also has a placeholder step for code signing.

**Data files are embedded.** The Hunspell dictionaries (`data/dictionaries`) and the trigram
models (`data/ngrams`) are compiled into `GreekSwitch.Core.dll` as resources, so the published
exe is truly a single file. The trigram models are generated by `tools/NgramBuilder` from the
dictionaries themselves (affix-expanded) and committed. See
[data/ngrams/README.md](data/ngrams/README.md) for the format, how to regenerate them, and the
`--corpus` hook for adding a real text corpus.

### Layout

```
src/GreekSwitch.Core/      pure logic (net8.0): layouts, decoder, scoring, decision rules, session
src/GreekSwitch.Windows/   hooks, SendInput, layout switching, UIA password detection, worker
src/GreekSwitch.App/       tray app, settings, logging, composition root
tests/                     xUnit + FluentAssertions (Core: 200 tests, Windows: pure helpers)
tools/NgramBuilder/        dictionary/corpus → trigram model
docs/manual-test.md        manual test checklist
```

The static Greek/US layout maps were checked against Windows 11 with `ToUnicodeEx`. At startup
the app re-verifies them against the installed layouts and follows the OS wherever they
differ (each difference is logged).

## Licenses

See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). The Greek dictionary is MPL 1.1 / GPL 2 /
LGPL 2.1, the English dictionary uses the permissive SCOWL license, and WeCantSpell.Hunspell is
MPL 1.1 / GPL 2 / LGPL 2.1.
