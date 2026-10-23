namespace GreekSwitch.Core.Input;

/// <summary>A finished word: its keys, the layout active when it was typed, and what ended it.</summary>
/// <param name="Keys">The physical keys of the word.</param>
/// <param name="Layout">Layout active at the first key.</param>
/// <param name="Delimiter">The key that ended the word.</param>
/// <param name="FollowsPeriod">The word came straight after "word." with no space (a domain or file name).</param>
/// <param name="Tainted">Backspace went past the start of the buffer, so the keys are only part of the word.</param>
public sealed record CompletedWord(
    IReadOnlyList<KeyStroke> Keys,
    Lang Layout,
    Delimiter Delimiter,
    bool FollowsPeriod,
    bool Tainted);

/// <summary>
/// The physical keys of the word being typed. Memory only, and cleared on every boundary
/// and reset.
/// </summary>
public sealed class WordBuffer
{
    private readonly List<KeyStroke> _keys = [];
    private Lang _layout;
    private bool _tainted;
    private bool _followsPeriod;

    public IReadOnlyList<KeyStroke> Keys => _keys;

    public int Count => _keys.Count;

    public bool IsEmpty => _keys.Count == 0;

    public void Append(KeyStroke key, Lang activeLayout)
    {
        if (_keys.Count == 0)
        {
            _layout = activeLayout;
        }

        _keys.Add(key);
    }

    /// <summary>
    /// Removes the last key. Returns false when the buffer was already empty: that Backspace
    /// deleted text we never saw, so the next word is marked tainted.
    /// </summary>
    public bool Pop()
    {
        if (_keys.Count == 0)
        {
            _tainted = true;
            _followsPeriod = false;
            return false;
        }

        _keys.RemoveAt(_keys.Count - 1);
        return true;
    }

    /// <summary>Ends the current word at <paramref name="delimiter"/> and starts a new one.</summary>
    public CompletedWord? Complete(Delimiter delimiter)
    {
        CompletedWord? word = _keys.Count == 0
            ? null
            : new CompletedWord([.. _keys], _layout, delimiter, _followsPeriod, _tainted);

        Clear();
        _followsPeriod = word is not null && delimiter is { Kind: DelimiterKind.Punctuation, Text: "." };
        return word;
    }

    /// <summary>Puts a word's keys back, e.g. after undoing a replacement, so typing can continue it.</summary>
    public void Restore(IReadOnlyList<KeyStroke> keys, Lang layout)
    {
        Clear();
        _keys.AddRange(keys);
        _layout = layout;
    }

    /// <summary>Forget everything: the caret moved, focus changed, or a shortcut ran.</summary>
    public void Reset()
    {
        Clear();
        _followsPeriod = false;
    }

    private void Clear()
    {
        _keys.Clear();
        _tainted = false;
    }
}
