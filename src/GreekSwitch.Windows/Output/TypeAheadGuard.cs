namespace GreekSwitch.Windows.Output;

/// <summary>
/// Tells the corrector whether the user has typed past the word being corrected. If they have,
/// those keys reach the app before our Backspaces, and the Backspaces would delete the wrong
/// characters. The hook numbers every captured typing key (modifiers type nothing and don't
/// count); each key event carries the number in force when it was captured, and the worker
/// records the number of the event it is processing.
/// </summary>
public sealed class TypeAheadGuard
{
    private long _captured;
    private long _processed;

    /// <summary>Number of typing keys captured so far.</summary>
    public long Captured => Interlocked.Read(ref _captured);

    /// <summary>True if a typing key was captured after the event the worker is processing.</summary>
    public bool UserTypedAhead => Interlocked.Read(ref _captured) > Interlocked.Read(ref _processed);

    /// <summary>Hook thread: a key was captured. Returns the typing-key count to attach to its event.</summary>
    public long OnKeyCaptured(bool isTypingKey) =>
        isTypingKey ? Interlocked.Increment(ref _captured) : Interlocked.Read(ref _captured);

    /// <summary>Worker: the event being processed was captured when the count was <paramref name="count"/>.</summary>
    public void MarkProcessed(long count) => Interlocked.Exchange(ref _processed, count);
}
