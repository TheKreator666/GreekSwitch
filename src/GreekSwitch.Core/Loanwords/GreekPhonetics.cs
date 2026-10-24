using System.Text;

namespace GreekSwitch.Core.Loanwords;

/// <summary>
/// Greek spelling → shared phonemes, by rule (docs/loanword-design.md). Greek spelling is almost
/// phonetic, so this runs live, per word. Digraphs are read first; a vowel pair only counts as
/// a digraph when the tonos isn't on its first letter and there's no dialytika on the second
/// (κοντέινερ: έι is two vowels, e i; ει is one, i). The tonos marks the stressed vowel.
/// </summary>
public static class GreekPhonetics
{
    private const char End = '\0';

    /// <summary>
    /// The phonemes of a Greek word, or null if it contains anything but Greek letters. Case,
    /// accents and final-sigma form don't matter.
    /// </summary>
    public static PhoneticWord? Transcribe(string word)
    {
        if (!TryDecompose(word, out var letters, out var tonos, out var dialytika))
        {
            return null;
        }

        var n = letters.Length;
        var output = new PhoneticWordBuilder();
        var i = 0;
        while (i < n)
        {
            var c = letters[i];
            var next = At(letters, i + 1);
            var joins = i + 1 < n && !tonos[i] && !dialytika[i + 1];

            if (IsVowelLetter(c))
            {
                i += ReadVowel(letters, tonos, i, joins, output);
                continue;
            }

            switch (c)
            {
                case 'μ' when next == 'π':
                    Emit(output, BetweenVowels(letters, i), Phoneme.B, Phoneme.M, Phoneme.P);
                    i += 2;
                    break;
                case 'ν' when next == 'τ':
                    Emit(output, BetweenVowels(letters, i), Phoneme.D, Phoneme.N, Phoneme.T);
                    i += 2;
                    break;
                case 'γ':
                    i += ReadGamma(letters, tonos, dialytika, i, output);
                    break;
                case 'τ' when next == 'σ':
                    output.Add(Phoneme.T, Phoneme.S);
                    i += 2;
                    break;
                case 'τ' when next == 'ζ':
                    output.Add(Phoneme.D, Phoneme.Z);
                    i += 2;
                    break;
                default:
                    AddConsonant(c, output);
                    i += next == c ? 2 : 1; // λλ, ττ, σσ…: one sound
                    break;
            }
        }

        return output.Count == 0 ? null : output.Build();
    }

    private static int ReadVowel(char[] letters, bool[] tonos, int i, bool joins, PhoneticWordBuilder output)
    {
        var c = letters[i];
        var next = At(letters, i + 1);
        if (joins)
        {
            var stressed = tonos[i + 1];
            switch (c, next)
            {
                case ('ο', 'υ'):
                    output.Add(Phoneme.U, stressed);
                    return 2;
                case ('α', 'ι'):
                    output.Add(Phoneme.E, stressed);
                    return 2;
                case ('ε' or 'ο' or 'υ', 'ι'):
                    output.Add(Phoneme.I, stressed);
                    return 2;
                case ('α' or 'ε', 'υ'):
                    // αυ/ευ: af/ef before a voiceless consonant or at the end, else av/ev.
                    var after = At(letters, i + 2);
                    output.Add(c == 'α' ? Phoneme.A : Phoneme.E, after == End || IsVoiceless(after) ? Phoneme.F : Phoneme.V, stressed);
                    return 2;
            }
        }

        output.Add(SingleVowel(c), tonos[i]);
        return 1;
    }

    private static int ReadGamma(char[] letters, bool[] tonos, bool[] dialytika, int i, PhoneticWordBuilder output)
    {
        var next = At(letters, i + 1);
        switch (next)
        {
            case 'κ':
                output.Add(Phoneme.G);
                return 2;
            case 'γ':
                output.Add(Phoneme.N, Phoneme.G);
                return 2;
            case 'χ':
                output.Add(Phoneme.N, Phoneme.H);
                return 2;
            case 'ξ':
                output.Add(Phoneme.N, Phoneme.K);
                output.Add(Phoneme.S);
                return 2;
        }

        // γου + vowel is how Greek writes English w (σόφτγουερ, γουέμπ).
        if (next == 'ο' && At(letters, i + 2) == 'υ' && !tonos[i + 1] && !dialytika[i + 2] && IsVowelLetter(At(letters, i + 3)))
        {
            output.Add(Phoneme.U, tonos[i + 2]);
            return 3;
        }

        // γι + vowel is a single y (γιούζερ); the vowel after it carries on as usual.
        if (next == 'ι' && !tonos[i + 1] && IsVowelLetter(At(letters, i + 2)) && !dialytika[i + 2])
        {
            output.Add(Phoneme.Y);
            return 2;
        }

        output.Add(StartsFrontVowel(letters, tonos, dialytika, i + 1) ? Phoneme.Y : Phoneme.Gh);
        return 1;
    }

