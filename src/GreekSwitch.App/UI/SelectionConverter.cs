using System.Runtime.InteropServices;
using GreekSwitch.Core.Layout;
using GreekSwitch.Windows.Layouts;
using GreekSwitch.Windows.Native;
using GreekSwitch.Windows.Output;
using Microsoft.Extensions.Logging;

namespace GreekSwitch.App.UI;

/// <summary>The clipboard as "convert selection" uses it (UI thread only: the clipboard needs STA).</summary>
internal interface IClipboardAccess
{
    /// <summary>Changes whenever any program writes the clipboard.</summary>
    uint SequenceNumber { get; }

    /// <summary>A copy of every format that can be read now; null if the clipboard is empty.</summary>
    object? Backup();

    /// <summary>Puts a <see cref="Backup"/> back (null: empties the clipboard), kept out of clipboard history.</summary>
    void Restore(object? backup);

    string? GetText();

    /// <summary>Sets text marked to stay out of clipboard history, cloud clipboard and clipboard monitors.</summary>
    void SetPrivateText(string text);
}

/// <summary>
/// "Convert selection": copy the selected text, re-read it in the other layout, paste it back,
/// switch the layout to the converted text's language, and restore what was on the clipboard.
/// Runs on the UI thread, awaiting between steps so the message loop keeps running.
/// </summary>
internal sealed class SelectionConverter
{
    private static readonly TimeSpan ReleaseTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan CopyTimeout = TimeSpan.FromMilliseconds(500);

    /// <summary>The target app reads the clipboard after it handles Ctrl+V; restoring sooner would paste the old content.</summary>
    private static readonly TimeSpan PasteSettle = TimeSpan.FromMilliseconds(500);

    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(15);

    private readonly IClipboardAccess _clipboard;
    private readonly IInputSender _sender;
    private readonly ILayoutSwitcher _layouts;
    private readonly TextConverter _converter;
    private readonly Func<bool> _modifierHeld;
    private readonly Func<nint> _foreground;
    private readonly Func<nint, bool> _isCommandWindow;
    private readonly ILogger _logger;
    private readonly Func<TimeSpan, Task> _delay;
    private bool _busy;

    public SelectionConverter(
        IClipboardAccess clipboard,
        IInputSender sender,
        ILayoutSwitcher layouts,
        TextConverter converter,
        Func<bool> modifierHeld,
        Func<nint> foreground,
        Func<nint, bool> isCommandWindow,
        ILogger logger,
        Func<TimeSpan, Task>? delay = null)
    {
        _clipboard = clipboard;
        _sender = sender;
        _layouts = layouts;
        _converter = converter;
        _modifierHeld = modifierHeld;
        _foreground = foreground;
        _isCommandWindow = isCommandWindow;
        _logger = logger;
        _delay = delay ?? Task.Delay;
    }

    /// <summary>Waits (up to 2 s) until the hotkey's modifiers are released; false if they weren't.</summary>
    public static async Task<bool> WaitForReleaseAsync(Func<bool> modifierHeld, Func<TimeSpan, Task>? delay = null)
    {
        delay ??= Task.Delay;
        for (var waited = TimeSpan.Zero; modifierHeld(); waited += Poll)
        {
            if (waited >= ReleaseTimeout)
            {
                return false;
            }

            await delay(Poll).ConfigureAwait(true);
        }

        return true;
    }

    /// <returns>True if converted text was pasted.</returns>
    public async Task<bool> ConvertAsync()
    {
        if (_busy)
        {
            return false;
        }

        _busy = true;
        try
        {
            return await ConvertCoreAsync().ConfigureAwait(true);
        }
        catch (ExternalException ex)
        {
            _logger.LogWarning("Selection not converted: the clipboard is in use ({Type})", ex.GetType().Name);
            return false;
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task<bool> ConvertCoreAsync()
    {
        // Ctrl+C with Alt or Shift still down would be another shortcut (Ctrl+Shift+C opens dev tools).
        if (!await WaitForReleaseAsync(_modifierHeld, _delay).ConfigureAwait(true))
        {
            _logger.LogInformation("Selection not converted: the hotkey's keys are still held");
            return false;
        }

        var window = _foreground();
        if (_isCommandWindow(window))
        {
            _logger.LogInformation("Selection not converted: Ctrl+C would interrupt a terminal");
            return false;
        }

        var backup = _clipboard.Backup();
        var before = _clipboard.SequenceNumber;
        if (!Send(SelectionKeys.Copy(), "Ctrl+C") || !await CopiedAsync(before).ConfigureAwait(true))
        {
            _logger.LogInformation("Selection not converted: nothing was copied (no selection?)");
            return false; // the clipboard is unchanged
        }

        try
        {
            var text = _clipboard.GetText();
            if (string.IsNullOrEmpty(text) || TextConverter.Script(text) is null)
            {
                _logger.LogInformation("Selection not converted: no letters in it");
                return false;
            }

            var converted = _converter.Convert(text);
            if (converted == text)
            {
                _logger.LogInformation("Selection not converted: nothing to change, length {Length}", text.Length);
                return false;
            }

            if (_foreground() != window)
            {
                _logger.LogInformation("Selection not converted: another window became active");
                return false;
            }

            _clipboard.SetPrivateText(converted);
            if (!Send(SelectionKeys.Paste(), "Ctrl+V"))
            {
                return false;
            }

            if (TextConverter.Script(converted) is { } lang)
            {
                _layouts.SwitchTo(lang);
            }

            await _delay(PasteSettle).ConfigureAwait(true);
            _logger.LogInformation("Selection converted, length {Length}", converted.Length);
            return true;
        }
        finally
        {
            _clipboard.Restore(backup);
        }
    }

    private async Task<bool> CopiedAsync(uint before)
    {
        for (var waited = TimeSpan.Zero; waited < CopyTimeout; waited += Poll)
        {
            if (_clipboard.SequenceNumber != before)
            {
                return true;
            }

            await _delay(Poll).ConfigureAwait(true);
        }

        return _clipboard.SequenceNumber != before;
    }

    private bool Send(INPUT[] inputs, string what)
    {
        if (_sender.Send(inputs) == inputs.Length)
        {
            return true;
        }

        _logger.LogWarning("Selection not converted: {Keys} could not be sent", what);
        return false;
    }
}
