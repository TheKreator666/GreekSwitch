using System.Diagnostics;
using System.Runtime.InteropServices;
using GreekSwitch.Core;
using GreekSwitch.Core.Layout;
using GreekSwitch.Core.Session;
using GreekSwitch.Windows.Layouts;
using GreekSwitch.Windows.Native;
using Microsoft.Extensions.Logging;
using static GreekSwitch.Windows.Native.NativeMethods;

namespace GreekSwitch.Windows.Output;

public interface IInputSender
{
    /// <summary>Injects the batch; returns how many events were inserted.</summary>
    uint Send(INPUT[] inputs);
}

/// <summary>SendInput.</summary>
public sealed class Win32InputSender : IInputSender
{
    public uint Send(INPUT[] inputs) => SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
}

/// <summary>
/// Applies a correction to the focused app. Order matters: switch the layout, wait until the
/// focused window reports it, and only then send the Backspaces and Unicode text in one SendInput
/// batch. Apps such as Windows 11 Notepad pass injected characters through the active layout, so
/// Latin text sent while Greek is still active comes out as spaces.
/// </summary>
public sealed class Corrector : ICorrectionOutput
{
    private readonly ILayoutSwitcher _layouts;
    private readonly TypeAheadGuard _typeAhead;
    private readonly ILogger _logger;
    private readonly IInputSender _sender;
    private readonly Func<bool> _modifierHeld;
    private readonly TimeSpan _switchTimeout;

    public Corrector(
        ILayoutSwitcher layouts,
        TypeAheadGuard typeAhead,
        ILogger logger,
        IInputSender? sender = null,
        Func<bool>? modifierHeld = null,
        TimeSpan? switchTimeout = null)
    {
        _layouts = layouts;
        _typeAhead = typeAhead;
        _logger = logger;
        _sender = sender ?? new Win32InputSender();
        _modifierHeld = modifierHeld ?? AnyModifierDown;
        _switchTimeout = switchTimeout ?? TimeSpan.FromMilliseconds(300);
    }

    public bool Replace(int backspaces, string text, Lang layout)
    {
        // With Ctrl, Alt or Win held, Backspace would delete whole words or trigger shortcuts.
        if (_modifierHeld())
        {
            _logger.LogInformation("Correction skipped: a modifier key is held");
            return false;
        }

        var waited = Stopwatch.StartNew();
        if (!EnsureLayout(layout))
        {
            _logger.LogWarning("Correction skipped: switch to {Lang} not visible after {Ms} ms", layout.Code(), (int)waited.ElapsedMilliseconds);
            return false;
        }

        // Keys typed meanwhile are already on their way to the app, ahead of our Backspaces.
        if (_typeAhead.UserTypedAhead)
        {
            _logger.LogInformation("Correction skipped: typing continued before it could be applied");
            return false;
        }

        var inputs = InputBuilder.Build(backspaces, text);
        var sent = _sender.Send(inputs);
        if (sent != inputs.Length)
        {
            // Typically UIPI: the target runs elevated and we don't.
            _logger.LogWarning("SendInput delivered {Sent} of {Total} events (error {Error})", sent, inputs.Length, Marshal.GetLastPInvokeError());
            return false;
        }

        return true;
    }

    private static bool AnyModifierDown() =>
        IsDown(VirtualKeys.Control) || IsDown(VirtualKeys.Menu) || IsDown(VirtualKeys.LWin) || IsDown(VirtualKeys.RWin);

    private bool EnsureLayout(Lang layout)
    {
        if (_layouts.CurrentLayout() == layout)
        {
            return true;
        }

        _layouts.SwitchTo(layout);
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < _switchTimeout)
        {
            if (_layouts.CurrentLayout() == layout)
            {
                return true;
            }

            Thread.Sleep(1);
        }

        return false;
    }
}
