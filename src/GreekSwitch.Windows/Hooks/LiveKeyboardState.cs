using GreekSwitch.Core.Layout;
using GreekSwitch.Windows.Output;
using static GreekSwitch.Windows.Native.NativeMethods;

namespace GreekSwitch.Windows.Hooks;

/// <summary>
/// Modifier state from GetAsyncKeyState, plus the Caps Lock toggle as tracked by the keyboard
/// hook (a background thread can't reliably read toggle state).
/// </summary>
public sealed class LiveKeyboardState : IKeyboardState
{
    private volatile bool _capsLock;

    public bool CommandModifierHeld =>
        IsDown(VirtualKeys.Control) || IsDown(VirtualKeys.Menu) || IsDown(VirtualKeys.LWin) || IsDown(VirtualKeys.RWin);

    public bool ShiftHeld => IsDown(VirtualKeys.Shift);

    public bool CapsLockOn => _capsLock;

    internal void SetCapsLock(bool on) => _capsLock = on;
}
