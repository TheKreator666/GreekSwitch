namespace GreekSwitch.Core.Scoring;

/// <summary>Remembers the language of the last few accepted words (languages only, never text).</summary>
public sealed class ContextTracker
{
    private readonly Queue<Lang> _recent = new();

    public void Record(Lang lang, int window)
    {
        _recent.Enqueue(lang);
        while (_recent.Count > window)
        {
            _recent.Dequeue();
        }
    }

    public void Clear() => _recent.Clear();

    /// <summary>Share of recent words in <paramref name="lang"/>, from 0 to 1 (0 with no history).</summary>
    public double Share(Lang lang) => _recent.Count == 0 ? 0 : (double)_recent.Count(l => l == lang) / _recent.Count;

    /// <summary>The language of most recent words, or null with no history or a tie.</summary>
    public Lang? Majority
    {
        get
        {
            var greek = Share(Lang.Greek);
            return _recent.Count == 0 || greek == 0.5 ? null : greek > 0.5 ? Lang.Greek : Lang.English;
        }
    }
}
