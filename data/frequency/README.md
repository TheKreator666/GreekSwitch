# Word frequency tables

| File | What | Used by |
|---|---|---|
| `el.tsv`, `en.tsv` | The most frequent words of each language as `word<TAB>zipf` (Zipf = log10 of occurrences per billion words), most frequent first. Exported from wordfreq by `tools/FrequencyExport/export_wordfreq.py`. | the evaluation harness; the input to the builder below |
| `el.zipf`, `en.zipf` | The same, compiled to a compact binary table by `tools/FrequencyTableBuilder`, and **embedded in GreekSwitch.Core** | the decision engine (both-readings-are-words rule, short-word rule) |

License: the data comes from wordfreq (https://github.com/rspeer/wordfreq) and is **CC BY-SA 4.0**,
and so are these tables, also in their embedded form. See THIRD_PARTY_NOTICES.md.

## Binary format (`.zipf`, v1)

```
"GSZF"            4 bytes, ASCII
version           1 byte (= 1)
count             int32, little-endian
count × entry:    zipf × 25 (byte, rounded, clamped 0–255)
                  key length (byte) + key (UTF-8)
```

Keys are normalised the way `WordFrequencyTable` looks them up. Greek keys are lowercase,
without tonos or dialytika, with ς as σ. English keys are lowercase. When several words share a
key (που/πού), the most frequent one wins. Loading builds one dictionary per language (~90k keys).

## Regenerating

```bash
pip install wordfreq==3.1.1
python tools/FrequencyExport/export_wordfreq.py          # → el.tsv, en.tsv
dotnet run --project tools/FrequencyTableBuilder -c Release -- data/frequency   # → el.zipf, en.zipf
```

Commit all four files.
