using GreekSwitch.Core;
using GreekSwitch.Windows.Native;
using static GreekSwitch.Windows.Native.NativeMethods;

namespace GreekSwitch.Windows.Layouts;

/// <summary>Reads keyboard layouts (HKLs) and maps them to <see cref="Lang"/>.</summary>
public static class KeyboardLayouts
{
    private const int LangGreek = 0x08;
    private const int LangEnglish = 0x09;

    /// <summary>
    /// The language of an HKL from the primary-language bits of its low word: any Greek layout
    /// (0x08), any English layout (0x09). Other languages give null, and GreekSwitch stays passive.
    /// </summary>
    public static Lang? ToLang(nint hkl) => ((int)(hkl & 0x3FF)) switch
    {
        LangGreek => Lang.Greek,
        LangEnglish => Lang.English,
        _ => null,
    };

    /// <summary>The layouts the user has installed (the language bar list).</summary>
    public static unsafe IReadOnlyList<nint> Installed()
    {
        var count = GetKeyboardLayoutList(0, null);
        var list = new nint[count];
        fixed (nint* p = list)
        {
            count = GetKeyboardLayoutList(count, p);
        }

        return list[..count];
    }

    /// <summary>The first installed layout for <paramref name="lang"/>, or 0 if none is installed.</summary>
    public static nint FindInstalled(Lang lang) => Installed().FirstOrDefault(h => ToLang(h) == lang);

    /// <summary>
    /// The layout of the thread that owns keyboard focus. That's the focused window's thread, not
    /// the frame's: UWP apps and some browsers type into a child window on another thread.
    /// </summary>
    public static (nint Hkl, nint FocusWindow) ForegroundLayout()
    {
        var foreground = GetForegroundWindow();
        if (foreground == 0)
        {
            return (0, 0);
        }

        var thread = GetWindowThreadProcessId(foreground, out _);
        var info = new GUITHREADINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<GUITHREADINFO>() };
        var focus = GetGUIThreadInfo(thread, ref info) && info.hwndFocus != 0 ? info.hwndFocus : foreground;
        var focusThread = GetWindowThreadProcessId(focus, out _);
        return (GetKeyboardLayout(focusThread), focus);
    }
}
