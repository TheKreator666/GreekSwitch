using GreekSwitch.Core;
using GreekSwitch.Windows.Output;
using Microsoft.Extensions.Logging;
using static GreekSwitch.Windows.Native.NativeMethods;

namespace GreekSwitch.Windows.Layouts;

public interface ILayoutSwitcher
{
    /// <summary>Language of the focused window's layout; null for a layout that is neither Greek nor English.</summary>
    Lang? CurrentLayout();

    void SwitchTo(Lang lang);

    /// <summary>Presses the user's language-switch hotkey once (fallback for apps that ignore <see cref="SwitchTo"/>).</summary>
    void SwitchWithHotkey();
}

/// <summary>
/// Reads the foreground layout with GetKeyboardLayout(GetWindowThreadProcessId(...)) and switches
/// it by posting WM_INPUTLANGCHANGEREQUEST to the foreground window, the same request the language
/// bar makes.
/// </summary>
public sealed class LayoutSwitcher : ILayoutSwitcher
{
    private readonly ILogger _logger;
    private readonly IInputSender _sender;

    public LayoutSwitcher(ILogger logger, IInputSender? sender = null)
    {
        _logger = logger;
        _sender = sender ?? new Win32InputSender();
    }

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

    public void SwitchWithHotkey()
    {
        var hotkey = LayoutHotkeys.Current();
        var inputs = InputBuilder.Chord(LayoutHotkeys.Keys(hotkey));
        if (_sender.Send(inputs) != inputs.Length)
        {
            _logger.LogWarning("Layout hotkey {Hotkey} could not be sent", hotkey);
        }
    }
}