    /// <summary>True if the sound starting at <paramref name="i"/> is e or i (ε, η, ι, υ, αι, ει, οι).</summary>
    private static bool StartsFrontVowel(char[] letters, bool[] tonos, bool[] dialytika, int i)
    {
        var c = At(letters, i);
        if (c is 'ε' or 'η' or 'ι' or 'υ')
        {
            return true;
        }

        return c is 'α' or 'ο' && At(letters, i + 1) == 'ι' && !tonos[i] && !dialytika[i + 1];
    }

    private static void Emit(PhoneticWordBuilder output, bool betweenVowels, Phoneme single, Phoneme first, Phoneme second)
    {
        // Between vowels μπ/ντ usually transcribe English mp/nt (κομπιούτερ, κοντέινερ); at the
        // start or end of a word or next to a consonant they are b/d (ντεπλόι, φίντμπακ, άπντεϊτ).
        // The distance treats b ≈ mp and d ≈ nt as cheap either way.
        if (betweenVowels)
        {
            output.Add(first, second);
        }
        else
        {
            output.Add(single);
        }
    }

    /// <summary>True if the two-letter digraph at <paramref name="i"/> has a vowel letter on both sides.</summary>
    private static bool BetweenVowels(char[] letters, int i) =>
        i > 0 && IsVowelLetter(letters[i - 1]) && IsVowelLetter(At(letters, i + 2));

    private static void AddConsonant(char c, PhoneticWordBuilder output)
    {
        switch (c)
        {
            case 'ξ':
                output.Add(Phoneme.K, Phoneme.S);
                return;
            case 'ψ':
                output.Add(Phoneme.P, Phoneme.S);
                return;
        }

        output.Add(c switch
        {
            'β' => Phoneme.V,
            'δ' => Phoneme.Dh,
            'ζ' => Phoneme.Z,
            'θ' => Phoneme.Th,
            'κ' => Phoneme.K,
            'λ' => Phoneme.L,
            'μ' => Phoneme.M,
            'ν' => Phoneme.N,
            'π' => Phoneme.P,
            'ρ' => Phoneme.R,
            'σ' => Phoneme.S,
            'τ' => Phoneme.T,
            'φ' => Phoneme.F,
            'χ' => Phoneme.H,
            _ => throw new InvalidOperationException("Unhandled Greek consonant."),
        });
    }

    private static Phoneme SingleVowel(char c) => c switch
    {
        'α' => Phoneme.A,
        'ε' => Phoneme.E,
        'ο' or 'ω' => Phoneme.O,
        _ => Phoneme.I, // η, ι, υ
    };

    /// <summary>True for a lowercase Greek consonant letter (ς included).</summary>
    public static bool IsConsonantLetter(char c) => c is >= 'α' and <= 'ω' && !IsVowelLetter(c);

    private static bool IsVowelLetter(char c) => c is 'α' or 'ε' or 'η' or 'ι' or 'ο' or 'υ' or 'ω';

    private static bool IsVoiceless(char c) => c is 'θ' or 'κ' or 'ξ' or 'π' or 'σ' or 'τ' or 'φ' or 'χ' or 'ψ';

    private static char At(char[] letters, int i) => i < letters.Length ? letters[i] : End;

    /// <summary>Lowercase base letters (σ for ς) with their tonos and dialytika flags.</summary>
    private static bool TryDecompose(string word, out char[] letters, out bool[] tonos, out bool[] dialytika)
    {
        var decomposed = word.Normalize(NormalizationForm.FormD);
        var baseLetters = new List<char>(decomposed.Length);
        var acute = new List<bool>(decomposed.Length);
        var diaeresis = new List<bool>(decomposed.Length);
        foreach (var raw in decomposed)
        {
            if (raw is '́' or '΄' or '̈')
            {
                if (baseLetters.Count == 0)
                {
                    letters = [];
                    tonos = dialytika = [];
                    return false;
                }

                if (raw == '̈')
                {
                    diaeresis[^1] = true;
                }
                else
                {
                    acute[^1] = true;
                }

                continue;
            }

            var c = char.ToLowerInvariant(raw);
            if (c == 'ς')
            {
                c = 'σ';
            }

            if (c is < 'α' or > 'ω')
            {
                letters = [];
                tonos = dialytika = [];
                return false;
            }

            baseLetters.Add(c);
            acute.Add(false);
            diaeresis.Add(false);
        }

        letters = [.. baseLetters];
        tonos = [.. acute];
        dialytika = [.. diaeresis];
        return letters.Length > 0;
    }
}
