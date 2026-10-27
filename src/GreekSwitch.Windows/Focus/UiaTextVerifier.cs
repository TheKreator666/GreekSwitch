using System.Runtime.InteropServices;
using GreekSwitch.Windows.Output;

namespace GreekSwitch.Windows.Focus;

/// <summary>
/// Reads the text just before the caret through UI Automation's TextPattern (on the
/// <see cref="UiaThread"/>, with a timeout) and compares it with what the corrector is about to
/// replace. The text is only compared and then dropped: never stored or logged.
/// </summary>
public sealed class UiaTextVerifier : ITextVerifier
{
    private readonly UiaThread _uia;
    private readonly TimeSpan _timeout;
    private readonly Lazy<IUIAutomation> _automation = new(() => (IUIAutomation)new CUIAutomation());

    public UiaTextVerifier(UiaThread uia, TimeSpan? timeout = null)
    {
        _uia = uia;
        _timeout = timeout ?? TimeSpan.FromMilliseconds(150);
    }

    public TextMatch BeforeCaret(string expected)
    {
        var expectedText = NormalizeLineBreaks(expected);
        return ReadBeforeCaret(expectedText.Length) is { } screen ? Classify(screen, expectedText) : TextMatch.Unknown;
    }

    public string? ReadBeforeCaret(int length)
    {
        var (answered, text) = _uia.Run(() => Read(() => _automation.Value.GetFocusedElement(), length), _timeout);
        return answered ? text : null;
    }

    /// <summary>Same comparison for a specific window, e.g. a control's handle (used by tests).</summary>
    public TextMatch BeforeCaretIn(nint hwnd, string expected)
    {
        var expectedText = NormalizeLineBreaks(expected);
        var (answered, text) = _uia.Run(() => Read(() => _automation.Value.ElementFromHandle(hwnd), expectedText.Length), _timeout);
        return answered && text is not null ? Classify(text, expectedText) : TextMatch.Unknown;
    }

    /// <summary>
    /// For accents fixed in place: each letter on screen may be as typed, or already as wanted (the
    /// app accented it itself, as Word's AutoCorrect does), and the first letter may have been
    /// capitalised by the app. Returns the text and replacement adjusted to what is on screen, so
    /// only what is still missing gets retyped; null for any other difference.
    /// </summary>
    public static (string Text, string Replacement)? Reconcile(string screen, string text, string replacement)
    {
        screen = NormalizeLineBreaks(screen).Replace(' ', ' ');
        if (screen.Length < text.Length || text.Length != replacement.Length)
        {
            return null;
        }

        var tail = screen.AsSpan(screen.Length - text.Length);
        var first = 0;
        while (first < text.Length && !char.IsLetter(text[first]))
        {
            first++;
        }

        var newText = text.ToCharArray();
        var newReplacement = replacement.ToCharArray();
        for (var i = 0; i < text.Length; i++)
        {
            if (tail[i] == text[i])
            {
                continue;
            }

            if (tail[i] == replacement[i])
            {
                newText[i] = replacement[i]; // already done by the app
                continue;
            }

            var upperTyped = char.ToUpperInvariant(text[i]);
            var upperWanted = char.ToUpperInvariant(replacement[i]);
            if (i == first && char.IsLower(text[i]) && (tail[i] == upperTyped || tail[i] == upperWanted))
            {
                newText[i] = tail[i]; // the app's sentence-start capital stays
                newReplacement[i] = upperWanted;
                continue;
            }

            return null;
        }

        return (new string(newText), new string(newReplacement));
    }

    /// <summary>Line breaks come back as \r or \r\n from rich edit controls; we type \n.</summary>
    public static string NormalizeLineBreaks(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    /// <summary>
    /// Compares the text before the caret with what was typed. The only accepted difference is an
    /// upper-cased first letter (sentence-start AutoCorrect); anything else is a mismatch. A no-break
    /// space on screen counts as a space.
    /// </summary>
    public static TextMatch Classify(string screen, string expected)
    {
        // Chrome's contenteditable keeps a typed space as a no-break space; one Backspace deletes either.
        screen = screen.Replace(' ', ' ');
        if (screen.EndsWith(expected, StringComparison.Ordinal))
        {
            return TextMatch.Match;
        }

        var first = 0;
        while (first < expected.Length && !char.IsLetter(expected[first]))
        {
            first++;
        }

        if (first == expected.Length || !char.IsLower(expected[first]) || screen.Length < expected.Length)
        {
            return TextMatch.Mismatch;
        }

        var tail = screen.AsSpan(screen.Length - expected.Length);
        return tail[..first].SequenceEqual(expected.AsSpan(0, first))
            && tail[first] == char.ToUpperInvariant(expected[first])
            && tail[(first + 1)..].SequenceEqual(expected.AsSpan(first + 1))
                ? TextMatch.FirstLetterCapitalised
                : TextMatch.Mismatch;
    }

    /// <summary>The text before the caret (length + 1 characters where there are), or null if unreadable.</summary>
    private static string? Read(Func<IUIAutomationElement?> find, int length)
    {
        IUIAutomationElement? element = null;
        IUIAutomationTextPattern? pattern = null;
        IUIAutomationTextRangeArray? selection = null;
        IUIAutomationTextRange? caret = null;
        IUIAutomationTextRange? before = null;
        try
        {
            element = find();
            pattern = element?.GetCurrentPattern(Uia.TextPatternId) as IUIAutomationTextPattern;
            if (pattern is null)
            {
                return null; // no TextPattern: keep the other guards
            }

            selection = pattern.GetSelection();
            if (selection is null || selection.Length == 0)
            {
                return null;
            }

            caret = selection.GetElement(0);
            before = caret.Clone();

            // Collapse to the end of the selection (the caret), then reach back.
            before.MoveEndpointByRange(Uia.EndpointStart, caret, Uia.EndpointEnd);
            var moved = before.MoveEndpointByUnit(Uia.EndpointStart, Uia.UnitCharacter, -(length + 1));
            if (moved == 0 && length > 0)
            {
                return null; // the control reports no text before the caret at all
            }

            return NormalizeLineBreaks(before.GetText(-1) ?? "");
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or InvalidOperationException)
        {
            return null;
        }
        finally
        {
            Uia.Release(before);
            Uia.Release(caret);
            Uia.Release(selection);
            Uia.Release(pattern);
            Uia.Release(element);
        }
    }
}
