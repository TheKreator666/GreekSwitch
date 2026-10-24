# Loanword matching: design

**Status:** built (milestone 15 in `CLAUDE.md`). Core: see *As built (Core)* at the end for the
API and the measured numbers. Wiring: `TypingSession` checks kept Greek words (`CheckLoanword`),
`ISuggestionSink` → `SuggestionSink` (App) opens the hook's `SuggestionGate` and shows
`SuggestionPopup` at `CaretLocator.Find()`; Tab arrives as `InputEventKind.AcceptSuggestion`.
Pairs are saved in settings.json (`loanwordPairs`, `loanwordLearned`). The popup is shown via
`GetGUIThreadInfo` or the mouse position; the UI Automation caret fallback is not built.

## The problem

Greeks often write English words in Greek letters. `κοντέινερ` means `container`.

This is a different problem from layout errors:

- `ψονταινερ` is what the keys C-O-N-T-A-I-N-E-R give in the Greek layout. It is gibberish in
  both languages, and the existing layout stage already fixes it.
- `κοντέινερ` is **not an error**. It is how Greeks correctly spell the loanword, and it may be
  in el_GR. Here we're guessing intent rather than fixing a mistake. Matching is the easy part;
  **deciding when to act** is the hard part.

So the default is to **suggest, not replace**.

## Where it fits

```
TypingSession.OnDelimiter → DecisionEngine.Decide
   Replace → existing path (layout correction)
   Keep, and the current reading is Greek, ≥ 4 letters, delimiter is Space or punctuation
        ▼
   LoanwordDetector.Check(greekReading) → None | Replace(text, tier) | Suggest(text, score)
        Replace → ICorrectionOutput.Replace (same guards, same undo)
        Suggest → ISuggestionSink.Show(...)   (popup, implemented in the App/Windows layer)
```

The order matters. `ψονταινερ` is fixed by the layout stage and never reaches this stage;
`κοντέινερ` passes the layout stage untouched and is considered here.

- `LoanwordDetector` lives in Core: pure logic, no Win32, behind an interface, fully unit-tested.
- The suggestion popup and the Tab key handling live in the Windows/App layers.
- The detector never runs in password fields or excluded apps, because no keys reach it there.

## Matching: compare sounds, not letters

Greek spelling is almost perfectly phonetic, and English pronunciation is available as data.
Convert both sides into one shared, simplified phoneme alphabet (an enum of about 25 symbols:
5 vowels plus consonants), then compare.

### Greek → phonemes (rules, run live)

About 40 rules. Apply digraphs first, after lowercasing and stripping accents. Keep the tonos
position separately, because it's used for the stress bonus.

| Greek | Phoneme | Greek | Phoneme |
|---|---|---|---|
| μπ | b (also mb) | ου | u |
| ντ | d (also nd) | αι, ε | e |
| γκ, γγ | g | ι, η, υ, ει, οι | i |
| τσ | ts | ο, ω | o |
| τζ | dz | β | v |
| θ | th | ξ | ks |
| δ | dh | ψ | ps |
| χ | h | αυ / ευ | av, ef (before voiceless consonant: af, ef; else av, ev) |
| γ | gh (before ε/ι sounds: y) | σ/ς, ζ, λ, μ, ν, ρ, π, τ, κ, φ | s, z, l, m, n, r, p, t, k, f |

Example: `κοντέινερ` → `k o n t e i n e r`.

### English → phonemes (data, prepared at build time)

Source: the **CMU Pronouncing Dictionary**. It's about 134k lines like `CONTAINER  K AH0 N T EY1 N ER0`,
about 3.5 MB of text, under a BSD-style license (check it, and add it to `THIRD_PARTY_NOTICES.md`).
Using it beats guessing pronunciation from English spelling, which isn't phonetic.

Map ARPAbet onto the shared alphabet, collapsing English vowels onto Greek's five:

