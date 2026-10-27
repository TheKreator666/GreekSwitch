using System.Collections.Concurrent;
using GreekSwitch.Windows.Native;
using static GreekSwitch.Windows.Native.NativeMethods;

namespace GreekSwitch.Windows.Output;

/// <summary>A key event the hook held back while a correction was being typed.</summary>
public readonly record struct HeldKey(ushort VirtualKey, ushort ScanCode, bool Extended, bool Up);

/// <summary>
/// Keeps the user's keys out of a correction while it is being typed. With a low-level keyboard
/// hook installed (ours), SendInput is no longer atomic: a key pressed while the batch is being
/// delivered lands in the middle of it ("δουλεκύει"), and next to the tonos dead key it can eat
/// letters. So for the few milliseconds of the send the hook queues every key event that isn't
/// ours and swallows it, and the corrector replays them, in order, right after its batch.
/// <para>
/// The hook side never waits or locks: it reads two fields and enqueues. Holding stops by itself
/// after the cap, even if the corrector never ends it, so the keyboard can't get stuck.
/// </para>
/// </summary>
public sealed class KeyHold
{
    private readonly ConcurrentQueue<HeldKey> _held = new();
    private long _until; // Environment.TickCount64 deadline; 0 = not holding
    private int _inHook;

    /// <summary>Hard cap on one hold, whatever happens to the corrector.</summary>
    public static readonly TimeSpan Cap = TimeSpan.FromMilliseconds(200);

    /// <summary>Corrector: start holding the user's keys (until <see cref="End"/> or the cap).</summary>
    public void Begin() => Interlocked.Exchange(ref _until, Environment.TickCount64 + (long)Cap.TotalMilliseconds);

    /// <summary>
    /// Hook thread: if a hold is on, queue this key event and return true (the hook then swallows
    /// it). Returns false when not holding: the event goes through as usual.
    /// </summary>
    public bool TryHold(HeldKey key)
    {
        Interlocked.Increment(ref _inHook);
        try
        {
            var until = Interlocked.Read(ref _until);
            if (until == 0 || Environment.TickCount64 >= until)
            {
                return false;
            }

            _held.Enqueue(key);
            return true;
        }
        finally
        {
            Interlocked.Decrement(ref _inHook);
        }
    }

    /// <summary>Corrector: the keys held so far, oldest first (empty if none).</summary>
    public List<HeldKey> Drain()
    {
        var keys = new List<HeldKey>();
        while (_held.TryDequeue(out var key))
        {
            keys.Add(key);
        }

        return keys;
    }

    /// <summary>
    /// Corrector: stop holding. Waits (microseconds) for a hook call that already decided to hold
    /// to finish queueing, so a following <see cref="Drain"/> sees every held key.
    /// </summary>
    public void End()
    {
        Interlocked.Exchange(ref _until, 0);
        var spin = new SpinWait();
        while (Volatile.Read(ref _inHook) != 0)
        {
            spin.SpinOnce();
        }
    }

    /// <summary>SendInput events that replay <paramref name="keys"/> (marked as ours, so the hook ignores them).</summary>
    public static INPUT[] ToInputs(IReadOnlyList<HeldKey> keys) =>
        [.. keys.Select(k => InputBuilder.Raw(k.VirtualKey, k.ScanCode, (k.Up ? KEYEVENTF_KEYUP : 0) | (k.Extended ? KEYEVENTF_EXTENDEDKEY : 0)))];
}
