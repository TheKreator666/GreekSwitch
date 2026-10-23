namespace GreekSwitch.Core.Decisions;

/// <summary>What is needed to put a replaced word back.</summary>
/// <param name="Original">The text that was on screen before the replacement.</param>
/// <param name="Replacement">The text the replacement typed, without the delimiter.</param>
/// <param name="Keys">The word's keys, restored into the buffer after undo.</param>
/// <param name="OriginalLayout">The layout to switch back to.</param>
public sealed record UndoRecord(string Original, string Replacement, IReadOnlyList<KeyStroke> Keys, Lang OriginalLayout);

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
