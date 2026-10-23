namespace GreekSwitch.Core.Session;

public sealed record SessionOptions
{
    /// <summary>
    /// Also correct words ended by Enter or Tab. Off by default: by the time we see the key,
    /// Enter may have sent a chat message and Tab may have moved focus to another field.
    /// </summary>
    public bool CorrectOnEnterAndTab { get; init; }

    /// <summary>
    /// The app's registered hotkeys. Their keys reach the hook before Windows turns them into a
    /// hotkey message; they must not reset the word they are meant to convert.
    /// </summary>
    public IReadOnlyList<Input.KeyChord> HotkeyChords { get; init; } = [];

    /// <summary>
    /// Debug builds only: include the words themselves in debug log events. Ignored, and
    /// compiled out, in Release builds.
    /// </summary>
    public bool DebugLogText { get; init; }
}