| ARPAbet | Shared | ARPAbet | Shared |
|---|---|---|---|
| AA, AE, AH | a | B, D, G, K, P, T | b, d, g, k, p, t |
| AO, OW | o | CH | ts |
| EH | e | JH | dz |
| ER | er | DH, TH | dh, th |
| EY | ei | SH, ZH | s, z |
| IH, IY, Y | i | HH | h |
| UH, UW | u | W | u (also v) |
| AY, AW, OY | ai, au, oi | NG | ng |

Example: `container` = `K AH0 N T EY1 N ER0` → `k a n t ei n er`.

### Comparison: weighted edit distance

Compute it over the two phoneme sequences, then normalise by length so the score is
`1 - distance / max(len)`, between 0 and 1.

- Vowel ↔ vowel substitution: cheap (≈ 0.3). `o`/`a` for AH is very common:
  κ**ο**ντέινερ vs c**o**ntainer.
- Known confusions: cheap (≈ 0.2–0.4). These are `ei`↔`e i`, `er`↔`e r`, `d`↔`nt`, `b`↔`mp`
  (κομπιούτερ, where people say "b" or "mp"), `g`↔`gk`, `s`↔`sh`, and `u`/`v`↔`w`.
- Inserting or deleting a vowel: ≈ 0.5. A consonant substitution, insertion or deletion: 1.0.
- **Stress bonus:** CMU marks the stressed vowel (EY**1**) and the Greek tonos marks it too. When
  the stress positions match, add a small bonus. It separates near-ties.

All costs and thresholds go in a `LoanwordOptions` record and are tuned against the tests below.

### Candidate retrieval: consonant skeleton + SymSpell deletions

Comparing against all 134k words per keystroke-word would be wasteful (≈ 10–50 ms).

- At build time, compute each English word's **consonant skeleton** (`container` → `k n t n r`)
  and index word IDs by skeleton.
- Also index every skeleton with **one consonant deleted** (`n t n r`, `k t n r`, …). This is the
  SymSpell trick: a typed word then only needs its own deletions looked up, never all possible substitutions.
- Per word that means about 6–10 hash lookups, then scoring typically 20–300 candidates.

### Ranking ties

When several English words match (`καρ` could be car or cur), rank by **English word frequency**
(the same Zipf table as milestone 10c), and give a bonus to targets the user has accepted before.

## Deciding: three tiers

Because the input is valid Greek, auto-replacing would annoy anyone writing normal Greek prose.

1. **User list (auto-replace).** These are exact pairs the user explicitly confirmed
   (`κοντέινερ → container`, `ντεπλόι → deploy`). They're deterministic, so there are no false positives.
2. **Strong match on a non-dictionary word (auto-replace, off by default).** Ad-hoc
   transliterations like `ντεπλόι`, `κόμιτ` or `ρίλιζ` usually *aren't* in el_GR. Auto-replace when
   all three hold: the word fails el_GR, the match score ≥ `StrongThreshold`, and the English word
   is on a **domain vocabulary list** (tech terms by default, editable).
3. **Everything else above `SuggestThreshold` (suggest).** Show a small popup near the caret:
   `container  [Tab]`. Tab accepts; any other key dismisses it.

**Promotion:** after the user accepts the same pair N times (default 3), it moves to tier 1. The
app gets better from use without anyone maintaining a list.

## The suggestion popup

- **Window:** created with `WS_EX_NOACTIVATE | WS_EX_TOPMOST | WS_EX_TOOLWINDOW`, so it never
  steals focus or shows in the taskbar. It's per-monitor DPI aware. It disappears after a few
  seconds, on focus change, or on any key.
- **Caret position:** `GetGUIThreadInfo().rcCaret` works for native Win32 apps (Notepad, Office).
  Chrome and Electron apps (Teams, Slack, VS Code) often don't report it, so try UI Automation
  `TextPattern` next (on the UIA thread from milestone 12, with a timeout). If both fail, fall back
  to a position near the mouse, or a toast.
