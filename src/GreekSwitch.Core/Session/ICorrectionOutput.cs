namespace GreekSwitch.Core.Session;

/// <summary>Where corrections go: SendInput on Windows, a simulated screen in tests.</summary>
public interface ICorrectionOutput
{
    /// <summary>
    /// Makes <paramref name="layout"/> the active layout of the focused window, then replaces
    /// <paramref name="before"/>, the text just before the caret, with <paramref name="after"/>
    /// ('\n' as Enter, '\t' as Tab) as one uninterrupted batch: before.Length Backspaces, then
    /// the new text. Implementations may check that the text before the caret really is
    /// <paramref name="before"/> (another tool may have rewritten it) and refuse if not.
    /// </summary>
    /// <remarks>
    /// The switch must take effect before any text is typed: apps such as Windows 11 Notepad pass
    /// injected characters through the active layout, and letters it can't produce come out as spaces.
    /// </remarks>
    /// <returns>False if nothing was typed (e.g. a modifier was held, input was blocked, or the user typed ahead).</returns>
    bool Replace(string before, string after, Lang layout);

    /// <summary>
    /// Changes letters inside <paramref name="text"/>, the text just before the caret, so that it
    /// reads <paramref name="replacement"/> (same length), in one uninterrupted batch: the caret
    /// moves left to each changed stretch, retypes only that, and comes back to the end. Text that
    /// doesn't change (other words, spaces, English) is never retyped, and the layout isn't
    /// switched. Implementations may check the text before the caret and refuse.
    /// </summary>
    /// <returns>False if nothing was typed.</returns>
    bool Retouch(string text, string replacement);

    /// <summary>Turns Caps Lock off (after fixing an accidental Caps Lock word).</summary>
    void TurnOffCapsLock();
}
