# Missing accents and lost letters: findings and status (2026-10-27)

Notes from one long debugging session, written to be picked up later. Short version: accents
for Greek typed without the tonos key work in the simulator. In real apps, retyping a word while
the user keeps typing loses letters. The code now adds accents only **when the user pauses**,
and holds keys typed during a correction. Lost letters while typing are gone in Notepad. Some
cases are still open (see *Open issues*).

## What was asked

1. Greek words don't always get their accent.
2. When the app adds an accent, the keyboard seems to stall: a letter of the next word goes missing.
3. Wrong-layout conversion must keep working in both directions: English typed in the Greek
   layout (`ψονταινερ` → `container`, `ροθτε σθρωευ` → `route survey`) and Greek typed in the
   English layout (`doyleyei` → `δουλεύει`). It must work everywhere people type: browsers,
   Office, Notepad.

## Timeline

| Step | Where | What |
|---|---|---|
| 1 | commit `de37eab` | Words converted from the wrong layout get their accents (`kanei` → `κάνει`). Greek lookups ignore accents, so `κανει` used to pass as a word and was typed out bare. New `GreekAccentTable` + `data/frequency/el.accents` (37,857 spellings, from wordfreq). |
| 2 | this commit | Accents also for Greek typed in the Greek layout (setting *Add missing Greek accents*, on by default). |
| 3 | this commit | Words ending in the Greek question mark (`κανω;`, typed with Q, so part of the word) now get accents. |
| 4 | this commit | `KeyHold`: keys pressed while a correction is being sent are held and replayed after it. |
| 5 | this commit | Accents are no longer typed after each word, but at a pause (~0.5 s), for all words since the last pause. |
| 6 | this commit | `tools/EndToEndDriver`: `--append`, `--probe` and `--matrix` modes used for the experiments below. |

## Issue A: the old exe was running (solved)

