using GreekSwitch.Core.Layout;
using Microsoft.Win32;

namespace GreekSwitch.Windows.Layouts;

/// <summary>The user's "switch input language" hotkey (Settings → Typing → Advanced keyboard settings).</summary>
public enum LayoutHotkey
{
    AltShift,
    CtrlShift,

    /// <summary>The ` key on its own.</summary>
    Grave,

    /// <summary>No language hotkey assigned; Win+Space still works.</summary>
    None,
}

/// <summary>
/// Reads the language hotkey from HKCU\Keyboard Layout\Toggle and turns it into keys to press.
/// Used as a fallback when an app ignores the WM_INPUTLANGCHANGEREQUEST switch request.
/// </summary>
public static class LayoutHotkeys
{
    private const string ToggleKey = @"Keyboard Layout\Toggle";

    /// <summary>
    /// "Language Hotkey" (or the older "Hotkey"): "1" Alt+Shift (also Windows' default when
    /// nothing is set), "2" Ctrl+Shift, "3" not assigned, "4" grave accent.
    /// </summary>
    public static LayoutHotkey FromRegistry(string? languageHotkey, string? hotkey) => (languageHotkey ?? hotkey) switch
    {
        "2" => LayoutHotkey.CtrlShift,
        "3" => LayoutHotkey.None,
        "4" => LayoutHotkey.Grave,
        _ => LayoutHotkey.AltShift,
    };

    /// <summary>The keys to press together (held in order, released in reverse).</summary>
    public static IReadOnlyList<int> Keys(LayoutHotkey hotkey) => hotkey switch
    {
        LayoutHotkey.AltShift => [VirtualKeys.LMenu, VirtualKeys.LShift],
        LayoutHotkey.CtrlShift => [VirtualKeys.LControl, VirtualKeys.LShift],
        LayoutHotkey.Grave => [VirtualKeys.Oem3],
        _ => [VirtualKeys.LWin, VirtualKeys.Space],
    };

    public static LayoutHotkey Current()
    {
        using var key = Registry.CurrentUser.OpenSubKey(ToggleKey);
        return FromRegistry(key?.GetValue("Language Hotkey") as string, key?.GetValue("Hotkey") as string);
    }
}
