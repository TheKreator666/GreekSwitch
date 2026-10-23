# Lexicons (the app's dictionaries)

`el.lex` and `en.lex` hold every word form of the LibreOffice Hunspell dictionaries
(`data/dictionaries/el_GR.*`, `en_US.*`), expanded with their affix rules by `tools/NgramBuilder`
and stored as hashes. They are embedded in GreekSwitch.Core and used by `LexiconDictionaryService`.
The `.dic`/`.aff` files themselves are not shipped.

Why: Hunspell's in-memory structures for el_GR took about 110 MB. These tables take about 7 MB,
need no parsing at start-up, and answer each lookup with one binary search instead of dozens of
accent-variant checks. Measured: the app's working set went from 191 MB to 87 MB, with
identical or slightly better evaluation numbers.

## Format (v1)

```
"GSLX"              4 bytes, ASCII
version             1 byte (= 1)
lowercase count     int32, little-endian
capitalised count   int32, little-endian
lowercase hashes    count × uint64, sorted ascending
capitalised hashes  count × uint64, sorted ascending
```

Each hash is 64-bit FNV-1a over the UTF-16 code units of the normalised form: Greek lowercase,
without tonos or dialytika, with ς as σ; English lowercase. Forms whose dictionary entry is all
lowercase go in the first list, forms of capitalised entries (names, acronyms) in the second.
Lowercase input only matches the first list, so `na` is not a word just because `Na` is.

With about 940k forms and 64-bit hashes, the chance of any accidental match is negligible
(around 5 × 10⁻¹⁴ per lookup).

## Regenerating

```bash
dotnet run --project tools/NgramBuilder -c Release -- --lang el \
  --aff data/dictionaries/el_GR.aff --dic data/dictionaries/el_GR.dic \
  --out data/ngrams/el.tri --lexicon data/lexicon/el.lex
dotnet run --project tools/NgramBuilder -c Release -- --lang en \
  --aff data/dictionaries/en_US.aff --dic data/dictionaries/en_US.dic \
  --out data/ngrams/en.tri --lexicon data/lexicon/en.lex
```

Commit the output.
