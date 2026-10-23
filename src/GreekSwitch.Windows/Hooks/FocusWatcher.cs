using System.Runtime.InteropServices;
using System.Threading.Channels;
using GreekSwitch.Windows.Native;
using static GreekSwitch.Windows.Native.NativeMethods;

namespace GreekSwitch.Windows.Hooks;

/// <summary>
/// SetWinEventHook for EVENT_SYSTEM_FOREGROUND (another window came to the front) and
/// EVENT_OBJECT_FOCUS (focus moved inside a window, e.g. into a password box). Both reset the
/// buffer; the worker resolves the process name and password state.
/// </summary>
internal sealed class FocusWatcher : IDisposable
{
    private readonly ChannelWriter<InputEvent> _writer;
    private readonly WinEventProc _proc;
    private nint _foregroundHook;
    private nint _focusHook;

    public FocusWatcher(ChannelWriter<InputEvent> writer)
    {
        _writer = writer;
        _proc = Callback;
    }

    public void Install()
    {
        var proc = Marshal.GetFunctionPointerForDelegate(_proc);
        const uint flags = WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS;
        _foregroundHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, 0, proc, 0, 0, flags);
        _focusHook = SetWinEventHook(EVENT_OBJECT_FOCUS, EVENT_OBJECT_FOCUS, 0, proc, 0, 0, flags);
    }

    public void Dispose()
    {
        if (_foregroundHook != 0)
        {
            UnhookWinEvent(_foregroundHook);
            _foregroundHook = 0;
        }

        if (_focusHook != 0)
        {
            UnhookWinEvent(_focusHook);
            _focusHook = 0;
        }
    }

    private void Callback(nint hook, uint eventType, nint hwnd, int idObject, int idChild, uint thread, uint time)
    {
        _writer.TryWrite(eventType == EVENT_SYSTEM_FOREGROUND ? InputEvent.Foreground(hwnd) : InputEvent.Focus(hwnd));
    }
}
