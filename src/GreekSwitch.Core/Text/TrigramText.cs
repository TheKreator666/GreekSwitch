using System.Text;

namespace GreekSwitch.Core.Text;

/// <summary>
/// Normalisation shared by NgramBuilder (training) and TrigramModel (scoring), so both see the
/// same symbols. Greek: lowercase, no accents, ς → σ, letters α-ω. English: lowercase a-z and the
/// apostrophe. Anything else becomes <see cref="Unknown"/>, which the model has rarely seen and
/// therefore scores poorly — e.g. the ';' of a tonos key read as English.
/// </summary>
public static class TrigramText
{
    public const char Start = '^';
    public const char End = '$';
    public const char Unknown = '?';

    public static string Normalize(string word, Lang lang)
    {
        var text = lang == Lang.Greek ? GreekText.NormalizeForMatch(MarkMisplacedFinalSigma(word)) : word.ToLowerInvariant();
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            sb.Append(IsInAlphabet(c, lang) ? c : Unknown);
        }

        return sb.ToString();
    }

    /// <summary>A ς before another letter isn't Greek spelling: score it as an unknown symbol, not as σ.</summary>
    private static string MarkMisplacedFinalSigma(string word)
    {
        if (!GreekText.HasMisplacedFinalSigma(word))
        {
            return word;
        }

        var chars = word.ToCharArray();
        for (var i = 0; i < chars.Length - 1; i++)
        {
            if (chars[i] == 'ς' && char.IsLetter(chars[i + 1]))
            {
                chars[i] = Unknown;
            }
        }

        return new string(chars);
    }

    /// <summary>"^^" + normalised word + "$": one prediction per letter plus the end of word.</summary>
    public static string Pad(string normalized) => $"{Start}{Start}{normalized}{End}";

    public static bool IsInAlphabet(char c, Lang lang) => lang == Lang.Greek
        ? c is >= 'α' and <= 'ω' && c != 'ς'
        : c is (>= 'a' and <= 'z') or '\'';
}