- **Tab to accept:** this needs a **narrow exception to the hook invariant**. While a suggestion is
  visible, the hook reads one `volatile` flag. If the key is Tab with no modifiers, the hook
  swallows it (returns 1 without calling the next hook) and enqueues an `AcceptSuggestion` event.
  It doesn't decide anything else and does no I/O. Every other key clears the flag and passes
  through as usual.
- **Applying an accepted suggestion:** the delimiter is already on screen, so delete the word plus
  the delimiter and type the target plus the delimiter. Use `Corrector.Replace` with all its guards.
  If any key was typed after the word, the popup is already gone (any key dismisses it), so the
  type-ahead problem can't arise. Undo by Backspace works as for layout corrections.

## Privacy

This feature must follow the same rules as the rest of the app (see `SECURITY.md`):

- Store user pairs and acceptance counts keyed by `HMAC(normalised Greek word)`, using the existing
  DPAPI-protected key. Store only the **English target** as text. Targets come from the English
  dictionary, so they reveal nothing the user typed.
- Words the user explicitly enters in Settings as pairs may be stored as text, like today's
  manual never-convert list.
- No text in logs. Log tier, score bucket and lengths only. Extend `NoTextInLogsTests` to cover the detector.

## Data build and performance

| Step | When | Cost (estimates; confirm with BenchmarkDotNet) |
|---|---|---|
| `tools/PhoneticIndexBuilder` reads CMUdict + frequency list and writes `data/phonetic/en.idx` (word table, phoneme byte arrays, skeleton → IDs index, deletion index) | Build time, once; commit the output | — |
| Embed `en.idx` as a resource; deserialize into arrays + a skeleton dictionary | App start, background thread after the tray icon appears | ~20–80 ms; RAM ~10–25 MB; exe +2–5 MB compressed |
| Greek rules → skeleton + deletions → score candidates → decide | Per word, at the delimiter, on the worker thread | well under 1 ms |

- Until the index is loaded, the detector returns None.
- A fast typist produces about 2 words per second, so the per-word budget is ~500 ms. The
  stage only runs at word boundaries, on Greek words the layout stage kept, so it adds nothing to the hook.
- Pitfalls to avoid:
  - Parsing CMUdict as text at startup costs ~200–500 ms plus allocations.
  - Scoring against every word instead of a skeleton bucket is ~100× slower.
- Add a BenchmarkDotNet project for real numbers. Also add a plain unit test that fails if the
  **median** lookup over a fixed word list exceeds **5 ms**; that's generous enough for noisy CI machines.

## Later: the reverse direction (Greeklish)

Run the Greek → phoneme rules over the el_GR word list and index it the same way. Then
`kalimera` can be matched to `καλημέρα` with the same engine. A fully expanded el_GR list has
more than 800k forms, so that index would be roughly 10× larger in RAM. Index lemmas plus the
most frequent inflected forms only.

## Tests

| Input (typed in the Greek layout, correctly) | Expected |
|---|---|
| `κοντέινερ` | Suggest `container` |
| `κομπιούτερ`, `σέρβερ` | Suggest `computer`, `server` |
| `ντεπλόι` (tier 2 on, tech list) | Replace with `deploy` |
| `ντεπλόι` (tier 2 off) | Suggest `deploy` |
| `καλημέρα`, `σπίτι`, `πράξεις` | Keep, no suggestion (no close English match) |
| `κοντέινερ` after 3 acceptances | Replace with `container` (tier 1) |
| `ψονταινερ` | Replace with `container` by the **layout** stage; the detector is never called |
| Any word, then a key other than Tab | Popup dismissed, nothing changed |
| Tab with no popup visible | Passes through untouched |

Also add evaluation-harness rows (milestone 9): run the detector over the frequency-weighted
Greek list and report how often it *suggests* on ordinary Greek words. That is the nuisance rate; keep it low.

