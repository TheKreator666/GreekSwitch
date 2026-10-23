using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using GreekSwitch.Windows.Native;
using static GreekSwitch.Windows.Native.NativeMethods;

namespace GreekSwitch.Windows.Hooks;

/// <summary>WH_MOUSE_LL hook: any button press may move the caret, so it resets the word buffer.</summary>
internal sealed class MouseHook : IDisposable
{
    private readonly ChannelWriter<InputEvent> _writer;
    private readonly HookProc _proc;
    private nint _hook;

    public MouseHook(ChannelWriter<InputEvent> writer)
    {
        _writer = writer;
        _proc = Callback;
    }

    public void Install()
    {
        _hook = SetWindowsHookExW(WH_MOUSE_LL, Marshal.GetFunctionPointerForDelegate(_proc), GetModuleHandleW(null), 0);
        if (_hook == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "SetWindowsHookEx(WH_MOUSE_LL) failed");
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

    private nint Callback(int nCode, nint wParam, nint lParam)
    {
        if (nCode == HC_ACTION && wParam is WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_MBUTTONDOWN or WM_XBUTTONDOWN)
        {
            _writer.TryWrite(InputEvent.Click());
        }

        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }
}
