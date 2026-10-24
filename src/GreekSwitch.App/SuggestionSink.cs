using GreekSwitch.Core.Session;
using GreekSwitch.Windows.Focus;
using GreekSwitch.Windows.Hooks;
using GreekSwitch.Windows.Layouts;

namespace GreekSwitch.App;

/// <summary>A suggestion to show next to the caret (screen coordinates).</summary>
internal sealed class SuggestionEventArgs(long id, string text, int x, int y) : EventArgs
{
    /// <summary>Identifies this suggestion: the UI opens the Tab gate, and expires it, by id.</summary>
    public long Id { get; } = id;

    /// <summary>The suggested replacement: an English dictionary word, or the other layout's reading.</summary>
    public string Text { get; } = text;

    public int X { get; } = x;

    public int Y { get; } = y;
}

/// <summary>
/// The session's suggestions on Windows. Show (on the worker) reads the caret and asks the UI for
/// the popup; the UI opens the hook's Tab gate only once the popup is on screen, and only if that
/// suggestion is still the current one, so Tab never accepts something the user can't see.
/// Never in a terminal or IDE, where Tab must stay Tab.
/// </summary>
internal sealed class SuggestionSink(SuggestionGate gate) : ISuggestionSink
{
    private readonly object _lock = new();
    private long _nextId;
    private long _current;

    public event EventHandler<SuggestionEventArgs>? Shown;

    public event EventHandler? Hidden;

    public void Show(string text)
    {
        var (_, focus) = KeyboardLayouts.ForegroundLayout();
        if (CommandWindows.IsCommandWindow(focus))
        {
            return; // the session keeps its pending suggestion until the next key; Tab passes through
        }

        long id;
        lock (_lock)
        {
            id = _current = ++_nextId;
        }

        var (x, y, _) = CaretLocator.Find();
        Shown?.Invoke(this, new SuggestionEventArgs(id, text, x, y));
    }

    public void Hide()
    {
        lock (_lock)
        {
            _current = 0;
            gate.Close();
        }

        Hidden?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>UI thread: the popup for <paramref name="id"/> is visible; Tab may accept it now.</summary>
    public void Opened(long id)
    {
        lock (_lock)
        {
            if (_current == id)
            {
                gate.Open();
            }
        }
    }

    /// <summary>UI thread: the popup for <paramref name="id"/> timed out.</summary>
    public void Expired(long id)
    {
        lock (_lock)
        {
            if (_current == id)
            {
                _current = 0;
                gate.Close();
            }
        }
    }
}