## Build order

1. Shared phoneme alphabet + Greek rules + their unit tests.
2. `PhoneticIndexBuilder` + CMUdict mapping + generated `en.idx` + loader.
3. Matcher (skeleton retrieval, weighted distance, ranking), passing the test table, plus the benchmark.
4. Tier 1 (user pairs, hashed storage, Settings UI for explicit pairs).
5. Popup, caret positioning and the Tab exception in the hook. This is the fiddliest Win32 part;
   everything before it can be tested without a UI.
6. Tier 2 (off by default) and promotion.

## As built (Core)

Code: `src/GreekSwitch.Core/Loanwords/`. Data: `data/phonetic/en.idx` (`tools/PhoneticIndexBuilder`,
see `data/phonetic/README.md`).

**Deviations from the design above**, found while tuning:

- ARPAbet `Y` is the semivowel y (cheap to swap with i), not i, and stress positions are counted
  from the *last* vowel, so `κομπιούτερ`/`computer` line up. `NG` is `n` before K/G (`λινκ`).
- `μπ`/`ντ` are mp/nt only between vowels; at a word edge or next to a consonant they are b/d
  (`ντεπλόι`, `φίντμπακ`, `άπντεϊτ`, `ντάουνλοουντ`). `γου` + vowel and `γι` + vowel are w and y.
- γ vs g is a full consonant edit (transliterations write g as γκ), so `γάτα` isn't `gotta`.
- The skeleton ignores voicing (b/p, d/t, g/k/γ, v/f, δ/θ, z/s) so `κομπιούτερ` finds `computer`.
- Matching alone suggests far too often (`σπίτι` sounds like `spotty`, `πράξεις` like `proxies`).
  The detector therefore skips: Greek words with Zipf ≥ 4.2 (established Greek, including adopted
  loanwords like `τεστ`); dictionary words that end like Greek words (vowel, ς, ν); names
  (capitalised-only dictionary entries); targets with Zipf > 5.5 (function words: `μένει`→many);
  and, when the Greek word has a frequency, targets less than 0.5 Zipf more frequent in English.
- Tier 1 is checked only once the index is loaded (the detector returns None until then).
- A rejection (undo, or an explicit dismissal) resets the accept count, demoting a promoted pair;
  3 rejections of the same pair hide that suggestion.

**API** (all thread-safe):

```csharp
var pairs = new LoanwordPairStore(settings.Pairs, settings.LearnedPairs, hasher);  // hasher: the DPAPI-keyed HmacWordHasher
var detector = new LoanwordDetector(pairs, dictionaries, frequencies, new DomainVocabulary(), new LoanwordOptions(), logger);
_ = Task.Run(() => detector.UseIndex(PhoneticIndex.FromEmbeddedResources()));     // ~15 ms, 2.4 MB
LoanwordResult r = detector.Check(greekWord);   // LoanwordNone | LoanwordReplace(Target, Tier) | LoanwordSuggest(Target, Score)
detector.Accept(greekWord, target);             // Tab on a suggestion; 3 accepts → tier 1
detector.Reject(greekWord, target);             // undo of a loanword replacement, or explicit rejection
pairs.Changed += …;  pairs.Snapshot();  pairs.LearnedSnapshot();                   // persistence, like NeverConvertList
```

Targets come back cased like the typed word. Learned pairs store `HMAC("loanword\u001f" +
normalised Greek)`, the lowercase English target and two counts; targets that aren't short
Latin words are refused, so nothing the user typed can be stored as text by mistake.

**Measured** (evaluation harness, default options): suggestions on 0.15% of words of the
frequency-weighted Greek list and 0.22% of Greek sentence words; tier 2 on replaces 0.00%
(weighted); 92.9% of `tests/GreekSwitch.Evaluation/Data/loanwords.tsv` get the right suggestion.
Median lookup 0.85 ms (unit test limit 5 ms). No BenchmarkDotNet project was added.
