namespace GreekSwitch.Windows.Hooks;

/// <summary>
/// Decides whether typing keys were missed in the focused field while capture was off, using the
/// hook's running count of dropped keys. Keys dropped before the last focus change went to another
/// field (e.g. a password box) and don't make the next word in the new field incomplete.
/// </summary>
public sealed class MissedKeys
{
    private long _baseline;

    /// <summary>Capture went off with this many keys dropped so far.</summary>
    public void CaptureStopped(long dropped) => _baseline = dropped;

    /// <summary>Focus moved; <paramref name="droppedAtFocus"/> was read on the hook thread when it did.</summary>
    public void FocusChanged(long droppedAtFocus) => _baseline = droppedAtFocus;

    /// <summary>Capture is resuming: were keys dropped in this field since capture stopped?</summary>
    public bool MissedOnResume(long dropped) => dropped != _baseline;
}
