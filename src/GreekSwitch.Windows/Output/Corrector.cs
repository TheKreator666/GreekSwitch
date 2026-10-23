using System.Runtime.InteropServices;
using GreekSwitch.Core;
using GreekSwitch.Core.Layout;
using GreekSwitch.Core.Session;
using GreekSwitch.Windows.Layouts;
using GreekSwitch.Windows.Native;
using Microsoft.Extensions.Logging;
using static GreekSwitch.Windows.Native.NativeMethods;

namespace GreekSwitch.Windows.Output;

/// <summary>Applies corrections to the focused app with one SendInput batch and switches its layout.</summary>
public sealed class Corrector : ICorrectionOutput
{
    private readonly ILayoutSwitcher _layouts;
    private readonly ILogger _logger;

    public Corrector(ILayoutSwitcher layouts, ILogger logger)
    {
        _layouts = layouts;
        _logger = logger;
    }

    public bool ReplaceText(int backspaces, string text)
    {
        // With Ctrl or Alt still held, Backspace would delete whole words or trigger shortcuts.
        if (IsDown(VirtualKeys.Control) || IsDown(VirtualKeys.Menu) || IsDown(VirtualKeys.LWin) || IsDown(VirtualKeys.RWin))
        {
            _logger.LogInformation("Correction skipped: a modifier key is held");
            return false;
        }

        var inputs = InputBuilder.Build(backspaces, text);
        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != inputs.Length)
        {
            // Typically UIPI: the target runs elevated and we don't. SendInput is all-or-nothing
            // in practice (0 sent) when blocked.
            _logger.LogWarning("SendInput delivered {Sent} of {Total} events (error {Error})", sent, inputs.Length, Marshal.GetLastPInvokeError());
            return false;
        }

        return true;
    }

    public void SwitchLayout(Lang layout) => _layouts.SwitchTo(layout);
}
