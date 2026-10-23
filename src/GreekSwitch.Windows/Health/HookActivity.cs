namespace GreekSwitch.Windows.Health;

/// <summary>When a hook last saw any event (GetTickCount clock). Written by the hook thread, read by the watchdog.</summary>
public sealed class HookActivity
{
    private int _tick = Environment.TickCount;

    public uint LastTick => unchecked((uint)Volatile.Read(ref _tick));

    public void Touch() => Volatile.Write(ref _tick, Environment.TickCount);
}
