namespace GreekSwitch.Windows.Output;

public enum TextMatch
{
    /// <summary>The text before the caret is exactly what we expect to replace.</summary>
    Match,

    /// <summary>It differs: something else (AutoCorrect, text suggestions...) changed it. Don't touch it.</summary>
    Mismatch,

    /// <summary>Can't tell (no TextPattern, timeout): rely on the other guards.</summary>
    Unknown,
}

/// <summary>Checks the text just before the caret in the focused control.</summary>
public interface ITextVerifier
{
    TextMatch BeforeCaret(string expected);
}

/// <summary>No verification available: every answer is Unknown.</summary>
public sealed class NoTextVerifier : ITextVerifier
{
    public TextMatch BeforeCaret(string expected) => TextMatch.Unknown;
}
