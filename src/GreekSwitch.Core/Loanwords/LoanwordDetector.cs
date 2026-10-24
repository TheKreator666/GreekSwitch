using System.Globalization;
using GreekSwitch.Core.Scoring;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GreekSwitch.Core.Loanwords;

/// <summary>
/// Decides what to do with a correctly typed Greek word that may be an English word written in
/// Greek letters (κοντέινερ → container). See docs/loanword-design.md.
/// </summary>
public interface ILoanwordDetector
{
    /// <summary>False until the phonetic index is loaded; <see cref="Check"/> returns None until then.</summary>
    bool IsReady { get; }

    /// <summary>
    /// Call for a completed word the layout stage kept, whose reading is Greek. Returns None,
    /// Replace (tier 1 user pair, tier 2 strong match) or Suggest (tier 3).
    /// </summary>
    LoanwordResult Check(string greekWord);

    /// <summary>
    /// The user accepted <paramref name="target"/> for <paramref name="greekWord"/> (Tab on a
    /// suggestion). After <see cref="LoanwordOptions.PromoteAfterAccepts"/> accepts the pair is tier 1.
    /// </summary>
    void Accept(string greekWord, string target);

    /// <summary>
    /// The user undid a loanword replacement or explicitly rejected a suggestion: demotes a
    /// promoted pair, and after <see cref="LoanwordOptions.SuppressAfterRejects"/> rejections
    /// the suggestion is no longer shown.
    /// </summary>
    void Reject(string greekWord, string target);
}

/// <summary>
/// The three tiers of the loanword design, over a <see cref="LoanwordMatcher"/>. Thread-safe:
/// the index is swapped in once with <see cref="UseIndex"/>, options are replaced atomically,
/// and the pair store and vocabulary lock internally. Logs tiers, score buckets and lengths only,
/// never the words.
/// </summary>
public sealed class LoanwordDetector : ILoanwordDetector
{
    private readonly LoanwordPairStore _pairs;
    private readonly IDictionaryService _dictionary;
    private readonly IWordFrequency _frequency;
    private readonly DomainVocabulary _vocabulary;
    private readonly ILogger _logger;
    private volatile LoanwordMatcher? _matcher;
    private volatile LoanwordOptions _options;

    public LoanwordDetector(
        LoanwordPairStore pairs,
        IDictionaryService dictionary,
        IWordFrequency frequency,
        DomainVocabulary? vocabulary = null,
        LoanwordOptions? options = null,
        ILogger? logger = null)
    {
        _pairs = pairs;
        _dictionary = dictionary;
        _frequency = frequency;
        _vocabulary = vocabulary ?? new DomainVocabulary();
        _options = options ?? new LoanwordOptions();
        _logger = logger ?? NullLogger.Instance;
    }

    public bool IsReady => _matcher is not null;

    public LoanwordOptions Options
    {
        get => _options;
        set => _options = value ?? throw new ArgumentNullException(nameof(value));
    }

    public LoanwordPairStore Pairs => _pairs;

    public DomainVocabulary Vocabulary => _vocabulary;

    /// <summary>Makes the detector ready. Load the index off the UI thread (it takes tens of ms).</summary>
    public void UseIndex(PhoneticIndex index) => _matcher = new LoanwordMatcher(index);

