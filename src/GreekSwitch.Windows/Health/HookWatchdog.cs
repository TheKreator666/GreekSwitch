using System.Runtime.InteropServices;
using GreekSwitch.Windows.Native;
using static GreekSwitch.Windows.Native.NativeMethods;

namespace GreekSwitch.Windows.Health;

/// <summary>
/// Windows silently removes a low-level hook that is too slow (or sometimes across sleep), and
/// nothing tells the app. The hooks note the time of every event they see; if Windows reports
/// keyboard or mouse input that neither hook saw, they are presumed dead and reinstalled.
/// </summary>
public static class HookWatchdog
{
    /// <summary>Input this much newer than anything the hooks saw means the hooks are silent.</summary>
    public static readonly TimeSpan Grace = TimeSpan.FromSeconds(2);

    /// <summary>
    /// True if the last input Windows saw (<paramref name="lastInputTick"/>) is more than
    /// <see cref="Grace"/> newer than the last event a hook saw (<paramref name="lastHookTick"/>).
    /// Both are GetTickCount values (milliseconds, wrapping every ~49.7 days).
    /// </summary>
    public static bool HooksLookSilent(uint lastInputTick, uint lastHookTick)
    {
        var ahead = unchecked((int)(lastInputTick - lastHookTick)); // wrap-safe difference
        return ahead > Grace.TotalMilliseconds;
    }

    /// <summary>GetTickCount time of the last keyboard or mouse input in this session.</summary>
    public static uint LastInputTick()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        return GetLastInputInfo(ref info) ? info.dwTime : 0;
    }

    /// <summary>The same clock as GetLastInputInfo.</summary>
    public static uint Now() => unchecked((uint)Environment.TickCount);
}
