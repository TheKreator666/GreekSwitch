namespace GreekSwitch.Core.Loanwords;

/// <summary>
/// CMU Pronouncing Dictionary (ARPAbet) → shared phonemes. Used at build time by
/// tools/PhoneticIndexBuilder; the app only reads the result. English vowels collapse onto
/// Greek's five (AH → a, ER → e r, EY → e i), as in docs/loanword-design.md, with one change:
/// Y is the semivowel y rather than i, so it doesn't count as a vowel for the stress position.
/// </summary>
public static class ArpabetPhonetics
{
    /// <summary>
    /// The phonemes of one pronunciation ("K AH0 N T EY1 N ER0"), or null if a symbol is unknown.
    /// Primary stress (digit 1) marks the stressed vowel. NG is n g (meeting: μίτινγκ), but only n
    /// before K or G (link: λινκ).
    /// </summary>
    public static PhoneticWord? Transcribe(IEnumerable<string> symbols)
    {
        var output = new PhoneticWordBuilder();
        var list = symbols.ToList();
        for (var i = 0; i < list.Count; i++)
        {
            var stressed = list[i].EndsWith('1');
            var symbol = list[i].TrimEnd('0', '1', '2');
            if (symbol == "NG" && i + 1 < list.Count && list[i + 1] is "K" or "G")
            {
                output.Add(Phoneme.N); // link = l i n k, as Greek writes it (λινκ)
                continue;
            }

            if (!Add(symbol, stressed, output))
            {
                return null;
            }
        }

        return output.Count == 0 ? null : output.Build();
    }

    private static bool Add(string symbol, bool stressed, PhoneticWordBuilder output)
    {
        switch (symbol)
        {
            case "AA" or "AE" or "AH":
                output.Add(Phoneme.A, stressed);
                return true;
            case "AO" or "OW":
                output.Add(Phoneme.O, stressed);
                return true;
            case "EH":
                output.Add(Phoneme.E, stressed);
                return true;
            case "ER":
                output.Add(Phoneme.E, Phoneme.R, stressed);
                return true;
            case "EY":
                output.Add(Phoneme.E, Phoneme.I, stressed);
                return true;
            case "IH" or "IY":
                output.Add(Phoneme.I, stressed);
                return true;
            case "UH" or "UW":
                output.Add(Phoneme.U, stressed);
                return true;
            case "AY":
                output.Add(Phoneme.A, Phoneme.I, stressed);
                return true;
            case "AW":
                output.Add(Phoneme.A, Phoneme.U, stressed);
                return true;
            case "OY":
                output.Add(Phoneme.O, Phoneme.I, stressed);
                return true;
            case "W":
                output.Add(Phoneme.U);
                return true;
            case "CH":
                output.Add(Phoneme.T, Phoneme.S);
                return true;
            case "JH":
                output.Add(Phoneme.D, Phoneme.Z);
                return true;
            case "NG":
                output.Add(Phoneme.N, Phoneme.G);
                return true;
        }

        Phoneme? single = symbol switch
        {
            "Y" => Phoneme.Y,
            "B" => Phoneme.B,
            "D" => Phoneme.D,
            "G" => Phoneme.G,
            "K" => Phoneme.K,
            "P" => Phoneme.P,
            "T" => Phoneme.T,
            "F" => Phoneme.F,
            "V" => Phoneme.V,
            "S" or "SH" => Phoneme.S,
            "Z" or "ZH" => Phoneme.Z,
            "TH" => Phoneme.Th,
            "DH" => Phoneme.Dh,
            "HH" => Phoneme.H,
            "M" => Phoneme.M,
            "N" => Phoneme.N,
            "L" => Phoneme.L,
            "R" => Phoneme.R,
            _ => null,
        };

        if (single is not { } p)
        {
            return false;
        }

        output.Add(p);
        return true;
    }
}
