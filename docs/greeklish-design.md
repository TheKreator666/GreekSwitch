# Greeklish → Greek: design

**Status:** design only, not built (milestone 16). It reuses the loanword engine
(`docs/loanword-design.md`) in the other direction.

## The problem

Many Greeks type Greek words in Latin letters on purpose: `kalimera`, `ti kaneis`, `8a ertheis?`.
That is not a layout error (the keys `kalimera` in the Greek layout give `καλιμερα`, which the
layout stage may already fix *if* the user meant the Greek layout). Greeklish is typed in the
**English** layout intentionally, so converting it is a style choice, not a fix. The default
must be **suggest-only**, and the feature off by default.

## Matching

- Greeklish spelling is loose: `i`, `h`, `y`, `ei`, `oi` all mean /i/; `8` and `th` mean θ;
  `w` and `o` mean ω/ο; `x`, `h`, `ch` mean χ; `ps` and `y` can mean ψ. So compare **sounds**, as
  the loanword matcher does.
- Latin → phonemes: a small rule set for Greeklish conventions (digraphs first: `th`, `ch`, `ks`,
  `ps`, `ou`, `mp`, `nt`, `gk`; digits `8`→θ, `3`→ξ, `9`→θ in some styles; `w`→ο).
- Greek side: run the existing Greek → phoneme rules over the Greek lexicon and index the results
  with the same skeleton + one-deletion index as `en.idx`, written to `data/phonetic/el.idx` by
  the same builder.
- Rank by Greek Zipf (the frequency table already has Greek).

## Memory

A fully expanded el_GR list has more than 800k forms, about 10× the English index. Index only:

- lemmas, plus
- the most frequent inflected forms (top ~100k by Zipf).

Target ≤ 15 MB RAM and ≤ 3 MB compressed in the exe. Load it lazily, only when the feature is
turned on.

## Deciding

- Only words typed in the **English** layout that the layout stage kept, ≥ 3 letters, not in
  the English lexicon (so `to`, `me`, `are` are never Greeklish candidates), and the sentence
  context is mostly Greeklish (two or more previous kept words also matched Greek).
- Suggest via the loanword popup (Tab accepts). Accepting converts **and** switches the layout
  to Greek, which often ends the Greeklish run naturally.
- User pairs and promotion as in the loanword design (hashed keys).

## Tests

| Typed (EN layout) | Expected |
|---|---|
| `kalimera` | Suggest `καλημέρα` |
| `ti kaneis` (second word) | Suggest `κάνεις` once context is Greeklish |
| `8elw` | Suggest `θέλω` |
| `meeting`, `are`, `email` | Nothing |

Plus an evaluation row: run it over the English frequency list (nuisance rate) and over a
Greeklish sample (recall).

## Build order

After milestone 15 is complete: Latin → phoneme rules, `el.idx`, matcher reuse, context rule,
suggest-only wiring, setting (off by default).
