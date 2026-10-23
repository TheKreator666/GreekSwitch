using GreekSwitch.Core.Layout;

namespace GreekSwitch.Core.Input;

public enum KeyAction
{
    /// <summary>Modifier or lock key on its own: no effect on the word.</summary>
    Ignore,

    /// <summary>Part of a word: goes into the buffer.</summary>
    Character,

    Backspace,

    /// <summary>Ends a word: Space, Enter, Tab or punctuation.</summary>
    Delimiter,

    /// <summary>The caret may have moved or a shortcut ran: forget the current word.</summary>
    Reset,
}

public enum DelimiterKind
{
    Space,
    Enter,
    Tab,
    Punctuation,
}

/// <summary>The key that ended a word and the text it typed, so the corrector can re-type it.</summary>
public sealed record Delimiter(DelimiterKind Kind, string Text);

public readonly record struct KeyClassification(KeyAction Action, Delimiter? Delimiter = null);

/// <summary>
/// Decides what a key press means for the word buffer. Punctuation only ends a word when both
/// layouts produce the same mark, so keys that differ (the Greek tonos key is ';' in English,
/// and Q is ';' in Greek) stay inside the word and are re-interpreted with it.
/// </summary>
public sealed class KeyClassifier
{
    private const string PunctuationDelimiters = ".,!?()[]{}\"-";

    private static readonly Delimiter SpaceDelimiter = new(DelimiterKind.Space, " ");
    private static readonly Delimiter EnterDelimiter = new(DelimiterKind.Enter, "\n");
    private static readonly Delimiter TabDelimiter = new(DelimiterKind.Tab, "\t");

    private readonly LayoutMap _greek;
    private readonly LayoutMap _english;

    public KeyClassifier(LayoutMap greek, LayoutMap english)
    {
        _greek = greek;
        _english = english;
    }

    public KeyClassification Classify(KeyInput key)
    {
        var vk = key.VirtualKey;
        if (VirtualKeys.IsModifier(vk))
        {
            return new(KeyAction.Ignore);
        }

        // Ctrl/Alt/Win combos (including AltGr = Ctrl+Alt) are shortcuts, not typing.
        if (key.Control || key.Alt || key.Win)
        {
            return new(KeyAction.Reset);
        }

        switch (vk)
        {
            case VirtualKeys.Back:
                return new(KeyAction.Backspace);
            case VirtualKeys.Space:
                return new(KeyAction.Delimiter, SpaceDelimiter);
            case VirtualKeys.Return:
                return new(KeyAction.Delimiter, EnterDelimiter);
            case VirtualKeys.Tab:
                return new(KeyAction.Delimiter, TabDelimiter);
        }

        var stroke = key.ToStroke();
        if (!_greek.TryMap(stroke, out var greek) || !_english.TryMap(stroke, out var english))
        {
            // Arrows, Home/End, Delete, Esc, F-keys, numpad...: the caret may have moved.
            return new(KeyAction.Reset);
        }

        if (!greek.IsDeadKey && !english.IsDeadKey && greek.Character == english.Character
            && PunctuationDelimiters.Contains(english.Character, StringComparison.Ordinal))
        {
            return new(KeyAction.Delimiter, new Delimiter(DelimiterKind.Punctuation, english.Character.ToString()));
        }

        return new(KeyAction.Character);
    }
}
