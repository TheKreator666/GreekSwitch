# GreekSwitch

Windows tray app (.NET 8, WinForms) that watches physical keystrokes, and at each word boundary
re-reads the word's keys in the other layout (Greek ↔ English). If the word was typed in the
wrong layout it deletes it, types the right word, and switches the layout.

```
Typed:  Θέλω να φτιάξω ένα αππλιψατιον poy ua κάνει πράξεις
Result: Θέλω να φτιάξω ένα application που θα κάνει πράξεις
```

**Status:** milestones 9–15 are built (evaluation harness, decision quality, retroactive
correction, robustness, hotkeys/UX, office deployment, loanword suggestions); milestone 16 has
design notes only. The code is the source of truth for how things work; this file records what
must stay true and what to do next.

**Target machines:** ordinary office laptops — Windows 10/11 x64 (some ARM), standard user
without admin rights, antivirus/EDR, sometimes AppLocker/WDAC, often Remote Desktop or Citrix.
Nothing may need installing besides the exe, and both Greek and English layouts must be installed in Windows.

## Commands

```bash
dotnet build GreekSwitch.sln -c Release
dotnet test GreekSwitch.sln -c Release                      # Core tests run on any OS; Windows tests need Windows
dotnet test tests/GreekSwitch.Core.Tests -c Release --filter "FullyQualifiedName~SentenceTests"
./build.ps1        # Windows: restore, build, test, publish → publish/win-x64/ and publish/win-arm64/ GreekSwitch.exe (+ .sha256)
./build.sh         # same, any OS (publish cross-targets win-x64 and win-arm64)
```

Regenerate the trigram models with the commands in `data/ngrams/README.md`. Commit the output.

## Pipeline (as built)

```
KeyboardHook (hook thread, WH_KEYBOARD_LL) ─► Channel<InputEvent> ─► CorrectionWorker (worker task)
  MouseHook, FocusWatcher (WinEvents) ──────┘                          │ passive? excluded app / password field / paused
                                                                       ▼
TypingSession: KeyClassifier → WordBuffer (VK codes) → at a delimiter: DecisionEngine
  DecisionEngine: DualDecoder (both readings) → Scorer (hashed lexicon + trigrams + Zipf + ContextTracker) → rules → Keep | Replace
  Replace → Corrector (ICorrectionOutput): guards → switch layout first → KeyPlanner real keys → one SendInput batch
  UndoTracker: Backspace right after a correction reverts it and learns the word (as an HMAC hash)
```

| Area | Files |
|---|---|
| Key maps, decoding, retyping plan | `src/GreekSwitch.Core/Layout/` (`StaticLayouts`, `LayoutMap`, `DualDecoder`, `KeyPlanner`) |
| Word buffer, key classification | `src/GreekSwitch.Core/Input/` |
| Scoring, dictionaries, trigrams | `src/GreekSwitch.Core/Scoring/`, `src/GreekSwitch.Core/Text/` |
| Decision rules, never-convert, undo | `src/GreekSwitch.Core/Decisions/` |
| Per-key pipeline | `src/GreekSwitch.Core/Session/TypingSession.cs` |
| Hooks, worker | `src/GreekSwitch.Windows/Hooks/`, `src/GreekSwitch.Windows/CorrectionWorker.cs` |
| Output and its guards | `src/GreekSwitch.Windows/Output/` (`Corrector`, `InputBuilder`, `TypeAheadGuard`, `FocusGuard`) |
| Layout read, switch, startup verification | `src/GreekSwitch.Windows/Layouts/` |
| Focus, password fields, terminals | `src/GreekSwitch.Windows/Focus/` |
| Tray, settings, logging, composition root | `src/GreekSwitch.App/` (`GreekSwitchHost` wires everything) |
| IT policy keys (`HKLM`/`HKCU\Software\Policies\GreekSwitch`) | `src/GreekSwitch.App/Policy/` (`ManagedPolicy`, `IPolicyRegistry`), applied by `AppPolicy` |
| Trigram model builder | `tools/NgramBuilder/` |
| Loanword matching (phonemes, index, matcher, tiers, pair store) | `src/GreekSwitch.Core/Loanwords/` (`LoanwordDetector` is the entry point) |
| Phonetic index builder (CMUdict → `data/phonetic/en.idx`) | `tools/PhoneticIndexBuilder/` |
| End-to-end smoke test (manual, takes over the keyboard; not in the solution) | `tools/EndToEndDriver/` |
| Tests | `tests/GreekSwitch.Core.Tests` (incl. `Support/TypingSimulator`), `tests/GreekSwitch.Windows.Tests` |
| Suggestions: popup, caret, Tab gate | `src/GreekSwitch.App/UI/SuggestionPopup.cs`, `src/GreekSwitch.App/SuggestionSink.cs`, `src/GreekSwitch.Windows/Hooks/SuggestionGate.cs`, `src/GreekSwitch.Windows/Focus/CaretLocator.cs` |
| Hotkeys, convert selection, first run | `src/GreekSwitch.App/UI/` (`HotkeyWindow`, `SelectionConverter`, `FirstRunForm`) |
| Docs | `README.md`, `SECURITY.md`, `THIRD_PARTY_NOTICES.md`, `docs/manual-test.md`, `docs/loanword-design.md`, `docs/hold-and-release-design.md`, `docs/layouts-design.md`, `docs/greeklish-design.md` |

