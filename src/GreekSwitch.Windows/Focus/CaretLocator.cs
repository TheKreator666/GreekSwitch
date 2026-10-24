using GreekSwitch.Windows.Native;
using static GreekSwitch.Windows.Native.NativeMethods;

namespace GreekSwitch.Windows.Focus;

/// <summary>Where to show a popup next to the text being typed, in screen coordinates.</summary>
public static class CaretLocator
{
    /// <summary>
    /// The caret's bottom-left corner from GetGUIThreadInfo (native Win32 apps, Office). Chrome
    /// and Electron apps often don't report a caret: then the mouse position, offset a little.
    /// </summary>
    public static (int X, int Y, bool FromCaret) Find()
    {
        var foreground = GetForegroundWindow();
        if (foreground != 0)
        {
            var thread = GetWindowThreadProcessId(foreground, out _);
            var info = new GUITHREADINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<GUITHREADINFO>() };
            if (GetGUIThreadInfo(thread, ref info) && info.hwndCaret != 0
                && (info.rcCaret.right > info.rcCaret.left || info.rcCaret.bottom > info.rcCaret.top))
            {
                var point = new POINT { x = info.rcCaret.left, y = info.rcCaret.bottom };
                if (ClientToScreen(info.hwndCaret, ref point))
                {
                    return (point.x, point.y + 2, true);
                }
            }
        }

        return GetCursorPos(out var cursor) ? (cursor.x + 12, cursor.y + 18, false) : (0, 0, false);
    }
}
