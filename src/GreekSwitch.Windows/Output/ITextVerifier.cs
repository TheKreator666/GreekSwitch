namespace GreekSwitch.Windows.Output;

public enum TextMatch
{
    /// <summary>The text before the caret is exactly what we expect to replace.</summary>
    Match,

    /// <summary>
    /// The same text, except that the app capitalised its first letter (Word's AutoCorrect does this
    /// to the first word of a sentence when Space is pressed). Safe to replace: the length is the
    /// same. The replacement keeps the capital.
    /// </summary>
    FirstLetterCapitalised,

    /// <summary>It differs: something else (AutoCorrect, text suggestions...) changed it. Don't touch it.</summary>
    Mismatch,

    /// <summary>Can't tell (no TextPattern, timeout): rely on the other guards.</summary>
    Unknown,
}

/// <summary>Checks the text just before the caret in the focused control.</summary>
public interface ITextVerifier
{
    TextMatch BeforeCaret(string expected);

    /// <summary>
    /// The text just before the caret, at least <paramref name="length"/> characters where there are
    /// that many; null if it can't be read. Kept in memory only for the comparison, never logged.
    /// </summary>
    string? ReadBeforeCaret(int length);
}

/// <summary>No verification available: every answer is Unknown.</summary>
public sealed class NoTextVerifier : ITextVerifier
{
    public TextMatch BeforeCaret(string expected) => TextMatch.Unknown;

    public string? ReadBeforeCaret(int length) => null;
}
