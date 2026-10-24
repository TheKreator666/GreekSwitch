using GreekSwitch.Core.Decisions;
using GreekSwitch.Core.Text;

namespace GreekSwitch.Core.Loanwords;

/// <summary>A pair the user typed into Settings; kept as text so it can be edited.</summary>
public sealed record LoanwordPair(string Greek, string Target);

/// <summary>
/// A pair learned from use, for saving: the Greek word only as a keyed hash, the English target
/// (a dictionary word) as text, and how often the suggestion was accepted and rejected.
/// </summary>
public sealed record LearnedLoanword(string Hash, string Target, int Accepts, int Rejects);

/// <summary>What the store knows about one Greek word.</summary>
public readonly record struct LoanwordPairInfo(string Target, bool Explicit, int Accepts, int Rejects);

/// <summary>
/// Thread-safe store of loanword pairs, shaped like <see cref="NeverConvertList"/> so the app
/// can save it in settings.json. Pairs the user enters in Settings are kept as text. Pairs
/// learned from accepted or rejected suggestions are keyed by an HMAC of the normalised Greek
/// word (domain-separated from the never-convert hashes), because the Greek word is whatever the
/// user typed; only the English target is stored as text, and it must be Latin script.
/// </summary>
public sealed class LoanwordPairStore
{
    /// <summary>Learned targets longer than this, or not in Latin script, are refused.</summary>
    public const int MaxTargetLength = 40;

    private const string HashDomain = "loanword\u001f";

    private readonly object _gate = new();
    private readonly Dictionary<string, LoanwordPair> _pairs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, LearnedLoanword> _learned = new(StringComparer.Ordinal);
    private readonly IWordHasher _hasher;

    public LoanwordPairStore(IEnumerable<LoanwordPair>? pairs = null, IEnumerable<LearnedLoanword>? learned = null, IWordHasher? hasher = null)
    {
        _hasher = hasher ?? HmacWordHasher.Ephemeral();
        foreach (var pair in pairs ?? [])
        {
            AddCore(pair.Greek, pair.Target);
        }

        foreach (var entry in learned ?? [])
        {
            if (entry.Hash.Length > 0 && IsValidLearnedTarget(entry.Target))
            {
                _learned[entry.Hash] = entry with { Accepts = Math.Max(0, entry.Accepts), Rejects = Math.Max(0, entry.Rejects) };
            }
        }
    }

    /// <summary>Raised after a pair is added, removed, accepted or rejected; handlers run on that thread.</summary>
    public event EventHandler? Changed;

    public int LearnedCount
    {
        get
        {
            lock (_gate)
            {
                return _learned.Count;
            }
        }
    }

    /// <summary>The pair for this Greek word: a Settings pair first, else a learned one.</summary>
    public LoanwordPairInfo? Find(string greekWord)
    {
        var key = Key(greekWord);
        if (key.Length == 0)
        {
            return null;
        }

        var hash = Hash(key);
        lock (_gate)
        {
            if (_pairs.TryGetValue(key, out var pair))
            {
                return new LoanwordPairInfo(pair.Target, Explicit: true, Accepts: 0, Rejects: 0);
            }

            return _learned.TryGetValue(hash, out var learned)
                ? new LoanwordPairInfo(learned.Target, Explicit: false, learned.Accepts, learned.Rejects)
                : null;
        }
    }

    /// <summary>A pair the user entered explicitly (always replaced, tier 1).</summary>
    public void Add(string greekWord, string target)
    {
        bool added;
        lock (_gate)
        {
            added = AddCore(greekWord, target);
        }

        if (added)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool Remove(string greekWord)
    {
        bool removed;
        lock (_gate)
        {
            removed = _pairs.Remove(Key(greekWord));
        }

        if (removed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return removed;
    }

    /// <summary>
    /// The user accepted <paramref name="target"/> for this word. Returns the pair's accept count
    /// (1 after a different target was accepted before), or 0 if the target was refused.
    /// </summary>
    public int RecordAccept(string greekWord, string target) =>
        Update(greekWord, target, (old, t) => old is not null && old.Target == t
            ? old with { Accepts = old.Accepts + 1 }
            : new LearnedLoanword("", t, Accepts: 1, Rejects: 0))?.Accepts ?? 0;

    /// <summary>
    /// The user rejected <paramref name="target"/> for this word (undid it, or dismissed it
    /// explicitly). Resets the accept count, so a promoted pair is demoted, and counts the rejection.
    /// Returns the pair's reject count, or 0 if the target was refused.
    /// </summary>
    public int RecordReject(string greekWord, string target) =>
        Update(greekWord, target, (old, t) => old is not null && old.Target == t
            ? old with { Accepts = 0, Rejects = old.Rejects + 1 }
            : new LearnedLoanword("", t, Accepts: 0, Rejects: 1))?.Rejects ?? 0;

    /// <summary>Replaces the user-entered pairs (edited in Settings). Does not raise <see cref="Changed"/>.</summary>
    public void ReplaceAll(IEnumerable<LoanwordPair> pairs)
    {
        lock (_gate)
        {
            _pairs.Clear();
            foreach (var pair in pairs)
            {
                AddCore(pair.Greek, pair.Target);
            }
        }
    }

    /// <summary>Forgets every learned pair. Does not raise <see cref="Changed"/>.</summary>
    public void ForgetLearned()
    {
        lock (_gate)
        {
            _learned.Clear();
        }
    }

    /// <summary>The user-entered pairs as entered, sorted by the Greek word.</summary>
    public IReadOnlyList<LoanwordPair> Snapshot()
    {
        lock (_gate)
        {
            return [.. _pairs.Values.OrderBy(p => p.Greek, StringComparer.CurrentCultureIgnoreCase)];
        }
    }

    /// <summary>The learned pairs (hash, target, counts), for saving.</summary>
    public IReadOnlyList<LearnedLoanword> LearnedSnapshot()
    {
        lock (_gate)
        {
            return [.. _learned.Values.OrderBy(l => l.Hash, StringComparer.Ordinal)];
        }
    }

    /// <summary>Learned targets come from the English index: Latin letters, apostrophe or hyphen, short.</summary>
    public static bool IsValidLearnedTarget(string target) =>
        target.Length is > 0 and <= MaxTargetLength && target.All(c => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '\'' or '-');

    private static string Key(string word) => GreekText.NormalizeForMatch(word.Trim());

    private string Hash(string key) => _hasher.Hash(HashDomain + key);

    private LearnedLoanword? Update(string greekWord, string target, Func<LearnedLoanword?, string, LearnedLoanword> change)
    {
        var key = Key(greekWord);
        var normalizedTarget = target.Trim().ToLowerInvariant();
        if (key.Length == 0 || !IsValidLearnedTarget(normalizedTarget))
        {
            return null;
        }

        var hash = Hash(key);
        LearnedLoanword updated;
        lock (_gate)
        {
            updated = change(_learned.GetValueOrDefault(hash), normalizedTarget) with { Hash = hash };
            _learned[hash] = updated;
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return updated;
    }

    private bool AddCore(string greekWord, string target)
    {
        var key = Key(greekWord);
        var trimmedTarget = target.Trim();
        if (key.Length == 0 || trimmedTarget.Length == 0)
        {
            return false;
        }

        var pair = new LoanwordPair(greekWord.Trim(), trimmedTarget);
        if (_pairs.TryGetValue(key, out var existing) && existing == pair)
        {
            return false;
        }

        _pairs[key] = pair;
        return true;
    }
}
