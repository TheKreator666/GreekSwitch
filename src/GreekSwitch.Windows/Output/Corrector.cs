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

/// <summary>What the corrector needs to know about the physical keyboard right now.</summary>
public interface IKeyboardState
{
    /// <summary>Ctrl, Alt or Win held: Backspace would delete words or trigger shortcuts.</summary>
    bool CommandModifierHeld { get; }

    /// <summary>Shift held: it would change the case of the keys we press.</summary>
    bool ShiftHeld { get; }

    bool CapsLockOn { get; }
}

/// <summary>
/// Applies a correction to the focused app: switch the layout, wait until the focused window
/// reports it, check (where UI Automation allows) that the text before the caret is still what
/// was typed, then press the real keys that type the replacement in that layout, in one
/// SendInput batch. Real keys rather than Unicode, because Windows 11 Notepad turns injected
/// Unicode characters typed right after a layout switch into spaces or repeated letters,
/// while it handles real keys fine. Unicode remains the fallback for characters no key types.
/// </summary>
public sealed class Corrector : ICorrectionOutput
{
    private readonly ILayoutSwitcher _layouts;
    private readonly Func<Lang, LayoutMap> _maps;
    private readonly TypeAheadGuard _typeAhead;
    private readonly IKeyboardState _keyboard;
    private readonly ILogger _logger;
    private readonly IInputSender _sender;
    private readonly Func<int, ushort> _scanCode;
    private readonly FocusGuard _focus;
    private readonly Func<nint> _currentFocus;
    private readonly Func<nint, bool> _isCommandWindow;
    private readonly ITextVerifier _textVerifier;
    private readonly KeyHold _hold;
    private readonly TimeSpan _timeout;

    public Corrector(
        ILayoutSwitcher layouts,
        Func<Lang, LayoutMap> maps,
        TypeAheadGuard typeAhead,
        IKeyboardState keyboard,
        ILogger logger,
        FocusGuard? focus = null,
        IInputSender? sender = null,
        Func<int, ushort>? scanCode = null,
        TimeSpan? timeout = null,
        Func<nint>? currentFocus = null,
        Func<nint, bool>? isCommandWindow = null,
        ITextVerifier? textVerifier = null,
        KeyHold? hold = null)
    {
        _hold = hold ?? new KeyHold();
        _layouts = layouts;
        _maps = maps;
        _typeAhead = typeAhead;
        _keyboard = keyboard;
        _logger = logger;
        _focus = focus ?? new FocusGuard();
        _sender = sender ?? new Win32InputSender();
        _scanCode = scanCode ?? (vk => (ushort)MapVirtualKeyExW((uint)vk, 0 /* MAPVK_VK_TO_VSC */, 0));
        _timeout = timeout ?? TimeSpan.FromMilliseconds(300);
        _currentFocus = currentFocus ?? (() => KeyboardLayouts.ForegroundLayout().FocusWindow);
        _isCommandWindow = isCommandWindow ?? Focus.CommandWindows.IsCommandWindow;
        _textVerifier = textVerifier ?? new NoTextVerifier();
    }

    /// <summary>Extra wait after the focused window reports a layout switch, before typing.</summary>
    public TimeSpan SwitchSettle { get; init; }

