namespace GreekSwitch.Core.Input;

/// <summary>A finished word: its keys, the layout active when it was typed, and what ended it.</summary>
/// <param name="Keys">The physical keys of the word.</param>
/// <param name="Layout">Layout active at the first key.</param>
/// <param name="Delimiter">The key that ended the word.</param>
/// <param name="FollowsPeriod">The word came straight after "word." with no space (a domain or file name).</param>
/// <param name="Tainted">Backspace went past the start of the buffer, so the keys are only part of the word.</param>
/// <param name="MixedLayout">The active layout changed partway through the word (the user switched it).</param>
public sealed record CompletedWord(
    IReadOnlyList<KeyStroke> Keys,
    Lang Layout,
    Delimiter Delimiter,
    bool FollowsPeriod,
    bool Tainted,
    bool MixedLayout = false);

/// <summary>
/// The physical keys of the word being typed. Memory only, and wiped on every boundary and reset.
/// It keeps its own array because List&lt;T&gt;.Clear() leaves plain structs such as KeyStroke in its
/// backing store, where they would linger (e.g. in a crash dump).
/// </summary>
public sealed class WordBuffer
{
    private KeyStroke[] _keys = new KeyStroke[32];
    private int _count;
    private Lang _layout;
    private bool _tainted;
    private bool _followsPeriod;
    private bool _mixedLayout;

    public IReadOnlyList<KeyStroke> Keys => new ArraySegment<KeyStroke>(_keys, 0, _count);

    public int Count => _count;

    public bool IsEmpty => _count == 0;

    public void Append(KeyStroke key, Lang activeLayout)
    {
        if (_count == 0)
        {
            _layout = activeLayout;
        }
        else if (activeLayout != _layout)
        {
            _mixedLayout = true;
        }

        if (_count == _keys.Length)
        {
            var larger = new KeyStroke[_keys.Length * 2];
            _keys.CopyTo(larger, 0);
            Array.Clear(_keys);
            _keys = larger;
        }

        _keys[_count++] = key;
    }

    /// <summary>
    /// Removes the last key. Returns false when the buffer was already empty: that Backspace
    /// deleted text we never saw, so the next word is marked tainted.
    /// </summary>
    public bool Pop()
    {
        if (_count == 0)
        {
            _tainted = true;
            _followsPeriod = false;
            return false;
        }

        _keys[--_count] = default;
        return true;
    }

    /// <summary>Ends the current word at <paramref name="delimiter"/> and starts a new one.</summary>
    public CompletedWord? Complete(Delimiter delimiter)
    {
        CompletedWord? word = _count == 0
            ? null
            : new CompletedWord(_keys[.._count], _layout, delimiter, _followsPeriod, _tainted, _mixedLayout);

        Clear();
        _followsPeriod = word is not null && delimiter is { Kind: DelimiterKind.Punctuation, Text: "." };
        return word;
    }

    /// <summary>Puts a word's keys back, e.g. after undoing a replacement, so typing can continue it.</summary>
    public void Restore(IReadOnlyList<KeyStroke> keys, Lang layout)
    {
        Clear();
        foreach (var key in keys)
        {
            Append(key, layout);
        }

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
        Array.Clear(_keys); // the whole array, including slots freed by Pop
        _count = 0;
        _tainted = false;
        _mixedLayout = false;
    }
}
