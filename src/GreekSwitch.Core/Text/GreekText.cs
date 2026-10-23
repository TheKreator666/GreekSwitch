using System.Globalization;
using System.Text;

namespace GreekSwitch.Core.Text;

/// <summary>Pure helpers for normalising Greek text.</summary>
public static class GreekText
{
    public static bool IsGreekLetter(char c) => c is >= 'Ά' and <= 'ώ' && char.IsLetter(c);

    /// <summary>
    /// Uses ς for a lowercase sigma at the end of a word and σ everywhere else, which is what a
    /// Greek typist means whichever of the two keys they hit.
    /// </summary>
    public static string FixFinalSigma(string text)
    {
        if (text.IndexOfAny(['σ', 'ς']) < 0)
        {
            return text;
        }

        var chars = text.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] is not ('σ' or 'ς'))
            {
                continue;
            }

            var nextIsLetter = i + 1 < chars.Length && char.IsLetter(chars[i + 1]);
            var prevIsLetter = i > 0 && char.IsLetter(chars[i - 1]);
            chars[i] = !nextIsLetter && prevIsLetter ? 'ς' : 'σ';
        }

        return new string(chars);
    }

    /// <summary>Removes tonos and dialytika (ά → α, ϊ → ι, ΰ → υ). Other characters are untouched.</summary>
    public static string StripDiacritics(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Accent-, case- and sigma-insensitive key: lowercase, no diacritics, ς → σ.</summary>
    public static string NormalizeForMatch(string text) =>
        StripDiacritics(text).ToLowerInvariant().Replace('ς', 'σ');
}
