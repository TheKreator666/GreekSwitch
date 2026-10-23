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
        var (answered, match) = _uia.Run(() => Compare(() => _automation.Value.GetFocusedElement(), expected), _timeout);
        return answered ? match : TextMatch.Unknown;
    }

    /// <summary>Same comparison for a specific window, e.g. a control's handle (used by tests).</summary>
    public TextMatch BeforeCaretIn(nint hwnd, string expected)
    {
        var (answered, match) = _uia.Run(() => Compare(() => _automation.Value.ElementFromHandle(hwnd), expected), _timeout);
        return answered ? match : TextMatch.Unknown;
    }

    /// <summary>Line breaks come back as \r or \r\n from rich edit controls; we type \n.</summary>
    public static string NormalizeLineBreaks(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static TextMatch Compare(Func<IUIAutomationElement?> find, string expected)
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
                return TextMatch.Unknown; // no TextPattern: keep the other guards
            }

            selection = pattern.GetSelection();
            if (selection is null || selection.Length == 0)
            {
                return TextMatch.Unknown;
            }

            caret = selection.GetElement(0);
            before = caret.Clone();

            // Collapse to the end of the selection (the caret), then reach back.
            before.MoveEndpointByRange(Uia.EndpointStart, caret, Uia.EndpointEnd);
            var expectedText = NormalizeLineBreaks(expected);
            var moved = before.MoveEndpointByUnit(Uia.EndpointStart, Uia.UnitCharacter, -(expectedText.Length + 1));
            var text = NormalizeLineBreaks(before.GetText(-1) ?? "");

            if (moved == 0 && expectedText.Length > 0)
            {
                return TextMatch.Unknown; // the control reports no text before the caret at all
            }

            return text.EndsWith(expectedText, StringComparison.Ordinal) ? TextMatch.Match : TextMatch.Mismatch;
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or InvalidOperationException)
        {
            return TextMatch.Unknown;
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
