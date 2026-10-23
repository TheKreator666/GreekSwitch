namespace GreekSwitch.Windows.Output;

/// <summary>
/// The control that had keyboard focus when the key being processed was pressed. The corrector
/// only types into that control: if focus has moved since (a popup, another window), its
/// Backspaces and text would land in the wrong place.
/// </summary>
public sealed class FocusGuard
{
    private long _expected;

    /// <summary>Focused window at the time of the key being processed, or 0 if unknown.</summary>
    public nint Expected => (nint)Interlocked.Read(ref _expected);

    /// <summary>Worker: the key it is about to process was pressed with <paramref name="focusWindow"/> focused.</summary>
    public void Expect(nint focusWindow) => Interlocked.Exchange(ref _expected, focusWindow);

    /// <summary>True if <paramref name="current"/> is where the key went (or nothing was recorded).</summary>
    public bool StillFocused(nint current) => Expected == 0 || Expected == current;
}
