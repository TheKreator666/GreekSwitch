# GreekSwitch — Greek/English Layout Auto-Corrector

## What we're building
A Windows tray application that watches keystrokes in real time and, at each word boundary, detects words typed in the **wrong keyboard layout** (Greek vs English) and replaces them with the correct version. It also switches the active layout so the next words come out right.

Example (typed with the wrong layout active for some words):

```
Input:  Θέλω να φτιάξω ένα αππλιψατιον poy ua κάνει πράξεις
Output: Θέλω να φτιάξω ένα application που θα κάνει πράξεις
```

**Core principle:** buffer **physical keys (virtual-key codes)**, not characters. Every word then has two readings, Greek layout and English layout. We pick the more plausible one. Nothing is "translated"; the same keystrokes are re-interpreted.

| Keys | Greek reading | English reading | Winner |
|---|---|---|---|
| A P P L I C A T I O N | αππλιψατιον | application | EN |
| P O Y | πού / που | poy | GR |
| U A | θα | ua | GR |

## Deliverables
1. A working, tested .NET solution (structure below).
2. A **single-file, self-contained Windows executable**: `publish/GreekSwitch.exe`, runnable on a clean Windows 10/11 x64 machine with no .NET installed.
3. A `README.md` covering how to run it, the settings, how undo works, and known limitations.

## Tech stack
- **C# / .NET 8**, nullable enabled, warnings as errors in `Core`.
- **Core:** `net8.0`, pure logic with no Win32 dependencies, so it builds and tests on any OS.
- **Windows layer + App:** `net8.0-windows`, WinForms tray app (`NotifyIcon`). Keep it simple; no WPF unless needed. Set `<EnableWindowsTargeting>true</EnableWindowsTargeting>` so it also *builds* on non-Windows machines.
- **Spellcheck:** `WeCantSpell.Hunspell` (pure managed) with the LibreOffice `el_GR` and `en_US` Hunspell dictionaries. Check their licenses and record them in `THIRD_PARTY_NOTICES.md`.
- **Tests:** xUnit and FluentAssertions.
- **Win32 interop:** `[LibraryImport]` / P/Invoke written by hand, or `Microsoft.Windows.CsWin32`. Your choice; be consistent.

## Solution structure
```
GreekSwitch.sln
src/
  GreekSwitch.Core/          # decoder, scorer, decision rules, word buffer (pure)
  GreekSwitch.Windows/       # keyboard hook, SendInput, layout switching, UIA password detection
  GreekSwitch.App/           # tray icon, settings, exclusions, startup wiring
tests/
  GreekSwitch.Core.Tests/
  GreekSwitch.Windows.Tests/ # only things testable without a live desktop
data/
  dictionaries/              # el_GR.aff/.dic, en_US.aff/.dic
  ngrams/                    # trigram models (generated, see below)
tools/
  NgramBuilder/              # console app: corpus text → trigram frequency file
```

## Architecture

```
WH_KEYBOARD_LL hook ──► Channel<KeyEvent> ──► worker thread
                                              │
                         WordBuffer (VK codes + shift/caps state)
                                              │  at boundary
                         DualDecoder → (greek, english)
                                              │
                         Scorer → scores + context prior
                                              │
                         DecisionEngine → Keep | Replace(text, targetLayout)
                                              │
                         Corrector → SendInput backspaces + unicode text,
                                     switch layout
```

### Core (`GreekSwitch.Core`)
- **`KeyStroke`**: VK code, shift, caps lock, and whether it was a dead key.
- **`WordBuffer`**: appends keys and pops on Backspace. `Reset()` is called on boundaries the Windows layer reports (mouse click, arrows, Home/End, focus change, Ctrl/Alt/Win combos, Esc). A word boundary is Space, Enter, Tab or punctuation; record which delimiter it was so the corrector can re-type it.
- **`LayoutMap`**: VK → character for each layout.
  - Ship a **static map** for the Greek (`00000408`) and US English (`00000409`) layouts. It is used in tests.
  - Greek specifics to handle correctly:
    - `VK_OEM_1` (`;` key) is the **tonos dead key**. `;` + vowel → accented vowel (ά έ ή ί ό ύ ώ). Shift+`;` is dialytika (ϊ ϋ), and the tonos+dialytika combination produces ΐ ΰ.
    - `W` → ς, `S` → σ. Normalize σ/ς when matching against the dictionary, and emit ς at word end when outputting Greek.
    - `Q` on the Greek layout produces `;` (the Greek question mark).
  - The Windows layer must **verify the static map at startup** against the real OS layout using `ToUnicodeEx` and log any mismatch. Treat the OS as the source of truth where they differ, but make the tests pass on the static map.
