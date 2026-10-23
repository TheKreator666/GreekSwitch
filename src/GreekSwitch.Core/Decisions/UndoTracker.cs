namespace GreekSwitch.Core.Decisions;

/// <summary>What is needed to put a replacement (possibly a batch of several words) back.</summary>
/// <param name="Original">The text that was on screen before the replacement, without the delimiter.</param>
/// <param name="Replacement">The text the replacement typed, without the delimiter.</param>
/// <param name="Keys">The last word's keys, restored into the buffer after undo.</param>
/// <param name="OriginalLayout">The layout to switch back to.</param>
/// <param name="Words">
/// Each converted word's original text, learned as never-convert on undo. Null means
/// <see cref="Original"/> is a single word.
/// </param>
public sealed record UndoRecord(
    string Original,
    string Replacement,
    IReadOnlyList<KeyStroke> Keys,
    Lang OriginalLayout,
    IReadOnlyList<string>? Words = null);

/// <summary>
/// Remembers the last replacement until the next key. A Backspace as that next key takes the
/// record (the session then reverts); any other key disarms it.
/// </summary>
public sealed class UndoTracker
{
    private UndoRecord? _armed;

    public bool IsArmed => _armed is not null;

    public void Arm(UndoRecord record) => _armed = record;

    public void Disarm() => _armed = null;

    public bool TryTake(out UndoRecord record)
    {
        record = _armed!;
        _armed = null;
        return record is not null;
    }
}
