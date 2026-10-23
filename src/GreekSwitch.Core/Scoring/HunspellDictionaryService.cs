using System.Text;
using GreekSwitch.Core.Data;
using GreekSwitch.Core.Text;
using WeCantSpell.Hunspell;

namespace GreekSwitch.Core.Scoring;

/// <summary>
/// Hunspell lookups through WeCantSpell. Hunspell matches exact forms, so Greek words are
/// normalised (no accents, lowercase, σ) and then re-accented in every plausible way: one tonos
/// on any vowel, optionally a dialytika on ι/υ, in lowercase and Capitalized form. That is a few
/// dozen microsecond-cheap checks per word.
/// </summary>
public sealed class HunspellDictionaryService : IDictionaryService
{
    private const string GreekVowels = "αεηιουω";
    private const string TonosVowels = "άέήίόύώ";

    private readonly Lazy<WordList> _greek;
    private readonly Lazy<WordList> _english;

    public HunspellDictionaryService(Func<WordList> greekFactory, Func<WordList> englishFactory)
    {
        _greek = new Lazy<WordList>(greekFactory, LazyThreadSafetyMode.ExecutionAndPublication);
        _english = new Lazy<WordList>(englishFactory, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>Loads el_GR and en_US from the resources embedded in this assembly.</summary>
    public static HunspellDictionaryService FromEmbeddedResources()
    {
        // el_GR.aff declares SET ISO8859-7, which .NET only decodes with the code-pages provider.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return new HunspellDictionaryService(
            () => Load(EmbeddedData.GreekDictionary, EmbeddedData.GreekAffixes),
            () => Load(EmbeddedData.EnglishDictionary, EmbeddedData.EnglishAffixes));
    }

    /// <summary>Forces both dictionaries to load (about a second for Greek) so the first word isn't slow.</summary>
    public void Preload()
    {
        _ = _greek.Value;
        _ = _english.Value;
    }

    public bool IsWord(string word, Lang lang)
    {
        if (word.Length == 0)
        {
            return false;
        }

        return lang == Lang.Greek ? IsGreekWord(word) : IsEnglishWord(word);
    }

    private static WordList Load(string dictionary, string affixes)
    {
        using var dic = EmbeddedData.Open(dictionary);
        using var aff = EmbeddedData.Open(affixes);
        return WordList.CreateFromStreams(dic, aff);
    }

    private bool IsEnglishWord(string word)
    {
        if (word.Any(GreekText.IsGreekLetter))
        {
            return false;
        }

        var list = _english.Value;
        var lower = word.ToLowerInvariant();
        return list.Check(word) || list.Check(lower) || list.Check(Capitalize(lower));
    }

    private bool IsGreekWord(string word)
    {
        if (!word.Any(GreekText.IsGreekLetter) || word.Any(c => c is >= 'A' and <= 'z') || GreekText.HasMisplacedFinalSigma(word))
        {
            return false;
        }

        var list = _greek.Value;
        if (list.Check(word))
        {
            return true;
        }

        var bare = GreekText.FixFinalSigma(GreekText.NormalizeForMatch(word));
        foreach (var candidate in AccentVariants(bare))
        {
            if (list.Check(candidate) || list.Check(Capitalize(candidate)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The bare word, then each way to add one tonos and/or one dialytika.</summary>
    private static IEnumerable<string> AccentVariants(string bare)
    {
        var vowels = new List<int>();
        var diaeresisTargets = new List<int>();
        for (var i = 0; i < bare.Length; i++)
        {
            if (GreekVowels.Contains(bare[i], StringComparison.Ordinal))
            {
                vowels.Add(i);
            }

            if (bare[i] is 'ι' or 'υ')
            {
                diaeresisTargets.Add(i);
            }
        }

        var chars = bare.ToCharArray();
        foreach (var tonos in vowels.Prepend(-1))
        {
            foreach (var dialytika in diaeresisTargets.Prepend(-1))
            {
                var variant = (char[])chars.Clone();
                if (dialytika >= 0)
                {
                    variant[dialytika] = variant[dialytika] == 'ι' ? 'ϊ' : 'ϋ';
                }

                if (tonos >= 0)
                {
                    variant[tonos] = tonos == dialytika
                        ? (variant[tonos] == 'ϊ' ? 'ΐ' : 'ΰ')
                        : TonosVowels[GreekVowels.IndexOf(chars[tonos], StringComparison.Ordinal)];
                }

                yield return new string(variant);
            }
        }
    }

    private static string Capitalize(string lower) =>
        lower.Length == 0 ? lower : char.ToUpperInvariant(lower[0]) + lower[1..];
}
