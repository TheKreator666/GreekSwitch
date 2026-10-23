using GreekSwitch.Core.Text;

namespace GreekSwitch.Core.Decisions;

/// <summary>
/// Thread-safe never-convert list. Words the user types into Settings are kept as text so they
/// can be edited. Words learned from undo are kept only as keyed hashes (see
/// <see cref="INeverConvertList.Learn"/>). The app saves both to settings.json.
/// </summary>
public sealed class NeverConvertList : INeverConvertList
{
    private readonly object _gate = new();
    private readonly Dictionary<string, string> _words = new(StringComparer.Ordinal);
    private readonly HashSet<string> _learned = new(StringComparer.Ordinal);
    private readonly IWordHasher _hasher;

    public NeverConvertList(IEnumerable<string>? words = null, IEnumerable<string>? learned = null, IWordHasher? hasher = null)
    {
        _hasher = hasher ?? HmacWordHasher.Ephemeral();
        foreach (var word in words ?? [])
        {
            AddCore(word);
        }

        _learned.UnionWith(learned ?? []);
    }

    /// <summary>Raised after a word is added or learned; handlers run on that thread.</summary>
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

    public bool Contains(string word)
    {
        var key = Key(word);
        if (key.Length == 0)
        {
            return false;
        }

        var hash = _hasher.Hash(key);
        lock (_gate)
        {
            return _words.ContainsKey(key) || _learned.Contains(hash);
        }
    }

    public void Add(string word)
    {
        bool added;
        lock (_gate)
        {
            added = AddCore(word);
        }

        if (added)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Learn(string word)
    {
        var key = Key(word);
        if (key.Length == 0)
        {
            return;
        }

        var hash = _hasher.Hash(key);
        bool added;
        lock (_gate)
        {
            added = _learned.Add(hash);
        }

        if (added)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Forget(string word)
    {
        var key = Key(word);
        if (key.Length == 0)
        {
            return;
        }

        var hash = _hasher.Hash(key);
        bool removed;
        lock (_gate)
        {
            removed = _learned.Remove(hash);
        }

        if (removed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Replaces the user-entered words (edited in Settings). Does not raise <see cref="Changed"/>.</summary>
    public void ReplaceAll(IEnumerable<string> words)
    {
        lock (_gate)
        {
            _words.Clear();
            foreach (var word in words)
            {
                AddCore(word);
            }
        }
    }

    /// <summary>Forgets every word learned from undo. Does not raise <see cref="Changed"/>.</summary>
    public void ForgetLearned()
    {
        lock (_gate)
        {
            _learned.Clear();
        }
    }

    /// <summary>The user-entered words as first added, sorted.</summary>
    public IReadOnlyList<string> Snapshot()
    {
        lock (_gate)
        {
            return [.. _words.Values.Order(StringComparer.CurrentCultureIgnoreCase)];
        }
    }

    /// <summary>The learned words' hashes, for saving.</summary>
    public IReadOnlyList<string> LearnedSnapshot()
    {
        lock (_gate)
        {
            return [.. _learned.Order(StringComparer.Ordinal)];
        }
    }

    private static string Key(string word) => GreekText.NormalizeForMatch(word.Trim());

    private bool AddCore(string word)
    {
        var key = Key(word);
        return key.Length > 0 && _words.TryAdd(key, word.Trim());
    }
}