- **`DualDecoder`**: `(string greek, string english) Decode(IReadOnlyList<KeyStroke>)`. Preserves case patterns (all-caps, Capitalized, lower).
- **`Scorer`**
  - `IDictionaryService.IsWord(string word, Lang lang)`: Hunspell, **accent-insensitive** for Greek (strip tonos and dialytika, normalize final sigma, compare lowercased).
  - `ITrigramModel.LogProb(string word, Lang lang)`: character trigrams with start/end padding and add-k smoothing, normalized by length.
  - Combined: `score = (isWord ? W_dict : 0) + W_ngram * logProb + contextPrior`.
  - `contextPrior`: a small bonus toward the language of the last 3 accepted words.
  - Put all weights in a `ScoringOptions` record so they can be tuned.
- **`DecisionEngine`** returns `Keep` or `Replace`. Rules, in order:
  1. Skip if the word contains digits, `@`, `/`, `:` (URL-ish), `_`, or mixed camelCase.
  2. Skip if the word is on the user's never-convert list.
  3. Skip if the key count is below `MinLength` (default 3), *unless* the alternative is a dictionary word, the current reading is not, and the context prior agrees (this catches `θα`, `να`, `σε`).
  4. Replace only if the current-layout reading is **not** a dictionary word **and** the alternative **is** one, **or** the trigram margin exceeds `ReplaceThreshold`.
  5. Otherwise keep.
- **`UndoTracker`**: remembers the last replacement. If the next key is Backspace, the engine requests a revert (restore the original text and switch the layout back) and adds the word to the never-convert list.

### Windows layer (`GreekSwitch.Windows`)
- **`KeyboardHook`**: `SetWindowsHookEx(WH_KEYBOARD_LL)` on a dedicated thread with its own message loop.
  - **The callback must return within a few milliseconds.** It only enqueues events to a `Channel<T>`. Windows silently unhooks slow callbacks.
  - **Ignore events with `LLKHF_INJECTED`** so the app doesn't process its own `SendInput` output.
- **`MouseHook`** (`WH_MOUSE_LL`): on a click, reset the buffer.
- **`FocusWatcher`**: `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` resets the buffer and resolves the foreground process name for the exclusion list.
- **`PasswordFieldDetector`**: UI Automation (`FocusedElement.Current.IsPassword`). Go fully passive while a password field is focused.
- **`Corrector`**: `SendInput` sends N backspaces (word length plus the delimiter), then the corrected word and delimiter as `KEYEVENTF_UNICODE` events, all in one batch.
- **`LayoutSwitcher`**: switches the foreground window to the target layout (`PostMessage(WM_INPUTLANGCHANGEREQUEST)` to the foreground window). Also reads the current layout via `GetKeyboardLayout(GetWindowThreadProcessId(foreground))`.

### App (`GreekSwitch.App`)
- Tray icon with the menu: Enabled ✓, Pause for 5 min, Settings, Exit.
- `settings.json` in `%APPDATA%\GreekSwitch\` holds: enabled, MinLength, thresholds, the excluded processes, and the never-convert list.
- Default excluded processes: `WindowsTerminal.exe`, `cmd.exe`, `powershell.exe`, `pwsh.exe`, `Code.exe`, `devenv.exe`, `rider64.exe`, plus common games and launchers.
- Single instance (named mutex).
- Optional "Start with Windows" (HKCU Run key). **Off by default.**

## Privacy and security (non-negotiable)
- **No network access of any kind.** No telemetry, no update checks.
- **Never write keystrokes or words to disk**, including logs. The logger may record events such as "replacement made, lang GR→EN, length 11" but **never the text**. Debug builds may log text only behind a `#if DEBUG` + explicit flag.
- Keep the buffer in memory only, and clear it on every reset.
- Be passive in password fields and excluded apps.
- Add a short `SECURITY.md` explaining what the app captures, what it doesn't, and why.