## Invariants: do not break these

**Privacy**
- No network access of any kind.
- No typed text in logs. Log calls take languages, lengths, reasons and exception *types* only;
  never exception messages. `NoTextInLogsTests` must stay green and must cover any new log call
  on the typing path.
- Words learned from use (undo today; accepted suggestions later) are stored only as
  HMAC-SHA256 hashes (key DPAPI-protected). The only readable words on disk are ones the user
  typed into Settings, plus dictionary-derived replacement targets.
- When capture is off (disabled, paused, excluded app, password field) the hook enqueues nothing.
  `AppSettings.AlwaysExcludedProcesses` (password managers) can't be overridden by settings.
- `WordBuffer` wipes its array. Don't add state that keeps typed text beyond the current word,
  the single armed `UndoRecord`, the single pending suggestion (until the next key), the last
  completed word (convert-last-word hotkey), (milestone 11) the keys of the last few words of the current run,
  the text since the last pause (`AccentRun`, ≤ 16 words) and key events held during a send (`KeyHold`, ≤ 200 ms).
- Keep the WER exclusion and the `DropUnusedWpfNatives` target: nothing is extracted to disk at run time.
- Any change to what is captured, stored or logged updates `SECURITY.md` in the same commit.

**Input safety**
- The hook callback only reads key/modifier state, enqueues, and returns. No COM/UI Automation,
  no I/O, no locks, no waiting. Windows silently removes slow low-level hooks. The hook never
  swallows keys, with two exceptions: a plain Tab (and its key-up) while a suggestion is visible,
  decided by reading the single `SuggestionGate` flag (if the suggestion is gone when the worker
  sees it, the worker sends the Tab on); and key events that arrive while a correction is being
  sent (`KeyHold`: lock-free enqueue, replayed in order by the corrector, hard 200 ms cap).
- Never retype while the user is typing when it can wait: missing accents are added at a pause
  (`TypingSession.OnPause`), not per word, and fixed in place (`Corrector.Retouch`: arrows to
  the changed letters, retype only those, arrows back; no layout switch). See `docs/accent-and-input-issues.md`.
- Never switch the layout right after sending keys without knowing the app has processed them:
  the switch request is a posted message and reaches the app before queued input, so the keys
  are then read with the new layout (`fti;ajv`).
- Ignore events with `LLKHF_INJECTED` (our own output also carries `InputBuilder.Signature`).
- A correction either happens completely or not at all. `Corrector.Replace` checks, in order:
  modifier held → focus moved → Enter/Tab into a command window → Shift held → switch layout
  and wait for it → user typed ahead → focus moved again. Any failure returns false and sends nothing.
