using GreekSwitch.Core.Input;
using GreekSwitch.Core.Layout;
using GreekSwitch.Core.Scoring;
using GreekSwitch.Core.Text;

namespace GreekSwitch.Core.Decisions;

/// <summary>
/// Decides whether a completed word was typed in the wrong layout. Rules, in order:
/// 1. code-like words are kept; 2. never-convert words are kept; 2b. if both readings are words
/// (and there are 2+ keys), replace only when context favours the other language and the other
/// reading is far more frequent; 3. short words are kept unless the alternative is a common word,
/// the current reading isn't a word, and context agrees; 4. replace if the current reading isn't a word but
/// the alternative is, or (only when the current reading is not a word) the alternative's combined
/// score wins by more than ReplaceThreshold; 5. otherwise keep.
/// </summary>
public sealed class DecisionEngine
{
    private readonly DualDecoder _decoder;
    private readonly Scorer _scorer;
    private readonly INeverConvertList _neverConvert;
    private readonly ContextTracker _context = new();
    private volatile ScoringOptions _options;

    public DecisionEngine(DualDecoder decoder, Scorer scorer, INeverConvertList neverConvert, ScoringOptions? options = null)
    {
        _decoder = decoder;
        _scorer = scorer;
        _neverConvert = neverConvert;
        _options = options ?? new ScoringOptions();
    }

    /// <summary>Current weights; can be swapped at runtime when settings change.</summary>
    public ScoringOptions Options
    {
        get => _options;
        set => _options = value;
    }

    /// <summary>Language of the most recent accepted words (null with no history or a tie).</summary>
    public Lang? ContextLanguage => _context.Majority;

    public void ClearContext() => _context.Clear();

    /// <summary>Tests and evaluation: make the recent words all <paramref name="lang"/>.</summary>
    internal void SeedContext(Lang lang)
    {
        _context.Clear();
        for (var i = 0; i < _options.ContextWindow; i++)
        {
            _context.Record(lang, _options.ContextWindow);
        }
    }

    public Decision Decide(CompletedWord word) =>
        word.Tainted ? new Keep(KeepReason.PartialWord)
        : word.MixedLayout ? new Keep(KeepReason.MixedLayout)
        : Decide(word.Keys, word.Layout, word.FollowsPeriod);

    public Decision Decide(IReadOnlyList<KeyStroke> keys, Lang currentLayout, bool followsPeriod = false)
    {
        var options = _options;
        var alternativeLayout = currentLayout.Other();
        var current = _decoder.Decode(keys, currentLayout);
        var alternative = _decoder.Decode(keys, alternativeLayout);

        // Rule 1: code, URLs, identifiers.
        if (followsPeriod || IsCodeLike(keys, current, alternative))
        {
            return new Keep(KeepReason.CodeLike);
        }

        // Rule 2: the user said no.
        if (_neverConvert.Contains(Scorer.LexicalCore(current)) || _neverConvert.Contains(Scorer.LexicalCore(alternative)))
        {
            return new Keep(KeepReason.NeverConvert);
        }

        var currentScore = _scorer.Score(current, currentLayout, _context, options);
        var alternativeScore = _scorer.Score(alternative, alternativeLayout, _context, options);
        var alternativeIsBetterWord = alternativeScore.IsWord && !currentScore.IsWord;

        // Both readings are words ("den"/"δεν", "toy"/"του"): the dictionary can't decide, so
        // take the far more frequent one, but only when the surrounding words agree.
        if (keys.Count >= 2 && currentScore.IsWord && alternativeScore.IsWord
            && _context.Majority == alternativeLayout
            && alternativeScore.Zipf >= currentScore.Zipf + options.FrequencyMargin)
        {
            return Accept(current, alternative, currentLayout, ReplaceReason.FrequencyInContext);
        }

        // Rule 3: short words need the dictionary *and* the context to agree.
        if (keys.Count < options.MinLength)
        {
            return alternativeIsBetterWord && _context.Majority == alternativeLayout
                && alternativeScore.Zipf >= options.ShortWordMinZipf
                ? Accept(current, alternative, currentLayout, ReplaceReason.ShortWordInContext)
                : KeepWord(KeepReason.TooShort, currentScore);
        }

        // Rule 4: dictionary evidence, or a clear margin on the combined score.
        if (alternativeIsBetterWord)
        {
            return Accept(current, alternative, currentLayout, ReplaceReason.Dictionary);
        }

        // The margin only arbitrates between non-words: a reading that is a dictionary word is
        // never replaced on letter statistics alone (both-words cases are handled above, by
        // frequency and context).
        if (!currentScore.IsWord && alternativeScore.Total - currentScore.Total > options.ReplaceThreshold)
        {
            return Accept(current, alternative, currentLayout, ReplaceReason.ScoreMargin);
        }

        // Rule 5.
        return KeepWord(KeepReason.Plausible, currentScore);
    }

    /// <summary>
    /// Digits, symbols such as @ / : _ # (from any key except the two whose meaning differs
    /// between layouts: the Greek accent key and Q), no letters at all, or mixed camelCase in
    /// both readings. (Shift+W is ΅ in Greek but W in English, so "camelCase" in only one
    /// reading is just a ΐ or ΰ.)
    /// </summary>
    private static bool IsCodeLike(IReadOnlyList<KeyStroke> keys, string current, string alternative)
    {
        foreach (var key in keys)
        {
            var vk = key.VirtualKey;
            if (!VirtualKeys.IsLetter(vk) && vk is not (VirtualKeys.Oem1 or VirtualKeys.Oem7))
            {
                return true;
            }
        }

        return !current.Any(char.IsLetter) || (IsCamelCase(current) && IsCamelCase(alternative));
    }

    private static bool IsCamelCase(string text)
    {
        var seenLower = false;
        foreach (var c in text)
        {
            if (char.IsLower(c))
            {
                seenLower = true;
            }
            else if (seenLower && char.IsUpper(c))
            {
                return true;
            }
        }

        return false;
    }

    private Keep KeepWord(KeepReason reason, ReadingScore current)
    {
        if (current.IsWord)
        {
            _context.Record(current.Lang, _options.ContextWindow);
        }

        return new Keep(reason);
    }

    private Replace Accept(string current, string alternative, Lang currentLayout, ReplaceReason reason)
    {
        var target = currentLayout.Other();
        _context.Record(target, _options.ContextWindow);
        var text = target == Lang.Greek ? GreekText.FixFinalSigma(alternative) : alternative;
        return new Replace(current, text, currentLayout, target, reason);
    }
}
