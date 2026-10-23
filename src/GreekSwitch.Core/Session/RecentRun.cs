namespace GreekSwitch.Core.Session;

/// <summary>A completed word of the current run: its keys, the layout it was typed in, and the language it now shows.</summary>
internal sealed record RunWord(KeyStroke[] Keys, Lang TypedLayout, Lang Shown);

/// <summary>
/// The last few completed words of the current run: the same field, no reset, separated by
/// single spaces. Kept so a confident correction can fix the words before it (milestone 11).
/// Keys only, never text; wiped (not just dropped) when words leave or the run ends.
/// </summary>
internal sealed class RecentRun
{
    public const int MaxWords = 3;

    private readonly List<RunWord> _words = [];

    public IReadOnlyList<RunWord> Words => _words;

    public void Add(IReadOnlyList<KeyStroke> keys, Lang typedLayout, Lang shown)
    {
        if (_words.Count == MaxWords)
        {
            Array.Clear(_words[0].Keys);
            _words.RemoveAt(0);
        }

        _words.Add(new RunWord([.. keys], typedLayout, shown));
    }

    /// <summary>The word at <paramref name="index"/> now shows <paramref name="shown"/> (it was converted).</summary>
    public void MarkShown(int index, Lang shown) => _words[index] = _words[index] with { Shown = shown };

    public void Clear()
    {
        foreach (var word in _words)
        {
            Array.Clear(word.Keys);
        }

        _words.Clear();
    }
}
