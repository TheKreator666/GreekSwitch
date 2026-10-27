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

    /// <summary>Check kept Greek words for English loanwords (κοντέινερ → container). Needs a detector and a suggestion sink.</summary>
    public bool SuggestLoanwords { get; init; }

    /// <summary>
    /// Give Greek words typed without accents their accented spelling (ειναι → είναι), at the
    /// next pause in typing (<see cref="TypingSession.OnPause"/>). Words converted from the
    /// wrong layout get their accents at once, whatever this says.
    /// </summary>
    public bool RestoreAccents { get; init; }

    /// <summary>Learn from accepted and undone loanword suggestions (off when IT policy disables learning).</summary>
    public bool LearnLoanwords { get; init; } = true;

    /// <summary>
    /// Debug builds only: include the words themselves in debug log events. Ignored, and
    /// compiled out, in Release builds.
    /// </summary>
    public bool DebugLogText { get; init; }
}
