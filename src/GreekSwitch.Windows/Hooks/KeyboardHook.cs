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
    private bool _capsLock;

    public KeyboardHook(
        ChannelWriter<InputEvent> writer,
        Func<bool> captureKeys,
        TypeAheadGuard typeAhead,
        LiveKeyboardState keyboard,
        bool acceptForeignInjected = false,
        Action? keyDropped = null,
        Health.HookActivity? activity = null)
    {
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
                // Never our own output; other injected input only in the opt-in Debug test mode.
                var injected = (data.flags & LLKHF_INJECTED) != 0;
                if (!injected || (_acceptForeignInjected && data.dwExtraInfo != InputBuilder.Signature))
                {
                    var vk = (int)data.vkCode;
                    if (vk == VirtualKeys.Capital)
                    {
                        _capsLock = !_capsLock;
                        _keyboard.SetCapsLock(_capsLock);
                    }

                    if (_captureKeys())
                    {
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
