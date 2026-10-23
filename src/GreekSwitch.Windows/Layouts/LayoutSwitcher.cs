using GreekSwitch.Core;
using Microsoft.Extensions.Logging;
using static GreekSwitch.Windows.Native.NativeMethods;

namespace GreekSwitch.Windows.Layouts;

public interface ILayoutSwitcher
{
    /// <summary>Language of the focused window's layout; null for a layout that is neither Greek nor English.</summary>
    Lang? CurrentLayout();

    void SwitchTo(Lang lang);
}

/// <summary>
/// Reads the foreground layout with GetKeyboardLayout(GetWindowThreadProcessId(...)) and switches
/// it by posting WM_INPUTLANGCHANGEREQUEST to the foreground window, the same request the language
/// bar makes.
/// </summary>
public sealed class LayoutSwitcher : ILayoutSwitcher
{
    private readonly ILogger _logger;

    public LayoutSwitcher(ILogger logger) => _logger = logger;

    public Lang? CurrentLayout() => KeyboardLayouts.ToLang(KeyboardLayouts.ForegroundLayout().Hkl);

    public void SwitchTo(Lang lang)
    {
        if (CurrentLayout() == lang)
        {
            return;
        }

        var hkl = KeyboardLayouts.FindInstalled(lang);
        if (hkl == 0)
        {
            _logger.LogWarning("No {Lang} keyboard layout is installed; cannot switch", lang.Code());
            return;
        }

        var window = GetForegroundWindow();
        if (window == 0 || !PostMessageW(window, WM_INPUTLANGCHANGEREQUEST, 0, hkl))
        {
            _logger.LogWarning("Layout switch to {Lang} could not be posted", lang.Code());
        }
    }
}
