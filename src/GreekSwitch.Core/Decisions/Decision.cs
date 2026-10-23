namespace GreekSwitch.Core.Decisions;

public enum KeepReason
{
    /// <summary>Digits, @, /, :, _ or similar, mixed camelCase, a domain after "x.", or no letters (rule 1).</summary>
    CodeLike,

    /// <summary>On the user's never-convert list (rule 2).</summary>
    NeverConvert,

    /// <summary>Shorter than MinLength and the short-word exception did not apply (rule 3).</summary>
    TooShort,

    /// <summary>The current reading is at least as plausible as the alternative (rule 5).</summary>
    Plausible,

    /// <summary>Backspace went past the start of the word, so it is incomplete.</summary>
    PartialWord,
}

public enum ReplaceReason
{
    /// <summary>Current reading is not a word, the alternative is.</summary>
    Dictionary,

    /// <summary>The alternative's combined score wins by more than ReplaceThreshold.</summary>
    ScoreMargin,

    /// <summary>A short word whose alternative is a word that context also favours.</summary>
    ShortWordInContext,

    /// <summary>Both readings are words; context favours the other language and it is far more frequent.</summary>
    FrequencyInContext,
}

/// <summary>Outcome of evaluating one completed word.</summary>
public abstract record Decision;

/// <summary>Leave the word as typed.</summary>
public sealed record Keep(KeepReason Reason) : Decision;

/// <summary>Replace <see cref="Original"/> (what is on screen) with <see cref="Text"/> and switch to <see cref="TargetLayout"/>.</summary>
public sealed record Replace(string Original, string Text, Lang SourceLayout, Lang TargetLayout, ReplaceReason Reason) : Decision;