- Switch the layout *before* typing, and type **real keys** of the target layout (`KeyPlanner`).
  Unicode injection is only the fallback; Windows 11 Notepad mangles it right after a switch.
- Never retype Enter or Tab into a terminal or IDE (`CommandWindows`).
- The OS layout wins over `StaticLayouts` (`LayoutVerifier`); tests use the static maps.
- Run as a standard user (`asInvoker`); never request elevation, open ports, pipes or other endpoints.

**Decision quality**
- False positives (changing a correctly typed word) cost far more than misses. Any change to
  scoring, rules or data must be measured with the evaluation harness (milestone 9) and must not
  raise the false-positive rate. Put before/after numbers in the commit message.

## Baseline measurements (2026-10-24, C# evaluation harness, milestone 9)

`tests/GreekSwitch.Evaluation`: the 5,000 most frequent words of each language (wordfreq),
weighted by frequency, each judged in isolation with the stated context; plus 400 Tatoeba
sentences per language. (An earlier Python port gave 0.03% / 68% / 70% / 72% / 85%.)

| Scenario | M9 baseline | After milestone 10 |
|---|---|---|
| Greek typed correctly, Greek context: words wrongly changed | 0.02% | 0.01% |
| English typed correctly, English context: words wrongly changed | 0.00% | 0.00% |
| Latin tokens inside Greek prose: wrongly changed (unweighted, 118 tokens) | 1.69% (`tv`, `ai`) | 0.00% |
| Greek typed in the EN layout: corrected (no context / Greek context) | 67.7% / 70.1% | 69.0% / 87.3% |
| English typed in the GR layout: corrected (no context / English context) | 71.8% / 84.5% | 73.1% / 89.6% |
| Sentences, Greek: wrong-layout words corrected / correct words wrongly changed | 73.8% / 0.05% | 83.2% / 0.04%; 86.6% after M11 |
| Sentences, English: wrong-layout words corrected / correct words wrongly changed | 78.5% / 0.43% | 83.2% / 0.43% (names: `Sami`, `Rima`); 85.6% after M11 |

Word-level rows type a name capitalised when its only dictionary entry is capitalised (from 10b
on; the lowercase frequency lists otherwise overstate false positives).

Loanword detector (milestone 15), default options: a suggestion on
0.15% of words of the frequency-weighted Greek list (all ~43k words) and 0.22% of the words of the
Greek sentences; with tier 2 on, 0.00% (weighted) replaced. 92.9% of the 70 hand-written
transliterations in `Data/loanwords.tsv` get the right suggestion. Both nuisance rates are gated.

What's missed is the most frequent words, because their other reading is also "a word":
`to`(το) `na`(να) `toy`(του) `me`(με) `den`(δεν) `ton`(τον) `soy`(σου) `saw`(σας) `mia`(μια),
and `was`(ςασ) `are` `one` `set` `top` `stop`. Known false positives: `tv`→`τω` and `ai`→`αι`
in Greek context (short-word rule), `Τραμπ`→`Tramp` (a Greek name not in el_GR whose other
reading is an English word), lowercase acronyms such as `οηε`. Office jargon typed in the EN
layout inside Greek text (`ok`, `pc`, `email`, `pdf`, `Teams`, `Excel`) and Greek loanword
verbs (`τσεκάρω`, `ποστάρω`) were kept correctly.

## Next milestones (in order)

Commit after each milestone and keep tests green. Each milestone ends with updated docs
(README, SECURITY.md if privacy-relevant, THIRD_PARTY_NOTICES.md if a dependency or data file
was added, `docs/manual-test.md` if Windows behaviour changed, and this file).

### 9. Frequency-weighted evaluation harness
The current `FalsePositiveTests` sample the dictionaries uniformly and skip words under 3
letters and capitalised names. That measures the easy cases. Running text is dominated by
short function words, names and acronyms.
- Add `tests/GreekSwitch.Evaluation` (test-only, never shipped) that reads a frequency list per
  language (word + count). Use wordfreq's exported lists or another corpus; record the source and
  license in the project README. wordfreq data is CC BY-SA 4.0, which is fine for test data.
