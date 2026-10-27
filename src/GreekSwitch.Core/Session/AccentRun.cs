namespace GreekSwitch.Core.Session;

/// <summary>
/// The text typed since the last pause, piece by piece as it is on screen, next to how it
/// should read with its accents ("ειναι " / "είναι "; English and other unchanged text as is).
/// When the user pauses, the session fixes the letters that change in place, in one correction,
/// so the accents never race the user's typing. At most <see cref="MaxPieces"/> pieces (the oldest drop off,
/// unaccented); cleared whenever the screen may no longer match it.
/// </summary>
internal sealed class AccentRun
{
    public const int MaxPieces = 16;

    private readonly List<(string Shown, string Accented)> _pieces = [];

    /// <summary>Some piece would change.</summary>
    public bool HasChanges => _pieces.Exists(p => p.Shown != p.Accented);

    /// <summary>A word and its delimiter as on screen, and as it should read.</summary>
    public void Add(string shown, string accented)
    {
        _pieces.Add((shown, accented));
        if (_pieces.Count > MaxPieces)
        {
            _pieces.RemoveAt(0);
        }
    }

    /// <summary>Text that stays as it is (a second delimiter, a word already corrected).</summary>
    public void AddFixed(string text) => Add(text, text);

    public void Clear() => _pieces.Clear();

    /// <summary>
    /// From the first piece that changes to the end: the text on screen and its accented form
    /// (the same length), and how many words change. Null if nothing would change.
    /// </summary>
    public (string Original, string Replacement, int Words)? Span()
    {
        var first = _pieces.FindIndex(p => p.Shown != p.Accented);
        if (first < 0)
        {
            return null;
        }

        var tail = _pieces.Skip(first).ToList();
        return (string.Concat(tail.Select(p => p.Shown)), string.Concat(tail.Select(p => p.Accented)), tail.Count(p => p.Shown != p.Accented));
    }
}
