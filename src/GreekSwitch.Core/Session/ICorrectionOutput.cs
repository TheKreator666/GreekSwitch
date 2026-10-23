namespace GreekSwitch.Core.Session;

/// <summary>Where corrections go: SendInput on Windows, a simulated screen in tests.</summary>
public interface ICorrectionOutput
{
    /// <summary>
    /// Deletes <paramref name="backspaces"/> characters before the caret, then types
    /// <paramref name="text"/> ('\n' as Enter, '\t' as Tab), as one uninterrupted batch.
    /// </summary>
    /// <returns>False if the correction was not applied (e.g. a modifier was held, or input was blocked).</returns>
    bool ReplaceText(int backspaces, string text);

    /// <summary>Makes <paramref name="layout"/> the active keyboard layout of the focused window.</summary>
    void SwitchLayout(Lang layout);
}
