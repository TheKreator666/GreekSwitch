namespace GreekSwitch.Core.Session;

/// <summary>Where corrections go: SendInput on Windows, a simulated screen in tests.</summary>
public interface ICorrectionOutput
{
    /// <summary>
    /// Makes <paramref name="layout"/> the active layout of the focused window, then deletes
    /// <paramref name="backspaces"/> characters before the caret and types <paramref name="text"/>
    /// ('\n' as Enter, '\t' as Tab) as one uninterrupted batch.
    /// </summary>
    /// <remarks>
    /// The switch must take effect before any text is typed: apps such as Windows 11 Notepad pass
    /// injected characters through the active layout, and letters it can't produce come out as spaces.
    /// </remarks>
    /// <returns>False if nothing was typed (e.g. a modifier was held, input was blocked, or the user typed ahead).</returns>
    bool Replace(int backspaces, string text, Lang layout);
}
