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
    private readonly INeverConvertList _alwaysConvert;
    private readonly IGreekAccents _accents;
    private readonly ContextTracker _context = new();
    private volatile ScoringOptions _options;

    /// <param name="alwaysConvert">Words the user converted by hotkey: converted automatically from then on.</param>
    /// <param name="accents">Puts the tonos back on Greek replacements typed without it ("kanei" → "κάνει").</param>
    public DecisionEngine(DualDecoder decoder, Scorer scorer, INeverConvertList neverConvert, ScoringOptions? options = null, INeverConvertList? alwaysConvert = null, IGreekAccents? accents = null)
    {
        _decoder = decoder;
        _scorer = scorer;
        _neverConvert = neverConvert;
        _alwaysConvert = alwaysConvert ?? new NeverConvertList();
        _accents = accents ?? new NoGreekAccents();
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
    internal void SeedContext(Lang lang) => Seed(_context, lang);

    /// <summary>
    /// True for replacements strong enough to re-judge the preceding words with (milestone 11):
    /// dictionary evidence, or frequency together with context.
    /// </summary>
    public static bool IsConfident(ReplaceReason reason) =>
        reason is ReplaceReason.Dictionary or ReplaceReason.FrequencyInContext;

    /// <summary>
    /// Judges an earlier word again as if the recent words were all <paramref name="contextLanguage"/>.
    /// No side effects: the engine's own context is neither read nor updated.
    /// </summary>
    public Decision Reconsider(IReadOnlyList<KeyStroke> keys, Lang currentLayout, Lang contextLanguage)
    {
        var context = new ContextTracker();
        Seed(context, contextLanguage);
        return Evaluate(keys, currentLayout, followsPeriod: false, context, record: false);
    }

    /// <summary>What <paramref name="keys"/> look like on screen when shown as <paramref name="shown"/>.</summary>
    /// <remarks>
    /// The typed layout's raw reading if the word was kept, or the replacement text (final sigma
    /// fixed, accents restored) if it was converted to the other language.
    /// </remarks>
    public string Display(IReadOnlyList<KeyStroke> keys, Lang typedLayout, Lang shown)
    {
        var text = _decoder.Decode(keys, shown);
        return shown != typedLayout && shown == Lang.Greek ? AsGreekReplacement(text) : text;
    }

    /// <summary>Greek <paramref name="text"/> with its missing accents put back (ειναι → είναι), or unchanged.</summary>
    public string WithAccents(string text) => _accents.Restore(text);

    /// <summary>A Greek reading as it is typed out when converted: final sigma fixed and missing accents put back.</summary>
    private string AsGreekReplacement(string reading) => _accents.Restore(GreekText.FixFinalSigma(reading));

    public Decision Decide(CompletedWord word) =>
        word.Tainted ? new Keep(KeepReason.PartialWord)
        : word.MixedLayout ? new Keep(KeepReason.MixedLayout)
        : Decide(word.Keys, word.Layout, word.FollowsPeriod);

    public Decision Decide(IReadOnlyList<KeyStroke> keys, Lang currentLayout, bool followsPeriod = false) =>
        Evaluate(keys, currentLayout, followsPeriod, _context, record: true);

    /// <summary>
    /// Like <see cref="Decide(CompletedWord)"/>, with the same context, but without recording the
    /// word in it: for a replacement that is only offered (suggest-only apps) and may be ignored.
    /// </summary>
    public Decision Peek(CompletedWord word) =>
        word.Tainted ? new Keep(KeepReason.PartialWord)
        : word.MixedLayout ? new Keep(KeepReason.MixedLayout)
        : Evaluate(word.Keys, word.Layout, word.FollowsPeriod, _context, record: false);

    /// <summary>A word now shows in <paramref name="lang"/> (an offered replacement was accepted).</summary>
    public void RecordContext(Lang lang) => _context.Record(lang, _options.ContextWindow);

    private Decision Evaluate(IReadOnlyList<KeyStroke> keys, Lang currentLayout, bool followsPeriod, ContextTracker context, bool record)
    {
        var options = _options;
        var alternativeLayout = currentLayout.Other();
        var current = _decoder.Decode(keys, currentLayout);
        var alternative = _decoder.Decode(keys, alternativeLayout);

        // Accidental Caps Lock (optional): "hELLO" typed with Caps Lock on means "Hello".
        if (options.FixAccidentalCapsLock && IsAccidentalCapsLock(keys, current))
        {
            if (record)
            {
                context.Record(currentLayout, options.ContextWindow);
            }

            return new Replace(current, InvertCase(current), currentLayout, currentLayout, ReplaceReason.AccidentalCapsLock, TurnOffCapsLock: true);
        }

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

        // Rule 2b: the user converted these keys by hotkey before.
        if (_alwaysConvert.Contains(Scorer.LexicalCore(current)))
        {
            return Accept(current, alternative, currentLayout, ReplaceReason.UserChoice, context, record);
        }

        var currentScore = _scorer.Score(current, currentLayout, context, options);
        var alternativeScore = _scorer.Score(alternative, alternativeLayout, context, options);
        var alternativeIsBetterWord = alternativeScore.IsWord && !currentScore.IsWord;

        // Both readings are words ("den"/"δεν", "toy"/"του"): the dictionary can't decide, so
        // take the far more frequent one, but only when the surrounding words agree.
        if (keys.Count >= 2 && currentScore.IsWord && alternativeScore.IsWord
            && context.Majority == alternativeLayout
            && alternativeScore.Zipf >= currentScore.Zipf + options.FrequencyMargin)
        {
            return Accept(current, alternative, currentLayout, ReplaceReason.FrequencyInContext, context, record);
        }

        // Rule 3: short words need the dictionary *and* the context to agree.
        if (keys.Count < options.MinLength)
        {
            return alternativeIsBetterWord && context.Majority == alternativeLayout
                && alternativeScore.Zipf >= options.ShortWordMinZipf
                ? Accept(current, alternative, currentLayout, ReplaceReason.ShortWordInContext, context, record)
                : KeepWord(KeepReason.TooShort, currentScore, context, record);
        }

        // Rule 4: dictionary evidence, or a clear margin on the combined score.
        if (alternativeIsBetterWord)
        {
            return Accept(current, alternative, currentLayout, ReplaceReason.Dictionary, context, record);
        }

        // The margin only arbitrates between non-words: a reading that is a dictionary word is
        // never replaced on letter statistics alone (both-words cases are handled above, by
        // frequency and context).
        if (!currentScore.IsWord && alternativeScore.Total - currentScore.Total > options.ReplaceThreshold)
        {
            return Accept(current, alternative, currentLayout, ReplaceReason.ScoreMargin, context, record);
        }

        // Rule 5.
        return KeepWord(KeepReason.Plausible, currentScore, context, record);
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

    /// <summary>Every key typed with Caps Lock on, and the result is "hELLO": first letter lower, all others upper.</summary>
    private static bool IsAccidentalCapsLock(IReadOnlyList<KeyStroke> keys, string current)
    {
        var letters = current.Where(char.IsLetter).ToList();
        return keys.All(k => k.CapsLock) && letters.Count >= 2
            && char.IsLower(letters[0]) && letters.Skip(1).All(char.IsUpper);
    }

    private static string InvertCase(string text) =>
        string.Concat(text.Select(c => char.IsUpper(c) ? char.ToLowerInvariant(c) : char.IsLower(c) ? char.ToUpperInvariant(c) : c));

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

    private void Seed(ContextTracker context, Lang lang)
    {
        context.Clear();
        for (var i = 0; i < _options.ContextWindow; i++)
        {
            context.Record(lang, _options.ContextWindow);
        }
    }

    private Keep KeepWord(KeepReason reason, ReadingScore current, ContextTracker context, bool record)
    {
        if (record && current.IsWord)
        {
            context.Record(current.Lang, _options.ContextWindow);
        }

        return new Keep(reason);
    }

    private Replace Accept(string current, string alternative, Lang currentLayout, ReplaceReason reason, ContextTracker context, bool record)
    {
        var target = currentLayout.Other();
        if (record)
        {
            context.Record(target, _options.ContextWindow);
        }

        var text = target == Lang.Greek ? AsGreekReplacement(alternative) : alternative;
        return new Replace(current, text, currentLayout, target, reason);
    }
}