The user was running `publish\GreekSwitch.exe` from 23 October. `build.ps1` writes to
`publish\win-x64\` and `publish\win-arm64\`, so that file was never replaced. It has been
deleted, as has `publish\win-arm64` (the user runs only x64; `build.ps1` recreates it on every
build). Run **`publish\win-x64\GreekSwitch.exe`** only.

## Issue B: only wrong-layout words got accents (solved in the simulator)

After the first correction GreekSwitch switches to the Greek layout. The rest of the sentence
is then typed in Greek, without accents, and was kept as typed. Simulator sweep (Greek typed
without the accent key, 400 Tatoeba sentences):

| | Accented correctly |
|---|---|
| Only wrong-layout words (step 1) | 20.4% |
| All Greek words (step 2) | 91.6% |
| Single words, top 5,000 by frequency | 98.9% |

False positives with the option on: Greek sentences changed 6 of 2,727 words instead of 2.
The 4 extra were words the test data itself has without accents (`απο` → `από`). English is
unchanged at 17 of 3,064. Decisions (which words get converted) are unchanged, and all
evaluation gates pass.

## Issue C: lost and garbled letters in real apps (partly solved)

Typing the same three sentences into Notepad with `tools/EndToEndDriver` (Greek layout, no
accent key, 150 ms and 60 ms between keys). With accents typed after every word:

```
δουλευει τωρα το …          → δουλεύει ωρα το …                  (τ lost)
ελεγχουμε παλι αμα … καλα   → ελεπάμα το σύστημα δουλκαλά
… να φορτωθει στο λιμανι    → … να φοστο λιμάνι
```

The app's log said "Accents added" for each word, and every batch was delivered whole
(`SendInput` 38/38 events). Experiments to find out where it breaks:

| Probe | Result | What it shows |
|---|---|---|
| GreekSwitch off; the driver sends the same correction batch itself, next key 1000/150/30 ms later | 10/10 correct, dead key or Unicode | Notepad handles the batch and the keys after it fine |
| Same, per word, whole sentence, with the UI Automation text read before each batch | 4/4 correct | The read alone doesn't break it |
| Same, with an **empty** low-level keyboard hook installed | dead key 2/2 correct, Unicode 0/2 (blanks: the known Notepad Unicode bug) | Slow delivery alone doesn't break it |
| Next key pressed **10 ms into** the send, empty hook installed | 0/3 (`δουλεκύει αλα`, `δουλεύκει αλα`, `δουλεύεικ αλα`) | **With any LL hook installed, SendInput is not atomic**: a key pressed during the send lands inside it |
| Same, no hook | 3/3 | Without a hook it is atomic |
| Same, hook thread at high priority | 2/3 | Priority isn't a fix |
| The app with a hook trace (B = our Backspace, o = our key, F = anyone else) | Always `BBBBBBBBBooooooooooFF`: complete and in order | In those app runs the damage happened *after* the keys left our hook |
| The app with the UI Automation text check **off** | 3/6 lines perfect, the rest 1 character or space lost (`τώρατο`, `ένφορτηγό`) | The text check (reading Notepad right after each word) was the biggest cause |
| GreekSwitch off, type `προγραμμα ενα φορτωθει δουλευει ελεγχουμε παλι` | `πρόγραμμα ένα φορτωθεί δουλευει ελεγχουμε παλι` | **Something besides GreekSwitch accents some words after Space** (see open issue 1) |

Conclusions:

1. A correction typed right after Space competes with whatever else is happening in the app at
   that moment: the user's next key (not atomic with our hook installed), our own UI Automation
   read, and the app's own processing of the word (spell check / autocorrect). The more often
   corrections happen, the more often this goes wrong. Per-word accents made it happen after
   almost every word.
2. Fix: **don't retype while the user types.** Accents are added at a pause, all at once.
   Layout corrections still happen at once, as before. They're rarer, and the delay would hurt.
3. `KeyHold` fixes the race in the probe above (a key pressed during the send). The hook queues
   any key event that isn't ours while a send is in progress, swallows it, and the corrector
   replays the queue in order right after its batch. Lock-free in the hook, hard 200 ms cap.
   This is a second exception to the "hook never swallows keys" rule (CLAUDE.md, SECURITY.md).

Live result with accents at a pause (Notepad, same sentences):

| Line | 150 ms | 60 ms |
|---|---|---|
| δουλεύει τώρα το πρόγραμμα … | no letter lost, but no accents: text check mismatch, batch skipped | same |
| ελέγχουμε πάλι άμα … καλά | **most of the line deleted** (`ελεγχουμε` left), see open issue 2 | correct |
| έχω ένα φορτηγό … λιμάνι | no letter lost, no accents: mismatch, batch skipped | same |

In the skipped lines, the screen had `πρόγραμμα`, `ένα`, `φορτωθεί` accented by the app
(not by GreekSwitch). So the text check correctly refused to retype, and the other words stayed bare.

## Open issues

1. **Who accents words after Space in Notepad?** With GreekSwitch stopped, `προγραμμα`,
   `ενα`, `φορτωθει` came out accented (`δουλευει`, `ελεγχουμε`, `παλι` did not). The user reports
   that Notepad's spell check only flags words. Candidates: Notepad's *Autocorrect* toggle
   (separate from spell check, in Notepad settings), or Windows *Settings → Time & language →
   Typing → Autocorrect misspelled words* (the registry under `HKCU\Software\Microsoft\input\Settings`
   showed no values set). Check both on the machine. Either way, other apps (Word, Outlook,
   browsers with spell check) can rewrite words too, so GreekSwitch has to cope.
2. **One line was mostly deleted** at 150 ms (accents at a pause, 44-character batch, text
   check passed). Not explained. Most likely an app-side change landed between the check and
   the batch. Reproduce before guessing; if it recurs, compare the text right after the batch.
3. **App changed some accents → whole batch skipped.** Idea: if the text before the caret
   differs from what was typed *only in accents*, accept the screen's version (keep the
   app's accents, add ours to the rest) instead of skipping. Needs the verifier to return the text
   (in memory only).
4. **An English word ends the stretch.** Greek keys can't retype Latin letters. In
   `θέλω να φτιαξω ενα application που θα κάνει πράξεις`, the words before `application` stay
   unaccented. Idea: when the English word's own layout correction happens, first add the
   pending accents in the Greek layout, then switch and type the English word (two-layout
   correction).
5. **Not yet tested outside Notepad.** `--matrix` is written and builds, but hasn't been run:
   Notepad, Word, Edge (textarea), Chrome (contenteditable), Firefox. The cases are `container`
   and `route survey` typed in the Greek layout, `doyleyei` typed in the English layout, a Greek
   sentence without accents, and the mixed sentence above. (`ρουτε` is a phonetic spelling,
   not a layout mistake. The Greek-layout keys for "route" give `ροθτε`.)
6. Rarer word forms outside the 38k list stay unaccented (`τηλεφωνησω`). Where two spellings
   differ only in the accent, the more frequent always wins (`πότε`/`ποτέ`).
7. Accents at a pause aren't undone by Backspace (a Backspace after a pause is usually meant for
   the text); Ctrl+Z in the app undoes them. Suggest-only apps get no accents.

## How to reproduce

All runs type real keys: close Notepad (and Word for the matrix), don't touch the PC while
they run. Every key-down is only sent while the target window is in front (the driver stops
otherwise). Back up `%APPDATA%\GreekSwitch\settings.json` first.

```powershell
dotnet build src/GreekSwitch.App -c Debug; dotnet build tools/EndToEndDriver -c Debug

# with the app: Debug build, test mode (accepts the driver's injected keys)
$env:GREEKSWITCH_TEST_ACCEPT_INJECTED = '1'; Start-Process src\GreekSwitch.App\bin\Debug\net8.0-windows\GreekSwitch.exe
dotnet run --project tools/EndToEndDriver -c Debug --no-build -- --append <some file.txt>   # 3 sentences, slow and fast
dotnet run --project tools/EndToEndDriver -c Debug --no-build -- --matrix <work dir>        # Notepad, Word, browsers

# without the app (stop GreekSwitch first): Notepad behaviour on its own
dotnet run --project tools/EndToEndDriver -c Debug --no-build -- --probe
```

If Notepad opens behind the terminal, click into it within 20 s (Windows doesn't let a
background process take the foreground). `"debugLogText": true` in settings.json (Debug builds
only) logs the words of each correction, for diagnosis.

## Leftovers on the machine

- `Desktop\New Text Document.txt` has 10 lines from the test runs at the end (after the user's
  own lines). They show the damage described above and can be deleted.
- `%APPDATA%\GreekSwitch\settings.json.e2e-backup`: settings backup from before the runs.
