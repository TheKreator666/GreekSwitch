"""Export word frequency tables from wordfreq for GreekSwitch.

Writes data/frequency/el.tsv and data/frequency/en.tsv: the most frequent words of each
language as "word<TAB>zipf" (Zipf = log10 of frequency per billion words, 2 decimals),
most frequent first.

    pip install wordfreq==3.1.1
    python tools/FrequencyExport/export_wordfreq.py

wordfreq (https://github.com/rspeer/wordfreq) code is Apache-2.0; its data is CC BY-SA 4.0,
so the exported tables are CC BY-SA 4.0 too (see THIRD_PARTY_NOTICES.md).
"""

import pathlib
import re
import sys

import wordfreq

TOP_N = 50_000
OUT = pathlib.Path(__file__).resolve().parents[2] / "data" / "frequency"

GREEK_WORD = re.compile(r"^[Ͱ-Ͽἀ-῿]+$")
ENGLISH_WORD = re.compile(r"^[a-z]+(?:'[a-z]+)?$")


def greek_final_sigma(word: str) -> str:
    # wordfreq folds every sigma to σ ("τησ"); written Greek uses ς at the end of a word.
    return word[:-1] + "ς" if word.endswith("σ") else word


def export(lang: str, pattern: re.Pattern, fix=lambda w: w) -> None:
    rows, seen = [], set()
    for word in wordfreq.top_n_list(lang, TOP_N):
        if not pattern.match(word):
            continue
        word = fix(word)
        if word in seen:
            continue
        seen.add(word)
        rows.append(f"{word}\t{wordfreq.zipf_frequency(word, lang):.2f}")

    path = OUT / f"{lang}.tsv"
    header = (
        f"# GreekSwitch frequency table v1: {lang}, top {len(rows)} words by Zipf\n"
        f"# source: wordfreq {wordfreq.__version__ if hasattr(wordfreq, '__version__') else '3.1.1'}"
        " (https://github.com/rspeer/wordfreq), data license CC BY-SA 4.0\n"
    )
    path.write_text(header + "\n".join(rows) + "\n", encoding="utf-8", newline="\n")
    print(f"{path}: {len(rows)} words", file=sys.stderr)


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    export("el", GREEK_WORD, greek_final_sigma)
    export("en", ENGLISH_WORD)
