# Layouts: Polytonic, US + UK, choosing the pair: design

**Status:** design only, not built (milestone 16).

## Today

- `KeyboardLayouts.ToLang` maps an HKL to Greek or English by its primary-language bits
  (`0x08`, `0x09`). Every Greek layout is "Greek" and every English layout is "English".
- `KeyboardLayouts.FindInstalled(lang)` returns the **first** installed HKL of that language.
- `LayoutVerifier.VerifyInstalled` checks the static map (`StaticLayouts.Greek` / `.English`)
  against that one HKL with `ToUnicodeEx` at startup and follows the OS where they differ.
- `LayoutSwitcher.SwitchTo(lang)` posts `WM_INPUTLANGCHANGEREQUEST` with that same HKL.

So with US + UK installed, only the first is verified and switched to; with Greek Polytonic
installed, it's treated as the ordinary Greek layout, whose keys differ.

## 1. Identify layouts by KLID, not by language

An HKL's low word is the language (`0x0408`); its high word is either the same language (the
default layout) or a *device handle* (`0xF0nn`) that indirectly names the layout. The stable
identity is the **KLID** (`00000408` Greek, `00020408` Greek Polytonic, `00000409` US,
`00000809` UK, `00010409` US-Dvorak, ...).

- Mapping HKL → KLID without activating anything: for a high word `0xF0nn`, find the subkey of
  `HKLM\SYSTEM\CurrentControlSet\Control\Keyboard Layouts` whose `Layout Id` value is `nn`
  (hex); otherwise the KLID is `0000` + the high word. This is read-only and runs once at startup.
- Introduce `InstalledLayout(nint Hkl, string Klid, Lang? Lang, LayoutKind Kind)` where `Kind` is
  `Greek`, `GreekPolytonic`, `English`, or `Other`. `ToLang` keeps working for callers that only
  need the language.

## 2. Greek Polytonic as its own map

The Polytonic layout moves the tonos to different dead keys and adds breathings, so the
monotonic map decodes its keys wrongly.

- Add `StaticLayouts.GreekPolytonic` (verified against `ToUnicodeEx` like the others).
- A Polytonic word is decoded with that map and **never auto-replaced into English** at first:
  polytonic text is mostly classical or ecclesiastical, where the English lexicon isn't the
  right comparison. Start in "suggest-only" (once the popup exists) or passive.
- English typed while Polytonic is active is still corrected *into* the user's monotonic Greek
  layout if one is installed, otherwise into Polytonic with `KeyPlanner` using the Polytonic map.

## 3. US and UK English at once

- Verify **every** installed English HKL at startup (one `LayoutMap` per HKL; the differences are
  `"`/`@`, `#`/`\`, `£`).
- Decode each word with the map of the HKL that was active when its keys were typed (the hook
  already captures the HKL per key since milestone 12).
- When switching *to* English, use the English HKL the user used most recently in this session
  (fallback: the first in the language bar order). Remember it per process, so a user who types
  UK in Word and US in a terminal keeps both.
- `KeyPlanner` plans keys with the map of the target HKL.

## 4. Choosing the pair with three or more layouts

With e.g. Greek, US and German installed, GreekSwitch must not switch into German.

- Settings: "Layouts to switch between" with two drop-downs (Greek-side, English-side), filled
  from `InstalledLayout`s, default = the first Greek and the first English.
- Layouts not in the pair are passive: keys typed in them are not captured as words (the worker
  already stays passive for languages that are neither Greek nor English; extend that to
  non-selected layouts of the same language).
- Policy (milestone 14) can set the pair by KLID: `GreekLayout` / `EnglishLayout` (REG_SZ).

## Tests

- Core: `StaticLayouts.GreekPolytonic` round-trip tests like the existing layout tests; decoding
  with a per-word map.
- Windows: KLID resolution from a fake registry; most-recent-English selection.
- Manual: US + UK installed (type `@` and `"` in both, corrections switch to the right one);
  Polytonic installed and active (no English replacements); Greek + US + German (never switches
  to German).

## Build order

1. `InstalledLayout` + KLID resolution (no behaviour change).
2. Per-HKL verified maps and decoding by the captured HKL.
3. Most-recent English HKL for switching.
4. Pair selection in Settings and policy.
5. Polytonic map, passive first.
