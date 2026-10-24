namespace GreekSwitch.Core.Session;

/// <summary>
/// Where suggestions go: a popup next to the caret on Windows (Tab accepts), a list in tests.
/// Called on the worker; implementations must return quickly.
/// </summary>
public interface ISuggestionSink
{
    /// <summary>Show <paramref name="text"/> as the suggestion for the word just typed.</summary>
    void Show(string text);

    /// <summary>The suggestion is no longer valid (a key was pressed, focus moved, it was accepted).</summary>
    void Hide();
}
