using GreekSwitch.Core.Session;
using GreekSwitch.Windows.Focus;
using GreekSwitch.Windows.Hooks;

namespace GreekSwitch.App;

/// <summary>A suggestion to show next to the caret (screen coordinates).</summary>
internal sealed class SuggestionEventArgs(string text, int x, int y) : EventArgs
{
    /// <summary>The suggested replacement: an English dictionary word, or the other layout's reading.</summary>
    public string Text { get; } = text;

    public int X { get; } = x;

    public int Y { get; } = y;
}

/// <summary>
/// The session's suggestions on Windows: opens the hook's Tab gate and asks the UI for the popup
/// at the caret. Runs on the worker, so it only reads the caret and raises events.
/// </summary>
internal sealed class SuggestionSink(SuggestionGate gate) : ISuggestionSink
{
    public event EventHandler<SuggestionEventArgs>? Shown;

    public event EventHandler? Hidden;

    public void Show(string text)
    {
        var (x, y, _) = CaretLocator.Find();
        Shown?.Invoke(this, new SuggestionEventArgs(text, x, y));
        gate.Open();
    }

    public void Hide()
    {
        gate.Close();
        Hidden?.Invoke(this, EventArgs.Empty);
    }
}
