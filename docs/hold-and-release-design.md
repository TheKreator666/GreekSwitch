# Hold-and-release input: design

**Status:** design only, not built (milestone 16). It is the riskiest change on the list: it
makes the keyboard hook swallow keys. Build it only behind a setting that is off by default.

## The problem

Today the hook never swallows a key. By the time the worker decides that a word was typed in the
wrong layout, the delimiter that ended it (Space, punctuation, Enter) has already reached the
app. That causes three limitations:

1. **Enter and Tab.** A word ended by Enter is not corrected by default: in a chat app Enter has
   already sent the message, and Tab has moved focus. (`correctOnEnterAndTab` exists but is off.)
2. **Fast typists.** If the next key arrives before the correction is sent, the corrector
   skips it (`TypeAheadGuard`), because its Backspaces would land after the new keys.
3. **Flicker.** The wrong word is visible for a moment, then deleted and retyped.

## The idea

At a delimiter, the hook **holds** the delimiter instead of passing it on, and holds any keys
typed while the decision is pending. When the decision arrives, it **releases**:

- Keep: re-inject the delimiter and the held keys unchanged.
- Replace: inject Backspaces for the word (not the delimiter, which never arrived), the
  corrected word, the delimiter, then the held keys. One `SendInput` batch.

The app then sees exactly the keys it would have seen if the user had typed the right word, in
order. Enter sends the *corrected* message.

## Why it's risky

- **Stuck input.** If the worker hangs, crashes or is slow, the user's keys disappear. That is
  far worse than a missed correction. Windows also removes a low-level hook that takes longer
  than `LowLevelHooksTimeout` (about 1 s by default; less on some machines) to return, and keys
  swallowed by a removed hook are simply gone.
- **Ordering.** Re-injected keys are `LLKHF_INJECTED`. Apps that ignore injected input (some
  games, some remote-desktop clients, anything elevated through UIPI) would lose the held keys.
- **Modifiers and shortcuts.** A held Space followed by Ctrl+S must release the Space before
  Ctrl+S runs, or the save sees the wrong text.
- **Key repeat and key-up.** Swallowing a key-down but not its key-up (or the reverse) confuses
  apps. Both must be held and released together.
- **Security software.** Key swallowing plus re-injection looks even more like a keylogger.

## Design

### In the hook (still: no locks, no I/O, no waiting)

- A `volatile` state word shared with the worker: `Idle` or `Holding(deadlineTick)`.
- On a delimiter key-down while capture is on and the setting is enabled: set `Holding`, enqueue
  the key with a `Held` flag, and return 1 (swallow). Its key-up is swallowed too.
- While `Holding`: every key-down and key-up is enqueued with `Held` and swallowed, **except**
  - any key with Ctrl, Alt or Win held, Esc, and the Windows key itself: these first trigger an
    immediate release (the hook sets a `ReleaseNow` flag and posts to the hook thread; the key
    itself passes through **after** the release batch, see below);
  - when `GetTickCount64() > deadline`: the hook stops holding (passes the key through) and the
    worker's eventual release is discarded.
- The hook never calls `SendInput` itself.

### In the worker

- A held delimiter triggers the decision as today. The worker builds the release batch
  (correction + delimiter + held keys, with their original scan codes and up/down order) and
  sends it with `InputBuilder.Signature`, then sets `Idle`.
- Hard timeout: the deadline is **150 ms** after the delimiter (configurable 50–300 ms). The
  current decision takes well under 5 ms; most of the budget is for the layout switch. If the
  layout switch hasn't been confirmed by then, release the keys as typed and skip the correction.

### Watchdog (non-negotiable)

- A timer on the hook thread (it has a message loop) checks every 50 ms: if `Holding` has lasted
  longer than the deadline + 100 ms, it releases the held keys itself (as typed) and logs
  `Hold-and-release: worker missed the deadline; keys released unchanged`.
- If that happens three times in a session, hold-and-release turns itself off until restart and
  the tray shows a warning.
- The hook thread owns the held-key buffer; the worker only asks it to release with a decision.
  So a dead worker can never strand keys.

### Undo

Unchanged: Backspace right after a correction reverts it. The undo record covers the word only;
the delimiter and held keys are already typed.

## What stays the same

- Off by default. The current path (correct after the fact, skip on type-ahead) remains the
  fallback whenever capture is off, the app is excluded, it's a command window, or the target is
  elevated (`SendInput` partially fails → release unchanged, not half a batch).
- Never in terminals and IDEs (`CommandWindows`): holding Enter there is too dangerous.
- `NoTextInLogsTests` covers the new log calls (counts and timings only).

## Tests

- Core: a `HoldBuffer` model (held events → release batch) with property tests for ordering,
  key-up pairing and modifiers.
- Windows: a fake clock + fake sender for the watchdog: a worker that never answers must release
  every held key within deadline + 100 ms, in order.
- Manual: chat app with Enter (message sent corrected), a 120 wpm typing burst (no skipped
  corrections, no lost or reordered keys), killing the worker mid-hold (keys still arrive),
  Ctrl+S right after a word, a game (excluded), and an elevated app (keys released unchanged).

## Build order

1. `HoldBuffer` in Core + tests.
2. Hook state and swallowing, behind a setting, with the hook-thread watchdog; release always "as typed".
3. Worker decisions in the release batch.
4. Enter/Tab in chat apps; then remove the type-ahead skip when holding is on.