    public LoanwordResult Check(string greekWord)
    {
        var matcher = _matcher;
        var options = _options;
        if (matcher is null || greekWord.Count(char.IsLetter) < options.MinLetters
            || GreekPhonetics.Transcribe(greekWord) is not { } phonetic)
        {
            return LoanwordResult.None;
        }

        var pair = _pairs.Find(greekWord);
        if (pair is { } p && (p.Explicit || p.Accepts >= options.PromoteAfterAccepts))
        {
            _logger.LogInformation("Loanword replaced, tier {Tier}, {Source}, length {Length}", (int)LoanwordTier.UserPair, p.Explicit ? "user pair" : "promoted", greekWord.Length);
            return new LoanwordReplace(MatchCase(greekWord, p.Target), LoanwordTier.UserPair);
        }

        var greekZipf = _frequency.Zipf(greekWord, Lang.Greek);
        if (greekZipf >= options.MaxGreekZipf)
        {
            return LoanwordResult.None;
        }

        var inDictionary = _dictionary.IsWord(greekWord, Lang.Greek);
        if (inDictionary && (IsName(greekWord) || (options.DictionaryWordsNeedForeignEnding && !HasForeignEnding(greekWord))))
        {
            return LoanwordResult.None;
        }

        if (Best(matcher.Match(phonetic, options, options.SuggestThreshold - options.AcceptedBonus), pair, options) is not { } best
            || best.Score < options.SuggestThreshold)
        {
            return LoanwordResult.None;
        }

        if (pair is { } rejected && rejected.Target == best.Word && rejected.Rejects >= options.SuppressAfterRejects)
        {
            _logger.LogDebug("Loanword suggestion suppressed after {Rejects} rejections, length {Length}", rejected.Rejects, greekWord.Length);
            return LoanwordResult.None;
        }

        if (best.Zipf > options.MaxEnglishZipf || (greekZipf > 0 && best.Zipf - greekZipf < options.MinEnglishZipfAdvantage))
        {
            return LoanwordResult.None; // a function word, or an established Greek word (πίτσα, pizza)
        }

        if (options.AutoReplaceStrongMatches && best.Score >= options.StrongThreshold
            && _vocabulary.Contains(best.Word) && !inDictionary)
        {
            _logger.LogInformation("Loanword replaced, tier {Tier}, score {Score}, length {Length}", (int)LoanwordTier.StrongMatch, Bucket(best.Score), greekWord.Length);
            return new LoanwordReplace(MatchCase(greekWord, best.Word), LoanwordTier.StrongMatch);
        }

        _logger.LogDebug("Loanword suggested, score {Score}, length {Length}", Bucket(best.Score), greekWord.Length);
        return new LoanwordSuggest(MatchCase(greekWord, best.Word), best.Score);
    }

    public void Accept(string greekWord, string target)
    {
        if (_pairs.Find(greekWord) is { Explicit: true })
        {
            return; // already a user pair
        }

        var accepts = _pairs.RecordAccept(greekWord, target);
        _logger.LogInformation(
            "Loanword suggestion accepted, accepts {Accepts}, promoted {Promoted}, learned {Learned}",
            accepts,
            accepts >= _options.PromoteAfterAccepts,
            accepts > 0);
    }

    public void Reject(string greekWord, string target)
    {
        if (_pairs.Find(greekWord) is { Explicit: true })
        {
            return; // the user's own pair: an undo is just an undo
        }

        var rejects = _pairs.RecordReject(greekWord, target);
        _logger.LogInformation("Loanword rejected, rejects {Rejects}, learned {Learned}", rejects, rejects > 0);
    }

    /// <summary>Cases the target like the typed word: all caps, capitalised, or as stored.</summary>
    public static string MatchCase(string typed, string target)
    {
        var letters = typed.Where(char.IsLetter).ToList();
        if (target.Length == 0 || letters.Count == 0)
        {
            return target;
        }

        if (letters.Count >= 2 && letters.All(char.IsUpper))
        {
            return target.ToUpperInvariant();
        }

        return char.IsUpper(letters[0]) ? char.ToUpperInvariant(target[0]) + target[1..] : target;
    }

    /// <summary>A name: in the dictionary only with its capital (Ιράκ, Μαρκ), as typed.</summary>
    private bool IsName(string word) =>
        word.Length > 0 && char.IsUpper(word[0]) && !_dictionary.IsWord(word.ToLowerInvariant(), Lang.Greek);

    /// <summary>Ends in a consonant letter other than ς or ν, which native Greek words don't.</summary>
    private static bool HasForeignEnding(string word)
    {
        var last = char.ToLowerInvariant(word.LastOrDefault(char.IsLetter));
        return GreekPhonetics.IsConsonantLetter(last) && last is not ('ς' or 'σ' or 'ν');
    }

    /// <summary>The top match, or the target the user accepted before if it's within the tie margin after its bonus.</summary>
    private static LoanwordMatch? Best(IReadOnlyList<LoanwordMatch> matches, LoanwordPairInfo? pair, LoanwordOptions options)
    {
        if (matches.Count == 0)
        {
            return null;
        }

        var top = matches[0];
        if (pair is { Accepts: > 0 } accepted && matches.FirstOrDefault(m => m.Word == accepted.Target) is { } previous
            && previous.Score + options.AcceptedBonus >= top.Score - options.TieMargin)
        {
            return previous with { Score = Math.Max(previous.Score + options.AcceptedBonus, top.Score) };
        }

        return top;
    }

    /// <summary>Scores are logged rounded down to a tenth, never with the words.</summary>
    private static string Bucket(double score) =>
        (Math.Floor(score * 10) / 10).ToString("0.0", CultureInfo.InvariantCulture);
}
