using GreekSwitch.Core.Input;
using static GreekSwitch.Windows.Native.NativeMethods;

namespace GreekSwitch.Windows;

/// <summary>RegisterHotKey for <see cref="KeyChord"/>s (call on the thread that owns the window).</summary>
public static class Hotkeys
{
    public const int WmHotkey = 0x0312;

    private const uint ModAlt = 0x1;
    private const uint ModControl = 0x2;
    private const uint ModShift = 0x4;
    private const uint ModWin = 0x8;
    private const uint ModNoRepeat = 0x4000;

    /// <summary>The RegisterHotKey modifier flags for a chord (no auto-repeat).</summary>
    public static uint Modifiers(KeyChord chord) =>
        (chord.Alt ? ModAlt : 0) | (chord.Control ? ModControl : 0) | (chord.Shift ? ModShift : 0) | (chord.Win ? ModWin : 0) | ModNoRepeat;

    /// <summary>False if another program already owns the chord.</summary>
    public static bool Register(nint window, int id, KeyChord chord) =>
        RegisterHotKey(window, id, Modifiers(chord), (uint)chord.VirtualKey);

    public static void Unregister(nint window, int id) => UnregisterHotKey(window, id);
}