- Word-level scenarios, each reported frequency-weighted with the top offenders listed:
  - correct Greek with Greek context;
  - correct English with English context;
  - Latin tokens inside Greek prose;
  - each language typed in the wrong layout, with no context and with the intended language as context.
- Sentence-level simulation (through `TypingSimulator`, layout switches followed): take real
  sentences and start typing in the wrong layout at a random word. Report corrected words, wrongly
  changed words and words left wrong.
- A hand-written regression file of mixed sentences with real capitalisation:
  - names and acronyms: `Τραμπ`, `ΟΗΕ`, `ΔΕΗ`;
  - office words typed in the EN layout: `Teams`, `Excel`, `email`, `pdf`, `tv`, `ai`;
  - Greek loanword verbs: `τσεκάρω`, `ποστάρω`;
  - punctuation around words: `poy,`, `(ua`, and `tiq` (= `τι;`);
  - apostrophes and elision: `don't`, `σ' αγαπώ`, `απ' το`;
  - sentence-initial capitals and ALL-CAPS words;
  - a word where the user switches layout halfway (Alt+Shift or Win+Space). It must be left alone.
- CI gate: fail if a false-positive rate rises above its committed baseline. Print the full table.

### 10. Decision-quality fixes (measure each one separately)
a. **Final-sigma position is evidence.** A Greek reading with ς anywhere except the end (`ςασ`,
   `ςαντ`, `ςε`) is not Greek. Don't normalise it away in `HunspellDictionaryService.IsGreekWord`
   or `TrigramText`. Expected: a small recall gain for English typed in the GR layout and no false-positive cost.
b. **Case-aware dictionary matching.** A lowercase reading must not match only a capitalised
   entry: `na`/`Na`, `se`/`Se`, `ti`/`Ti`, `ta`/`Ta` and names. Remove the `Capitalize(...)`
   fallbacks for lowercase input in both languages; keep them for capitalised input. Expected:
   Greek recall in context +6 points. Verify on the capitalised regression file (milestone 9),
   because lowercase frequency lists overstate the cost.
c. **Frequency plus context when both readings are words.** Embed a compact frequency table
   (Zipf, top ~50k words per language). License and attribution go in THIRD_PARTY_NOTICES.md;
   CC BY-SA data (e.g. wordfreq) can be embedded but the table itself stays under CC BY-SA.
   Rule: ≥ 2 keys, both readings are words, context majority is the other language, and the other
   reading's Zipf is ≥ 1.5 higher → replace. Expected: recall in context of about 86% for Greek
   and 89% for English, at the same false-positive rate. Single letters stay excluded.
d. **Short-word rule requires a frequent alternative** (e.g. Zipf ≥ 5), so rare/archaic entries
   (`τω`, `αι`) can't trigger it. This removes the `tv`/`ai` false positives.
e. Make the "both readings are dictionary words → never replaced by the trigram margin" behaviour
   explicit in `DecisionEngine` and covered by a test. The README claims it; today only the margin size keeps it true.

### 11. Retroactive correction of the previous word(s)
Context is only established after the first confident correction, so a wrong-layout sentence
keeps its first function words: `den θέλω`. Keep the last 1–3 completed words of the current
run (same field, no reset, separated only by spaces; keys only, wiped on reset). After a
confident replacement, re-judge them with the new context. If they now convert, fix them in the
same SendInput batch (one backspace count across the run). Undo must revert the whole batch.
Same guards apply. Add tests in `TypingSimulator`.

### 12. Robustness
- **Check the text before replacing it.** Other tools can rewrite a word after you press Space:
  Word AutoCorrect, Windows text suggestions, Grammarly, autocomplete. Blind backspaces would then
  delete the wrong characters. Where the focused control supports UI Automation `TextPattern`,
  read the text before the caret (on the UIA thread below, with a short timeout) and abort the
  correction if it doesn't end with the expected word + delimiter. Where it isn't supported,
  keep today's guards; don't drop to suggest-only.
