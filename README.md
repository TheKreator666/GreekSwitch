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
   .NET install. There is one exe for Windows 10/11 x64 (`win-x64`) and one for ARM64 laptops
   (`win-arm64`). An "αb" icon appears in the tray. It turns blue after
   about a second, once the dictionaries have loaded.
3. Type. Right-click the tray icon for the menu:
   - **Enabled ✓**: master on/off switch (saved).
   - **Pause for 5 min**: click again to resume early.
   - **Settings…**: also opens on a double-click of the icon.
   - **Exit**

   The icon flashes green for a moment when it corrects a word (Settings can turn that off).
   On the first run a short dialog checks that both layouts are installed, says what is
   captured, lists the hotkeys and offers *Start with Windows*.

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
3. remembers the word so it's left alone from now on. It stores only a keyed hash, never the
   text, in case the "word" was a password (see [SECURITY.md](SECURITY.md)).

If the correction also fixed the words just before it (see *Earlier words* below), one
Backspace reverts the whole batch and remembers each of those words.

Type Space and carry on. To forget the learned words, use Settings → *Forget learned words*.
Words you type into the Settings list yourself are kept as readable text. Any other key
after a correction (or a click, arrow key, focus change and so on) disarms undo, and Backspace
becomes an ordinary Backspace again.

## Hotkeys

Detection will never be perfect, so there are three hotkeys. Change them in Settings (leave one
empty to turn it off); if another program already owns one, Settings says so.

