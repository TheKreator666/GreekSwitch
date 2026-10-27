using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using GreekSwitch.Core.Input;
using GreekSwitch.Core.Layout;
using GreekSwitch.Windows.Native;
using GreekSwitch.Windows.Output;
using static GreekSwitch.Windows.Native.NativeMethods;

namespace GreekSwitch.Windows.Hooks;

/// <summary>
/// WH_KEYBOARD_LL hook. The callback only reads the key and the modifier state and enqueues
/// them: Windows silently removes low-level hooks that are slow to return. Injected events,
/// including our own SendInput output, are ignored. Install it on a thread that runs a message loop.
/// It never swallows a key, with two exceptions: Tab (no modifiers) while a loanword suggestion is
/// visible, decided by reading one flag (<see cref="SuggestionGate"/>); and keys pressed during
/// the few milliseconds a correction is being typed, which are held and replayed right after it
/// (<see cref="KeyHold"/>, at most 200 ms).
/// </summary>
internal sealed class KeyboardHook : IDisposable
{
    private readonly ChannelWriter<InputEvent> _writer;
    private readonly Func<bool> _captureKeys;
    private readonly TypeAheadGuard _typeAhead;
    private readonly bool _acceptForeignInjected;
    private readonly Action _keyDropped;
    private readonly Health.HookActivity? _activity;
    private readonly HookProc _proc; // kept in a field so the GC can't collect the callback
    private nint _hook;
    private readonly LiveKeyboardState _keyboard;
    private readonly SuggestionGate _suggestion;
    private readonly KeyHold _hold;
    private bool _capsLock;
    private bool _swallowTabUp;

    public KeyboardHook(
        ChannelWriter<InputEvent> writer,
        Func<bool> captureKeys,
        TypeAheadGuard typeAhead,
        LiveKeyboardState keyboard,
        bool acceptForeignInjected = false,
        Action? keyDropped = null,
        Health.HookActivity? activity = null,
        SuggestionGate? suggestion = null,
        KeyHold? hold = null)
    {
        _suggestion = suggestion ?? new SuggestionGate();
        _hold = hold ?? new KeyHold();
        _writer = writer;
        _captureKeys = captureKeys;
        _typeAhead = typeAhead;
        _keyboard = keyboard;
        _acceptForeignInjected = acceptForeignInjected;
        _keyDropped = keyDropped ?? (() => { });
        _activity = activity;
        _proc = Callback;
    }

    public void Install()
    {
        // Seed the Caps Lock toggle state; afterwards it is tracked from the key events.
        _capsLock = (GetKeyState(VirtualKeys.Capital) & 1) != 0;
        _keyboard.SetCapsLock(_capsLock);
        _hook = SetWindowsHookExW(WH_KEYBOARD_LL, Marshal.GetFunctionPointerForDelegate(_proc), GetModuleHandleW(null), 0);
        if (_hook == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "SetWindowsHookEx(WH_KEYBOARD_LL) failed");
        }
    }

    public void Dispose()
    {
        if (_hook != 0)
        {
            UnhookWindowsHookEx(_hook);
            _hook = 0;
        }
    }

    private unsafe nint Callback(int nCode, nint wParam, nint lParam)
    {
        _activity?.Touch(); // any event, injected or not: proof the hook is alive
        try
        {
            if (nCode == HC_ACTION && wParam is WM_KEYDOWN or WM_SYSKEYDOWN)
            {
                var data = *(KBDLLHOOKSTRUCT*)lParam;
                var vk = (int)data.vkCode;

                // Caps Lock really toggles whoever presses it (including our own fix for an
                // accidental Caps Lock), so track every press.
                if (vk == VirtualKeys.Capital)
                {
                    _capsLock = !_capsLock;
                    _keyboard.SetCapsLock(_capsLock);
                }

                // Never our own output; other injected input only in the opt-in Debug test mode.
                var injected = (data.flags & LLKHF_INJECTED) != 0;

                // Tab still held after accepting a suggestion: its auto-repeat belongs to the swallowed press.
                if (_swallowTabUp && vk == VirtualKeys.Tab && !injected)
                {
                    return 1;
                }

                if (!injected || (_acceptForeignInjected && data.dwExtraInfo != InputBuilder.Signature))
                {

                    if (_captureKeys())
                    {
                        // Any key closes a visible suggestion; only a plain Tab accepts it, and is swallowed.
                        if (_suggestion.Visible && _suggestion.TryTake() && vk == VirtualKeys.Tab
                            && !IsDown(VirtualKeys.Shift) && !IsDown(VirtualKeys.Control) && (data.flags & LLKHF_ALTDOWN) == 0
                            && !IsDown(VirtualKeys.LWin) && !IsDown(VirtualKeys.RWin))
                        {
                            _swallowTabUp = true; // the app must not see a key-up without its key-down
                            _writer.TryWrite(InputEvent.AcceptSuggestion());
                            return 1;
                        }

                        var typedCount = _typeAhead.OnKeyCaptured(isTypingKey: !VirtualKeys.IsModifier(vk));
                        var (layout, focus) = Layouts.KeyboardLayouts.ForegroundLayout();
                        _writer.TryWrite(InputEvent.ForKey(
                            new KeyInput(
                                vk,
                                Shift: IsDown(VirtualKeys.Shift),
                                CapsLock: _capsLock,
                                Control: IsDown(VirtualKeys.Control),
                                Alt: (data.flags & LLKHF_ALTDOWN) != 0 || IsDown(VirtualKeys.Menu),
                                Win: IsDown(VirtualKeys.LWin) || IsDown(VirtualKeys.RWin)),
                            typedCount,
                            focus,
                            layout));
                    }
                    else if (!VirtualKeys.IsModifier(vk))
                    {
                        _keyDropped(); // only counted, never queued: the word in progress is now incomplete
                    }
                }
            }
            else if (_swallowTabUp && nCode == HC_ACTION && wParam is WM_KEYUP or WM_SYSKEYUP
                && ((KBDLLHOOKSTRUCT*)lParam)->vkCode == VirtualKeys.Tab
                && (((KBDLLHOOKSTRUCT*)lParam)->flags & LLKHF_INJECTED) == 0) // the user's key-up, not ours
            {
                _swallowTabUp = false;
                return 1;
            }

            // A correction is being typed: hold back every key event that isn't ours (it has
            // already been queued for the worker above), so it can't land inside the correction.
            // The corrector replays it right after. Ends by itself after KeyHold.Cap.
            if (nCode == HC_ACTION && wParam is WM_KEYDOWN or WM_SYSKEYDOWN or WM_KEYUP or WM_SYSKEYUP)
            {
                var data = *(KBDLLHOOKSTRUCT*)lParam;
                if (data.dwExtraInfo != InputBuilder.Signature
                    && _hold.TryHold(new HeldKey(
                        (ushort)data.vkCode,
                        (ushort)data.scanCode,
                        Extended: (data.flags & LLKHF_EXTENDED) != 0,
                        Up: wParam is WM_KEYUP or WM_SYSKEYUP)))
                {
                    return 1;
                }
            }
        }
#pragma warning disable CA1031 // An exception must never cross back into user32; report it to the worker instead.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _writer.TryWrite(InputEvent.Error(ex));
        }

        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }
}