- **Capture the layout in the hook.** The hook already calls `KeyboardLayouts.ForegroundLayout()`;
  pass the HKL in `InputEvent` and use it in `CorrectionWorker.OnKey` instead of reading the
  layout when the worker gets to the key (wrong if the worker lags and the user switched).
- **Health and watchdog.** If the worker task faults or the hook stops delivering keys while
  `GetLastInputInfo` shows keyboard activity, reinstall the hook or restart the worker, and show an
  error state on the tray icon. Today the tray says "on" even when the worker is dead.
  Reset the buffer on lock, unlock and resume (`WTSRegisterSessionNotification`, `WM_POWERBROADCAST`).
- **UI Automation off the hot path.** `PasswordFieldDetector` runs on every focus event on the
  worker thread. Slow apps then delay key processing, and every query keeps Chrome's
  accessibility tree switched on. Move it to its own thread with a timeout. Cache the result per
  focused HWND, and only query while capture would otherwise be on. Until it answers, treat the
  field as a password field (fail closed).
- **Remote sessions and VMs.** Keys sent to `mstsc.exe`, Citrix (`wfica32.exe`, `CDViewer.exe`),
  `vmconnect.exe`, VMware and VirtualBox consoles, AnyDesk and TeamViewer are decoded with the
  *local* layout, while the remote side has its own. Add them to `DefaultExcludedProcesses`.
- **Layout-switch fallback.** Some apps ignore `WM_INPUTLANGCHANGEREQUEST`. When
  `Corrector.EnsureLayout` times out, try the user's own switch hotkey (Alt+Shift, Ctrl+Shift or
  Win+Space, read from `HKCU\Keyboard Layout\Toggle`) once before giving up.
- `SettingsStore.Load`: when settings.json fails to parse, keep a copy (`settings.json.bad`) before
  writing defaults. Today a typo silently wipes the user's settings.
- `FileLogger`: also catch `UnauthorizedAccessException`. A logging failure inside the worker's
  catch block can kill the worker.
- Measure the working set with el_GR loaded. If it's large, replace Hunspell lookups with a
  build-time hash set of normalised affix-expanded forms (the NgramBuilder already expands them).
  That is one probe instead of dozens of accent-variant checks per non-dictionary word.
  **Done (12g):** 191 MB → 87 MB working set, `LexiconDictionaryService`, `data/lexicon/`.
- Extend `docs/manual-test.md` with: Remote Desktop/Citrix (must be passive), an app run as
  administrator (correction skipped and logged), a UWP/Store app, Google Docs, Word with
  AutoCorrect on, Grammarly, Windows text suggestions, NVDA/Narrator running, and a
  high-DPI multi-monitor setup.

### 13. Hotkeys and everyday UX
Detection will never be perfect, so give the user an escape hatch.
- **Convert last word:** a hotkey converts the last word, even one the engine kept. It counts as
  a correction for undo, and the opposite choice is learned (hashed).
- **Convert selection:** a hotkey converts the selected text — copy, convert, paste, then restore
  the clipboard (all formats). This fixes whole sentences typed in the wrong layout.
- **Toggle on/off:** a hotkey for the same switch as the tray menu's Enabled.
- Make all hotkeys configurable, with defaults that don't clash with Windows or Office shortcuts.
  Register them with `RegisterHotKey` on the UI thread, not in the hook.
- **Feedback:** a brief tray-icon flash when a correction happens, so users notice it and know
  Backspace undoes it. Optional, on by default.
- **First run:** check that a Greek and an English layout are installed (`KeyboardLayouts.FindInstalled`)
  and explain how to add the missing one if not. Show a short note on what is captured (link to
  SECURITY.md), and offer Start with Windows (off by default).
- **Accidental Caps Lock** (optional, off by default): a word with an inverted case pattern
  (`hELLO`) while Caps Lock is on becomes `Hello`, and Caps Lock is turned off.
- **Per-app mode** — auto / suggest-only / off — replaces the plain exclusion list. Suggest-only
  depends on the popup from milestone 15, so add that mode with it.

