"""Sample evaluation sentences from Tatoeba per-language exports.

    curl -LO https://downloads.tatoeba.org/exports/per_language/ell/ell_sentences.tsv.bz2
    curl -LO https://downloads.tatoeba.org/exports/per_language/eng/eng_sentences.tsv.bz2
    python tools/FrequencyExport/sample_tatoeba.py ell_sentences.tsv.bz2 eng_sentences.tsv.bz2

Writes tests/GreekSwitch.Evaluation/Data/sentences.el.tsv and sentences.en.tsv: a fixed-seed
sample of plain sentences (5-14 words, letters and simple punctuation only) as "id<TAB>text".
Tatoeba sentences are CC BY 2.0 FR (https://tatoeba.org); the ids link back to the originals.
"""

import bz2
import pathlib
import random
import re
import sys

SAMPLE = 400
SEED = 42
OUT = pathlib.Path(__file__).resolve().parents[2] / "tests" / "GreekSwitch.Evaluation" / "Data"

PATTERNS = {
    "el": re.compile(r"^[Ά-ώ ,.;!']+$"),
    "en": re.compile(r"^[A-Za-z ,.?!']+$"),
}


def sample(path: str, lang: str) -> None:
    pattern = PATTERNS[lang]
    candidates = []
    with bz2.open(path, "rt", encoding="utf-8") as f:
        for line in f:
            parts = line.rstrip("\n").split("\t")
            if len(parts) != 3:
                continue
            sid, _, text = parts
            words = text.split()
            if 5 <= len(words) <= 14 and pattern.match(text) and "  " not in text:
                candidates.append((int(sid), text))

    candidates.sort()
    chosen = sorted(random.Random(SEED).sample(candidates, SAMPLE))
    out = OUT / f"sentences.{lang}.tsv"
    header = (
        f"# {SAMPLE} sentences sampled (seed {SEED}) from Tatoeba ({lang}), https://tatoeba.org\n"
        "# License: CC BY 2.0 FR. Each id is the Tatoeba sentence id: https://tatoeba.org/sentences/show/<id>\n"
    )
    out.write_text(header + "".join(f"{sid}\t{text}\n" for sid, text in chosen), encoding="utf-8", newline="\n")
    print(f"{out}: {len(chosen)} of {len(candidates)} candidates", file=sys.stderr)


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    sample(sys.argv[1], "el")
    sample(sys.argv[2], "en")