    public bool Replace(string before, string after, Lang layout)
    {
        var backspaces = before.Length;
        var text = after;

        if (_keyboard.CommandModifierHeld)
        {
            _logger.LogInformation("Correction skipped: a modifier key is held");
            return false;
        }

        if (!_focus.StillFocused(_currentFocus()))
        {
            _logger.LogInformation("Correction skipped: focus moved to another control");
            return false;
        }

        // Retyping Enter in a terminal would run the corrected text as a new command.
        if (text.AsSpan().IndexOfAny('\n', '\t') >= 0 && _isCommandWindow(_currentFocus()))
        {
            _logger.LogInformation("Correction skipped: Enter/Tab in a terminal or IDE");
            return false;
        }

        // E.g. Shift still down from typing '?' or '!': wait for it, or our keys come out shifted.
        if (!WaitUntil(() => !_keyboard.ShiftHeld))
        {
            _logger.LogInformation("Correction skipped: Shift is held");
            return false;
        }

        if (!EnsureLayout(layout))
        {
            _logger.LogWarning("Correction skipped: switch to {Lang} not visible in time", layout.Code());
            return false;
        }

        // Keys typed meanwhile are already on their way to the app, ahead of our Backspaces.
        if (_typeAhead.UserTypedAhead)
        {
            _logger.LogInformation("Correction skipped: typing continued before it could be applied");
            return false;
        }

        // Focus can also move during the layout switch and settle pause.
        if (!_focus.StillFocused(_currentFocus()))
        {
            _logger.LogInformation("Correction skipped: focus moved to another control");
            return false;
        }

        // Another tool (AutoCorrect, text suggestions, Grammarly) may have rewritten the word
        // after the delimiter; blind Backspaces would then delete the wrong characters.
        switch (_textVerifier.BeforeCaret(before))
        {
            case TextMatch.Mismatch:
                _logger.LogInformation("Correction skipped: the text before the caret is not what was typed");
                return false;
            case TextMatch.FirstLetterCapitalised:
                // Word capitalised the sentence's first word: it would have done so to the right word too.
                _logger.LogInformation("The app capitalised the first letter; the replacement keeps it");
                text = CapitaliseFirstLetter(text);
                break;
        }

        var keys = KeyPlanner.Plan(text, _maps(layout), _keyboard.CapsLockOn);
        if (keys is null)
        {
            _logger.LogInformation("Replacement has a character no {Lang} key types; sending it as Unicode", layout.Code());
        }

        var inputs = keys is null ? InputBuilder.Build(backspaces, text) : InputBuilder.BuildKeys(backspaces, keys, _scanCode);
        var sent = SendHoldingUserKeys(inputs);
        if (sent != inputs.Length)
        {
            // Typically UIPI: the target runs elevated and we don't.
            _logger.LogWarning("SendInput delivered {Sent} of {Total} events (error {Error})", sent, inputs.Length, Marshal.GetLastPInvokeError());
            return false;
        }

        return true;
    }