**Done (13):** 13a core + 13b wiring (`HotkeyWindow`, `SelectionConverter`, always-convert list,
tray flash, `FirstRunForm`); per-app mode built with milestone 15 as `suggestOnlyProcesses`
next to the exclusion list (off) and the default (auto).

### 14. Office deployment
- **Code signing:** the CI step exists as a placeholder. Signing is what gets past SmartScreen and
  makes EDR and AppLocker/WDAC allow-listing practical (allow by publisher instead of by hash).
- **ARM64:** publish `win-arm64` alongside `win-x64` in `build.ps1`, `build.sh` and CI.
- **IT-managed settings:** read optional policy values from
  `HKLM\Software\Policies\GreekSwitch` (and HKCU). Cover: enabled, excluded apps, extra
  always-excluded apps, disable undo learning, disable loanword suggestions. Policy values
  override user settings and are shown as locked in Settings.
- **Optional MSI or MSIX package** for software-distribution tools. The single exe stays the main deliverable.
- **README "For IT" section:**
  - why antivirus may flag the app (global hook + SendInput), and how to verify the SHA-256;
  - AppLocker/WDAC rules;
  - the UIPI limit: no corrections in apps run as administrator;
  - the requirement for both layouts;
  - the policy keys.

**Done (14):** `win-arm64` + `win-x64` in `publish/<rid>/` (signing still a placeholder, for both);
policy keys via `ManagedPolicy` (HKLM over HKCU over settings.json, never written to it; locked
controls in Settings and the tray; `DisableUndoLearning` = `NeverConvertList.LearningEnabled`,
old hashes still apply; `AppSettings.SuggestLoanwords` forced off by policy); MSI/MSIX skipped;
README "For IT".

### 15. Loanword suggestions (`κοντέινερ` → `container`)
Build it as designed in **`docs/loanword-design.md`**:
- phonetic matching through a shared phoneme alphabet: Greek by rules, English from CMUdict, compared by weighted edit distance;
- candidates found with a skeleton index plus SymSpell deletions;
- three tiers: user pairs replace automatically; strong matches on non-dictionary words replace automatically (off by default); everything else is a suggestion popup accepted with Tab;
- privacy-preserving storage of learned pairs;
- a 5 ms median-lookup test.

Follow that doc's build order. **Done (15):** all six steps. Not built: the UI Automation caret
fallback for the popup (it uses `GetGUIThreadInfo`, then the mouse position) and a
BenchmarkDotNet project (the 5 ms median test covers it). Known weak spots: some ties rank the
wrong word (`ρίλιζ` → rallies, `κλάιντ` → cloud), capitalised unknown names can get a suggestion.

### 16. Later (design notes written: `docs/hold-and-release-design.md`, `docs/layouts-design.md`, `docs/greeklish-design.md`)
- **Hold-and-release input.** Let the hook swallow the delimiter (and keys typed while a decision
  is pending), then inject correction + delimiter + held keys. This would allow correcting words
  ended by Enter (chat apps) and remove skipped corrections for fast typists. It's high risk:
  it needs a hard timeout so input can never get stuck.
- **Layouts:**
  - map Greek Polytonic separately rather than as Greek;
  - support both US and UK English installed at once (verify each installed English HKL, not just the first);
  - let the user choose the pair when three or more layouts are installed.
- **Greeklish direction** (`kalimera` → `καλημέρα`) reusing the loanword engine. See the end of
  `docs/loanword-design.md` for the memory caveat.

## Conventions
- Small classes; Core has no Win32 and no static mutable state; dependencies through interfaces.
- `TreatWarningsAsErrors` in Core; central package versions in `Directory.Packages.props`.
- Keep FluentAssertions on 7.x (8.x is commercially licensed). Any new shipped dependency or
  data file needs a license check and an entry in `THIRD_PARTY_NOTICES.md`.
- Build-time data (trigram models, frequency table, phonetic index) is generated by a tool under
  `tools/`, committed, and embedded as a resource. Never parse large text data at app start.
- Never swallow exceptions on the worker: log the type and stack (no message), reset, continue.
- Windows-layer behaviour changes need a pass through `docs/manual-test.md` on a real machine.