## N-gram models
- `tools/NgramBuilder` takes plain-text corpora and writes `ngrams/el.tri` and `ngrams/en.tri` (a compact text or binary format; your choice, document it).
- For corpus input, generate the training text **from the Hunspell dictionaries themselves**: expand the words with the affix rules where feasible, otherwise use the stems. This avoids downloading external corpora. If a better freely licensed corpus is easy to add later, leave a hook for it.
- Commit the generated model files so the app build doesn't depend on running the tool.

## Tests (must pass before packaging)
`GreekSwitch.Core.Tests` should cover at least:
- **Decoder:** every letter key in both layouts; tonos dead key + each vowel; dialytika; final sigma; Shift and Caps Lock case patterns; `Q` → `;` in Greek.
- **End-to-end sentence tests** (key sequences in, corrected text out). Write a test helper that turns "what the user physically pressed" into `KeyStroke`s:
  - `Θέλω να φτιάξω ένα αππλιψατιον poy ua κάνει πράξεις` → `Θέλω να φτιάξω ένα application που θα κάνει πράξεις`
  - `ηελλο ςορλδ` → `hello world`
  - `καλημέρα` stays unchanged.
  - `hello` stays unchanged.
  - `the` and `ok` stay unchanged (short, valid).
  - `C#`, `user@mail.com`, `v2`, `myVariable` stay unchanged.
  - Mixed sentence: `I love ελλάδα` → unchanged (both are valid words).
- **Undo:** a replacement followed by Backspace yields the original text and adds the word to the never-convert list.
- **Buffer:** a Backspace mid-word pops correctly; a reset clears it.
- **No-text-in-logs test:** run the decision pipeline with a capturing logger and assert that none of the input words appear in the log output.

`GreekSwitch.Windows.Tests`: test only the pure helpers (the `INPUT` struct builder for SendInput, mapping from VK to the static map). Don't test live hooks.

Also add a **manual test checklist** in `docs/manual-test.md` covering Notepad, Word, Chrome, Teams/Slack, a password field, an excluded app (terminal), and undo.

## Build and package
```bash
dotnet restore
dotnet build -c Release
dotnet test -c Release
dotnet publish src/GreekSwitch.App -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -o publish
```
- Dictionaries and n-gram files: either embed them as resources (preferred, for a true single file) or copy them next to the exe. Pick one and document it.
- Add `build.ps1` (Windows) and `build.sh` (cross-platform for restore, build, test and publish) that run the steps above and fail on any test failure.
- Add a GitHub Actions workflow (`windows-latest`) that builds, tests, publishes and uploads `GreekSwitch.exe` as an artifact.
- Note in the README that the exe is unsigned and antivirus tools may flag it because of the keyboard hook plus SendInput. Leave a placeholder step for code signing.

## Development order (milestones)
Work through these in order. Commit after each one, and don't move on while tests are red.
1. **Scaffold:** solution, projects, `.editorconfig`, `Directory.Build.props` (nullable, LangVersion, TreatWarningsAsErrors for Core), and an empty test passing.
2. **Decoder:** `LayoutMap`, `KeyStroke`, `DualDecoder`, and the full decoder test suite.
3. **Dictionaries:** add the Hunspell files, `IDictionaryService` with accent-insensitive Greek matching, and its tests.
4. **N-grams:** `NgramBuilder`, generated models, `ITrigramModel`, and tests that `ψατ`/GR scores low and `cat`/EN scores high.
5. **Decision engine:** rules, context prior, undo, and the sentence tests passing.
6. **Windows layer:** hook, focus, mouse, password detection, corrector, layout switcher, and the `ToUnicodeEx` verification.
7. **App:** tray, settings, exclusions, single instance.
8. **Packaging:** publish the single-file exe, build scripts, CI workflow, README, SECURITY.md, THIRD_PARTY_NOTICES.md, and the manual test checklist.

## Coding conventions
- Small, focused classes. Core has no static mutable state, and everything is injected through interfaces so it stays testable.
- Use `async`/`Channel<T>` for the hook → worker handoff. Never block the hook thread.
- Clean up all Win32 handles (`UnhookWindowsHookEx`, `UnhookWinEvent`) in `Dispose`, and on exit even after a crash (`AppDomain.ProcessExit`).
- Never swallow exceptions on the worker thread. If the pipeline fails, log it (without text) and keep the hook alive.

## Definition of done
- `dotnet test` is green.
- `publish/GreekSwitch.exe` launches on Windows, shows the tray icon, and correctly fixes the example sentence when typed in Notepad.
- Undo works; terminals and password fields are untouched.
- No keystroke text is written anywhere on disk.
