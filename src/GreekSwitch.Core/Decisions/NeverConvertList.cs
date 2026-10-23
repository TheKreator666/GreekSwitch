using GreekSwitch.Core.Text;

namespace GreekSwitch.Core.Decisions;

/// <summary>
/// Thread-safe in-memory never-convert list. The worker adds to it on undo, and the app reads a
/// snapshot to save to settings.json (the only place words are stored, because the user put them there).
/// </summary>
public sealed class NeverConvertList : INeverConvertList
{
    private readonly object _gate = new();
    private readonly Dictionary<string, string> _words = new(StringComparer.Ordinal);

    public NeverConvertList(IEnumerable<string>? words = null)
    {
        foreach (var word in words ?? [])
        {
            AddCore(word);
        }
    }

    /// <summary>Raised after a word is added; handlers run on the adding thread.</summary>
    public event EventHandler? Changed;

    public bool Contains(string word)
    {
        lock (_gate)
        {
            return _words.ContainsKey(Key(word));
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

    /// <summary>The words as first added, sorted.</summary>
    public IReadOnlyList<string> Snapshot()
    {
        lock (_gate)
        {
            return [.. _words.Values.Order(StringComparer.CurrentCultureIgnoreCase)];
        }
    }

    private static string Key(string word) => GreekText.NormalizeForMatch(word.Trim());

    private bool AddCore(string word)
    {
        var key = Key(word);
        return key.Length > 0 && _words.TryAdd(key, word.Trim());
    }
}
