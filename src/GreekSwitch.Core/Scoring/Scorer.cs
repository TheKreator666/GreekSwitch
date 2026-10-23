namespace GreekSwitch.Core.Scoring;

/// <summary>How plausible one reading of a word is in one language.</summary>
public readonly record struct ReadingScore(Lang Lang, bool IsWord, double LogProb, double ContextPrior, double Total);

public sealed class Scorer
{
    private readonly IDictionaryService _dictionary;
    private readonly ITrigramModel _trigrams;

    public Scorer(IDictionaryService dictionary, ITrigramModel trigrams)
    {
        _dictionary = dictionary;
        _trigrams = trigrams;
    }

    public ReadingScore Score(string reading, Lang lang, ContextTracker context, ScoringOptions options)
    {
        var core = LexicalCore(reading);
        var isWord = core.Length > 0 && _dictionary.IsWord(core, lang);
        var logProb = core.Length > 0 ? _trigrams.LogProb(core, lang) : double.NegativeInfinity;
        var prior = options.ContextWeight * context.Share(lang);
        var total = (isWord ? options.DictionaryWeight : 0) + (options.NgramWeight * logProb) + prior;
        return new ReadingScore(lang, isWord, logProb, prior, total);
    }

    /// <summary>
    /// The word without leading or trailing non-letters, e.g. the Greek question mark in "τι;" or
    /// a stray tonos. Inner apostrophes stay ("don't").
    /// </summary>
    public static string LexicalCore(string reading)
    {
        var start = 0;
        var end = reading.Length;
        while (start < end && !char.IsLetter(reading[start]))
        {
            start++;
        }

        while (end > start && !char.IsLetter(reading[end - 1]))
        {
            end--;
        }

        return reading[start..end];
    }
}