    private static string CapitaliseFirstLetter(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsLetter(text[i]))
            {
                return text[..i] + char.ToUpperInvariant(text[i]) + text[(i + 1)..];
            }
        }

        return text;
    }

    /// <summary>
    /// Fixes letters in place (missing accents): one batch that moves the caret left to each
    /// changed stretch, Backspaces it, types the new letters, and moves back to the end. No layout
    /// switch: a switch requested right after typed keys reaches the app before them (posted
    /// messages go before input) and turns queued Greek keys into Latin letters. Real keys when
    /// the active layout types the letters, Unicode otherwise (e.g. the English layout is active
    /// after an English word; Unicode is only mangled right after a switch, and there is none).
    /// </summary>
    public bool Retouch(string text, string replacement)
    {
        if (text.Length != replacement.Length)
        {
            return false;
        }

        if (_keyboard.CommandModifierHeld)
        {
            _logger.LogInformation("Correction skipped: a modifier key is held");
            return false;
        }

        if (!_focus.StillFocused(_currentFocus()))
        {
            _logger.LogInformation("Correction skipped: focus moved to another control");
            return false;
        }

        if (!WaitUntil(() => !_keyboard.ShiftHeld))
        {
            _logger.LogInformation("Correction skipped: Shift is held");
            return false;
        }

        if (_typeAhead.UserTypedAhead)
        {
            _logger.LogInformation("Correction skipped: typing continued before it could be applied");
            return false;
        }

        // Where the text can be read, take what the app already did (its own accents, a sentence-start
        // capital) and only fix the rest; any other difference means the text isn't ours to touch.
        if (_textVerifier.ReadBeforeCaret(text.Length) is { } screen)
        {
            if (Focus.UiaTextVerifier.Reconcile(screen, text, replacement) is not { } onScreen)
            {
                _logger.LogInformation("Correction skipped: the text before the caret is not what was typed");
                return false;
            }

            (text, replacement) = onScreen;
        }

        var layout = _layouts.CurrentLayout();
        var inputs = RetouchInputs(text, replacement, layout is { } lang ? _maps(lang) : null, _keyboard.CapsLockOn, _scanCode);
        var sent = SendHoldingUserKeys(inputs);
        if (sent != inputs.Length)
        {
            _logger.LogWarning("SendInput delivered {Sent} of {Total} events (error {Error})", sent, inputs.Length, Marshal.GetLastPInvokeError());
            return false;
        }

        return true;
    }

    /// <summary>
    /// The batch for <see cref="Retouch"/>, right to left so earlier positions don't move: Left
    /// arrows to the end of each changed stretch, Backspaces over it, its new letters, and finally
    /// Right arrows back to where the caret was.
    /// </summary>
    public static INPUT[] RetouchInputs(string text, string replacement, LayoutMap? map, bool capsLock, Func<int, ushort> scanCode)
    {
        var inputs = new List<INPUT>();
        var caret = text.Length;
        var end = text.Length;
        while (end > 0)
        {
            if (text[end - 1] == replacement[end - 1])
            {
                end--;
                continue;
            }

            var start = end - 1;
            while (start > 0 && text[start - 1] != replacement[start - 1])
            {
                start--;
            }

            inputs.AddRange(InputBuilder.Arrows(VirtualKeys.Left, caret - end, scanCode));
            var letters = replacement[start..end];
            var keys = map is null ? null : KeyPlanner.Plan(letters, map, capsLock);
            inputs.AddRange(keys is null ? InputBuilder.Build(end - start, letters) : InputBuilder.BuildKeys(end - start, keys, scanCode));
            caret = end;
            end = start;
        }

        inputs.AddRange(InputBuilder.Arrows(VirtualKeys.Right, text.Length - caret, scanCode));
        return [.. inputs];
    }

    /// <summary>
    /// Sends the batch while the hook holds back the user's keys (with our hook installed,
    /// SendInput is not atomic: a key pressed meanwhile would land inside the correction), then
    /// replays the held keys in the order they were pressed.
    /// </summary>
    private uint SendHoldingUserKeys(INPUT[] inputs)
    {
        _hold.Begin();
        var held = 0;
        try
        {
            return _sender.Send(inputs);
        }
        finally
        {
            // Still holding while replaying, so keys pressed meanwhile queue up behind these.
            for (var keys = _hold.Drain(); keys.Count > 0; keys = _hold.Drain())
            {
                held += Replay(keys);
            }

            _hold.End();
            held += Replay(_hold.Drain()); // pressed between the last drain and the end
            if (held > 0)
            {
                _logger.LogDebug("Keys held during a correction and replayed after it: {Count}", held);
            }
        }
    }

    private int Replay(List<HeldKey> keys)
    {
        if (keys.Count == 0)
        {
            return 0;
        }

        var inputs = KeyHold.ToInputs(keys);
        if (_sender.Send(inputs) != inputs.Length)
        {
            _logger.LogWarning("Keys held during a correction could not all be replayed");
        }

        return keys.Count;
    }

    public void TurnOffCapsLock()
    {
        if (!_keyboard.CapsLockOn)
        {
            return;
        }

        var inputs = InputBuilder.Chord([VirtualKeys.Capital]);
        if (_sender.Send(inputs) != inputs.Length)
        {
            _logger.LogWarning("Caps Lock could not be turned off");
        }
    }

    private bool EnsureLayout(Lang layout)
    {
        if (_layouts.CurrentLayout() == layout)
        {
            return true;
        }

        _layouts.SwitchTo(layout);
        if (!WaitUntil(() => _layouts.CurrentLayout() == layout))
        {
            // Some apps ignore WM_INPUTLANGCHANGEREQUEST: press the user's own switch hotkey once.
            _logger.LogInformation("Layout switch request ignored; trying the layout hotkey");
            _layouts.SwitchWithHotkey();
            if (!WaitUntil(() => _layouts.CurrentLayout() == layout))
            {
                return false;
            }
        }

        // The window reports the new layout before the input stack has fully switched; keys sent
        // immediately can still be translated with the old layout.
        if (SwitchSettle > TimeSpan.Zero)
        {
            Thread.Sleep(SwitchSettle);
        }

        return true;
    }

    private bool WaitUntil(Func<bool> condition)
    {
        var clock = Stopwatch.StartNew();
        while (!condition())
        {
            if (clock.Elapsed >= _timeout)
            {
                return false;
            }

            Thread.Sleep(1);
        }

        return true;
    }
}
