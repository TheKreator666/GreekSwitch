namespace GreekSwitch.Core.Loanwords;

/// <summary>
/// The shared, simplified phoneme alphabet both languages are transcribed into: Greek's five
/// vowels, the semivowel y, and the consonants of both. English vowels collapse onto the five
/// (container = k a n t e i n e r). Affricates and clusters are sequences (ts = T S, x = K S).
/// The byte values are stored in data/phonetic/en.idx: only append, never renumber.
/// </summary>
public enum Phoneme : byte
{
    A,
    E,
    I,
    O,
    U,

    /// <summary>Semivowel: Greek γ before ε/ι sounds, English Y ("yes", the y in "computer").</summary>
    Y,

    P,
    B,
    T,
    D,
    K,
    G,
    F,
    V,

    /// <summary>θ, English TH.</summary>
    Th,

    /// <summary>δ, English DH.</summary>
    Dh,
    S,
    Z,

    /// <summary>χ, English HH.</summary>
    H,

    /// <summary>γ before a, o, u sounds and consonants.</summary>
    Gh,
    M,
    N,
    L,
    R,
}

/// <summary>Classes of phonemes, used by the distance costs and the consonant skeleton.</summary>
public static class Phonemes
{
    /// <summary>Number of symbols; every byte below this is a valid <see cref="Phoneme"/>.</summary>
    public const int Count = (int)Phoneme.R + 1;

    /// <summary>One of the five vowels (counted for the stress position).</summary>
    public static bool IsVowel(Phoneme p) => p <= Phoneme.U;

    /// <summary>A vowel or the semivowel y: cheap to insert, delete or substitute.</summary>
    public static bool IsVowelLike(Phoneme p) => p <= Phoneme.Y;

    /// <summary>
    /// The coarse consonant class a phoneme contributes to the skeleton, or null for vowels and y.
    /// Voicing is ignored (b/p, d/t, g/k/γ, v/f, δ/θ, z/s), because transliterations often
    /// change it (κομπιούτερ has μπ where computer has p).
    /// </summary>
    public static Phoneme? SkeletonClass(Phoneme p) => p switch
    {
        _ when IsVowelLike(p) => null,
        Phoneme.B => Phoneme.P,
        Phoneme.D => Phoneme.T,
        Phoneme.G or Phoneme.Gh => Phoneme.K,
        Phoneme.V => Phoneme.F,
        Phoneme.Dh => Phoneme.Th,
        Phoneme.Z => Phoneme.S,
        _ => p,
    };

    /// <summary>Readable form for tests and diagnostics: "k o n t e i n e r".</summary>
    public static string Format(ReadOnlySpan<Phoneme> phonemes)
    {
        var parts = new string[phonemes.Length];
        for (var i = 0; i < phonemes.Length; i++)
        {
            parts[i] = phonemes[i].ToString().ToLowerInvariant();
        }

        return string.Join(' ', parts);
    }
}
