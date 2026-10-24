namespace GreekSwitch.Windows.Hooks;

/// <summary>
/// The single flag behind the hook's one exception to "never swallow a key": while a loanword
/// suggestion is visible, Tab with no modifiers accepts it instead of reaching the app. Lock-free:
/// the hook only reads and clears it.
/// </summary>
public sealed class SuggestionGate
{
    private int _visible;

    public bool Visible => Volatile.Read(ref _visible) != 0;

    /// <summary>A suggestion is on screen (set after the popup is shown).</summary>
    public void Open() => Volatile.Write(ref _visible, 1);

    /// <summary>The suggestion is gone (timeout, focus change, any key).</summary>
    public void Close() => Volatile.Write(ref _visible, 0);

    /// <summary>Hook: true exactly once per visible suggestion, for the key that accepts it.</summary>
    internal bool TryTake() => Interlocked.Exchange(ref _visible, 0) != 0;
}