| Default | Does |
|---|---|
| **Ctrl+Alt+Space** | **Convert last word**: converts the word you're typing, or the one you just finished, to the other layout, even if GreekSwitch kept it. Backspace right after undoes it. GreekSwitch learns from it (as a hash): a word it kept is converted automatically from then on; a word it had converted is left alone. |
| **Ctrl+Alt+Shift+Space** | **Convert selection**: converts the selected text, for a whole sentence typed in the wrong layout. It copies the selection, converts it, pastes it back, switches the layout and restores your clipboard (see [SECURITY.md](SECURITY.md#the-clipboard)). |
| **Ctrl+Alt+Shift+G** | **Turn on/off**: the same as the tray menu's *Enabled*. |

The convert hotkeys do nothing where GreekSwitch is passive (password fields, password
managers, excluded apps, while disabled or paused). Convert selection is never used in a
terminal, where Ctrl+C would interrupt the running command.

## Settings

The Settings dialog edits `%APPDATA%\GreekSwitch\settings.json`. You can also edit the file by
hand while GreekSwitch is closed:

| Key | Default | Meaning |
|---|---|---|
| `enabled` | `true` | Master switch (same as the tray checkbox). |
| `minLength` | `3` | Words with fewer keys are only converted when the other reading is a dictionary word, the current one isn't, **and** the last few words were in the other language. This is what lets `ua` → `θα` happen mid-sentence. |
| `replaceThreshold` | `2.0` | Score margin needed to replace a word when no dictionary settles it. Higher means more cautious. |
| `dictionaryWeight` / `ngramWeight` / `contextWeight` | `2.0` / `1.0` / `0.3` | Scoring weights (see *How it decides*). File only; not in the dialog. |
| `layoutSwitchSettleMs` | `50` | Pause after an app reports a layout switch, before retyping the word (0–250). File only; takes effect on restart. See *Known limitations*. |
| `correctOnEnterAndTab` | `false` | Also correct words ended by Enter/Tab. Off because by the time GreekSwitch sees the key, Enter may already have sent a chat message and Tab may have moved focus. |
| `excludedProcesses` | terminals, IDEs, game launchers and common games | Executable names where GreekSwitch is completely passive. The defaults include `WindowsTerminal.exe`, `cmd.exe`, `powershell.exe`, `pwsh.exe`, `Code.exe`, `devenv.exe` and `rider64.exe`. "Restore default exclusions" in the dialog brings the list back. Password managers are **always** excluded, built in, and not part of this list. |
| `neverConvert` | `[]` | Words never to convert that you typed into Settings (accent- and case-insensitive). |
| `neverConvertLearned` / `neverConvertKey` | | Words learned from undo, as HMAC hashes, and their key, encrypted with DPAPI for your Windows account. Not meant to be edited; *Forget learned words* in Settings clears them. Hashes copied to another machine or account don't work there. |
| `alwaysConvertLearned` | | Words you converted with the *Convert last word* hotkey after GreekSwitch kept them, as HMAC hashes (same key). Cleared by *Forget learned words*. |
| `convertLastWordHotkey` / `convertSelectionHotkey` / `toggleHotkey` | `Ctrl+Alt+Space` / `Ctrl+Alt+Shift+Space` / `Ctrl+Alt+Shift+G` | The hotkeys (see *Hotkeys*). Empty turns one off. |
| `flashOnCorrection` | `true` | Flash the tray icon green for a moment after a correction. |
| `fixAccidentalCapsLock` | `false` | A word typed with Caps Lock on by accident (`hELLO`) becomes `Hello`, and Caps Lock is turned off. |
| `suggestLoanwords` | `true` | Suggest the English spelling of loanwords typed in Greek (`κοντέινερ` → `container`). Reserved for the loanword feature; the policy `DisableLoanwordSuggestions` forces it off. |
| `firstRunCompleted` | `false` | The first-run dialog has been shown. Existing installs are set to `true` on upgrade. |
| `debugLogText` | `false` | **Debug builds only**: log the words themselves. Compiled out of Release builds. |

Start with Windows lives in the registry, not in this file. An organization can fix some of
these values with policy keys (see *For IT*); Settings then shows them disabled and marked
*Set by your organization*, and settings.json keeps your own choice for when the policy is removed.

## How it decides

At each boundary (Space or punctuation that's the same in both layouts: `. , ! ? ( ) [ ] { } " -`)
the word's keys are decoded in both layouts. The Greek reading follows the real dead-key rules:
`;` + vowel → ά έ ή ί ό ύ ώ, Shift+`;` + ι/υ → ϊ ϋ, Shift+W + ι/υ → ΐ ΰ. Each reading is scored as

```
score = (isDictionaryWord ? dictionaryWeight : 0) + ngramWeight × trigramLogProb + contextPrior
```

- **Dictionary:** every word form of the LibreOffice Hunspell dictionaries `el_GR` and `en_US`,
  expanded with their affix rules at build time and stored as hashes (`data/lexicon`). Greek matching
  ignores accents and the σ/ς form at the end of a word, so `καλημερα`, `ΚΑΛΗΜΕΡΑ` and `πράξεισ`
  all count. Two things are *not* ignored. A `ς` before another letter (`ςασ`, which is "was"
  typed in the Greek layout) is never Greek. A lowercase reading doesn't match an entry that
  only exists capitalised (`na` isn't English because of `Na`, `ελλάδα` isn't the name
  `Ελλάδα`); capitalised input still matches names.
- **Trigrams:** character trigram models for each language, averaged per character.
- **Word frequency:** a Zipf table of the ~50k most frequent words per language (wordfreq).
- **Context prior:** a small bonus toward the language of the last 3 accepted words.

The rules, in order:

1. **Skip code-like words**: digits or symbols such as `@ / _ #`, no letters at all, camelCase
   in both readings, a word glued to a preceding `x.` (domains such as `mail.com`), or a word
   during which you switched layout yourself (Alt+Shift, Win+Space).
2. **Skip never-convert words.**
3. **Both readings are words** (`den`/`δεν`, `toy`/`του`): replace only if the last words were in
   the other language **and** the other reading is far more common (Zipf at least 1.5 higher).
   Single letters are never replaced this way.
4. **Skip short words** (< `minLength` keys) unless the other reading is a *common* word
   (Zipf ≥ 5), the current one isn't a word, and the context agrees. So `ua` becomes `θα` mid
   sentence, but `tv` and `ai` never become the rare `τω`, `αι`.
5. **Replace** if the current reading isn't a word but the other one is, **or** neither is a word
   and the other reading's combined score beats the current one by more than
   `replaceThreshold`. A reading that is a dictionary word is never replaced on letter
   statistics alone.
6. Otherwise **keep**.

**Earlier words.** Context only exists once something has been corrected, so the first words
of a sentence typed in the wrong layout would otherwise stay wrong (`den θέλω`). After a
confident correction (dictionary evidence, or frequency plus context), the last 1–3 words of
the same run (same field, separated only by spaces, no click or other reset in between) are
judged again with the new context. The ones that now convert are fixed in the same batch:
`den u;elv` → `δεν θέλω`. It stops at the first earlier word that stays as it is.

A Greek replacement gets a final ς at the end of the word. To apply it, the corrector:

1. posts `WM_INPUTLANGCHANGEREQUEST` to switch the foreground window to the target layout,
2. waits until the focused window reports the new layout, plus `layoutSwitchSettleMs`,
3. sends one `SendInput` batch: Backspaces for the word and the delimiter, then the **real key
   presses** that type the new word and delimiter in the target layout (tonos via the `;` dead
   key, ς via W, with Shift and Caps Lock handled).

Real keys rather than Unicode characters: Windows 11 Notepad turns injected Unicode text typed
around a layout switch into spaces or repeated letters. It also keeps translating keys with the
old layout for a few tens of milliseconds after it reports the switch. Hence the settle pause:
in end-to-end runs 0 ms failed, while 30–60 ms worked every time. Unicode is used only for a
character no key types.

Measured by the evaluation harness (see *Measuring decision quality*) over the 5,000 most
frequent words of each language, **weighted by how often they occur**:

| | Greek | English |
|---|---|---|
| Typed correctly: wrongly changed | 0.01% | 0.00% |
| Typed in the wrong layout: corrected, no context / with context | 69% / 87% | 73% / 90% |
| Real sentences, wrong layout from a random word on: corrected / correct words changed | 87% / 0.04% | 86% / 0.43% |

Latin words typed on purpose inside Greek (`email`, `pdf`, `Teams`, `tv`, `ai`): 0 of 118
changed. What's left uncorrected is mostly single letters, true ties such as `to`/`το`, and
names that read as words in both languages (`Tom`/`Τομ`).

## Privacy

There is no network access of any kind, no telemetry and no update checks. Keystrokes and words
are never written to disk. The only readable words ever stored are the ones you type into the
never-convert list yourself; words learned from undo are stored only as keyed hashes.
GreekSwitch is fully passive in password fields (detected with UI Automation), in excluded
apps, and always in password managers. A correction is only typed into the control the word
was typed into, and Enter is never retyped into a terminal. Details: [SECURITY.md](SECURITY.md).

The event log is `%LOCALAPPDATA%\GreekSwitch\greekswitch.log` (rolled at 1 MB). It records
events such as `Replacement made, lang GR→EN, length 11, reason Dictionary`, but never the
text. A unit test enforces this.

## Known limitations

- **Typing very fast right after a word.** A correction needs roughly 60–70 ms: the layout
  switch, the settle pause, then the keys. If you type another letter before it's sent, the
  correction is skipped (and logged) so its Backspaces can't delete your new letter, and the
  word stays as typed. In end-to-end tests at normal typing speed (~150 ms between keys) every
  correction landed.
- **If a correction comes out in the wrong layout** (an app that applies layout switches more
  slowly than Notepad), raise `layoutSwitchSettleMs` in `settings.json`. Values much above
  100 ms start to collide with your next keystroke.
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
- **Memory and size.** About 90 MB of RAM in use (the dictionaries are hashed word lists of
  ~7 MB; the rest is .NET and WinForms). The self-contained exe is about 70 MB.
- **Dead-key edge cases.** A dead key followed by Space types a lone accent in Greek; such words
  are left as they are.
- **Caps Lock** is tracked from key events after start-up. If it gets out of sync (rare, e.g.
  toggled from an on-screen keyboard), toggle it once.

## For IT

**What to deploy.** One self-contained exe per architecture, nothing to install, no admin
rights needed (it runs `asInvoker` and never asks for elevation):

| Build | Machines |
|---|---|
| `publish/win-x64/GreekSwitch.exe` | Windows 10/11 on Intel/AMD |
| `publish/win-arm64/GreekSwitch.exe` | Windows 11 on ARM (Snapdragon etc.). The x64 exe also runs there under emulation, but the native one is faster and lighter. |

Each comes with a `GreekSwitch.exe.sha256`. There is **no MSI or MSIX package**: the single exe is
the deliverable. Software-distribution tools (Intune, SCCM/MECM, PDQ, a logon script) can copy it
to e.g. `%LOCALAPPDATA%\Programs\GreekSwitch\` or a read-only share and, if wanted, add it to
`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. It writes only to `%APPDATA%\GreekSwitch`,
`%LOCALAPPDATA%\GreekSwitch` and two HKCU values (see [SECURITY.md](SECURITY.md#what-it-writes-to-disk)).

**Requirements.** Each user needs both a **Greek** and an **English** keyboard layout installed
(GreekSwitch only switches between layouts that exist; the first-run dialog says if one is
missing). No .NET install, no network access, no open ports, pipes or services.

**Verifying the exe.** The exes are **not code-signed yet** (the build has placeholder steps for
both). Compare the hash with the published `.sha256` before allow-listing or deploying:

```powershell
Get-FileHash .\GreekSwitch.exe -Algorithm SHA256   # must equal the hash in GreekSwitch.exe.sha256
```

**Why antivirus or EDR may flag it.** GreekSwitch installs a global low-level keyboard hook
(`SetWindowsHookEx(WH_KEYBOARD_LL)`) and types corrections with `SendInput`. Keyloggers and macro
tools use the same two APIs, so heuristic scanners may flag it, and unsigned binaries get less
benefit of the doubt. [SECURITY.md](SECURITY.md) documents exactly what is captured (keys of the
current word, in memory only), and that nothing is logged as text or sent anywhere. Allow-list it
by hash (or by path) in your AV/EDR.

**AppLocker / WDAC.** Until the exe is signed, allow it with a **file hash rule** (re-created for
each release; take the hash from the `.sha256` file or from `Get-AppLockerFileInformation`) or a
**path rule** for a folder standard users can't write to (e.g. under `%ProgramFiles%` or a
read-only share; a path rule on a user-writable folder lets any exe placed there run). Once
releases are signed, a **publisher rule** covers every future version. GreekSwitch loads no
plug-ins and extracts nothing to disk at run time, so no extra DLL rules are needed.

**Apps run as administrator.** Windows (UIPI) doesn't let a standard-user process send input to an
elevated window. GreekSwitch skips corrections there and logs it; everything else keeps working.

**Remote Desktop, Citrix and VM consoles** are excluded by default: keys sent to them are decoded
with the local layout while the remote side has its own. Run GreekSwitch inside the remote
session instead.

### Policy keys

Optional values under **`HKLM\Software\Policies\GreekSwitch`** (machine) and
**`HKCU\Software\Policies\GreekSwitch`** (user). For each value, HKLM wins over HKCU, and both win
over the user's settings. Policies are read at start-up and whenever Settings is opened; Settings
shows each one disabled and marked *Set by your organization*. The user's own settings.json values
are kept underneath and come back if the policy is removed.

| Value | Type | Effect |
|---|---|---|
| `Enabled` | `REG_DWORD` | `1` = always on, `0` = always off. The tray menu's *Enabled*, the toggle hotkey and Settings can't change it (the attempt is logged). *Pause for 5 min* still works while it is on. |
| `ExcludedProcesses` | `REG_MULTI_SZ` | Replaces the user's list of excluded apps (one `.exe` name per line, case-insensitive). An empty value means "no excluded apps". |
| `AdditionalAlwaysExcluded` | `REG_MULTI_SZ` | Added to the built-in, always-excluded password managers. Values from HKLM and HKCU are combined. Nothing can switch GreekSwitch on in these apps. |
| `DisableUndoLearning` | `REG_DWORD` | `1` = no new words are learned from undo or from the *Convert last word* hotkey (corrections are still undone and converted). Hashes learned before the policy still apply; *Forget learned words* in Settings still deletes them. |
| `DisableLoanwordSuggestions` | `REG_DWORD` | `1` = loanword suggestions (`κοντέινερ` → `container`, a coming feature) are off. |

A value of the wrong type is ignored (a `REG_SZ` `"0"`/`"1"`, or a `;`-separated `REG_SZ` list,
is accepted too). Example `.reg`:

```reg
Windows Registry Editor Version 5.00

[HKEY_LOCAL_MACHINE\Software\Policies\GreekSwitch]
"Enabled"=dword:00000001
"DisableUndoLearning"=dword:00000001
"DisableLoanwordSuggestions"=dword:00000001
; REG_MULTI_SZ as hex(7): UTF-16LE strings, each ending in 00,00, the list ending in 00,00.
; This is "CorpVault.exe" followed by "PayrollClient.exe".
"AdditionalAlwaysExcluded"=hex(7):43,00,6f,00,72,00,70,00,56,00,61,00,75,00,6c,00,74,00,2e,00,\
  65,00,78,00,65,00,00,00,50,00,61,00,79,00,72,00,6f,00,6c,00,6c,00,43,00,6c,00,69,00,65,00,\
  6e,00,74,00,2e,00,65,00,78,00,65,00,00,00,00,00
```

Or with PowerShell (as administrator for HKLM):

```powershell
$key = "HKLM:\Software\Policies\GreekSwitch"
New-Item $key -Force | Out-Null
New-ItemProperty $key -Name Enabled -PropertyType DWord -Value 1 -Force
New-ItemProperty $key -Name ExcludedProcesses -PropertyType MultiString -Value "cmd.exe","pwsh.exe","mstsc.exe" -Force
```

There is no ADMX template yet; Group Policy Preferences (Registry items) can set these values.

## Building from source

Requires the .NET 8 SDK (or newer). Tests and the core library build on any OS, and the Windows
projects build anywhere thanks to `EnableWindowsTargeting`.

```bash
dotnet restore
dotnet build -c Release
dotnet test -c Release
dotnet publish src/GreekSwitch.App -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -o publish/win-x64
# and the same with -r win-arm64 -o publish/win-arm64
```

Or run `./build.ps1` (Windows) or `./build.sh` (any OS). Both run all of the above for both
architectures, write `GreekSwitch.exe.sha256` next to each exe
(`publish/win-x64/`, `publish/win-arm64/`), and stop on the first failing test.
`-SkipPublish` / `--skip-publish` stop after the tests. GitHub Actions
(`.github/workflows/build.yml`, `windows-latest`) does the same and uploads the two exes as the
artifacts `GreekSwitch-win-x64` and `GreekSwitch-win-arm64`. It also has a placeholder step for
code signing both.

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
src/GreekSwitch.App/       tray app, settings, IT policy keys, logging, composition root
tests/                     xUnit + FluentAssertions (Core: 200 tests, Windows: pure helpers)
tools/NgramBuilder/        dictionary/corpus → trigram model
docs/manual-test.md        manual test checklist
```

The static Greek/US layout maps were checked against Windows 11 with `ToUnicodeEx`. At startup
the app re-verifies them against the installed layouts and follows the OS wherever they
differ (each difference is logged).

## Measuring decision quality

`tests/GreekSwitch.Evaluation` (test-only, never shipped) runs the decision engine over:

- the 5,000 most frequent words of each language, **weighted by frequency**, typed correctly and
  typed in the wrong layout, with and without context;
- Latin-script words Greeks type on purpose (`email`, `pdf`, `Teams`, `tv`…) inside Greek text;
- 400 real sentences per language, typed as if the user forgot to switch layout from a random
  word on, with the app's layout switches applied to the following words;
- the loanword detector (milestone 15): how often it would show a suggestion on ordinary Greek
  (the whole frequency-weighted Greek list, and the Greek sentences: the nuisance rate, gated
  like the false positives), how often tier 2 would replace ordinary Greek, and how many of the
  English words written in Greek letters in `Data/loanwords.tsv` get the right suggestion;
- `Data/regression.tsv`: hand-written mixed sentences (names, acronyms, punctuation, elision,
  capitals, a layout switched mid-word). Known failures are tagged `xfail`.

It prints the full table (also written to `evaluation-report.md` next to the test binaries and
to the CI job summary), and fails if any "wrongly changed" or loanword "suggested" rate rises above
`Data/baseline.json`. After an intended improvement, regenerate the baseline with
`GREEKSWITCH_UPDATE_BASELINE=1 dotnet test tests/GreekSwitch.Evaluation`, and commit it along
with the before/after numbers.

Data sources: word frequencies from **wordfreq** (data CC BY-SA 4.0), exported by
`tools/FrequencyExport/export_wordfreq.py` into `data/frequency/`; sentences from **Tatoeba**
(CC BY 2.0 FR), sampled by `tools/FrequencyExport/sample_tatoeba.py`. See THIRD_PARTY_NOTICES.md.

## Licenses

See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). The Greek dictionary is MPL 1.1 / GPL 2 /
LGPL 2.1, the English dictionary uses the permissive SCOWL license, and WeCantSpell.Hunspell is
MPL 1.1 / GPL 2 / LGPL 2.1.
