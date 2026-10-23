namespace GreekSwitch.Core.Scoring;

/// <summary>
/// Tunable weights and thresholds. A reading scores
/// <c>(isWord ? DictionaryWeight : 0) + NgramWeight * logProb + contextPrior</c>, where
/// contextPrior = <c>ContextWeight</c> × the share of the last <c>ContextWindow</c> accepted
/// words that were in that reading's language.
/// </summary>
public sealed record ScoringOptions
{
    public double DictionaryWeight { get; init; } = 2.0;

    public double NgramWeight { get; init; } = 1.0;

    public double ContextWeight { get; init; } = 0.3;

    public int ContextWindow { get; init; } = 3;

    /// <summary>
    /// Replace when the alternative's combined score beats the current reading's by more than
    /// this, even if neither reading is a dictionary word.
    /// </summary>
    public double ReplaceThreshold { get; init; } = 2.0;

    /// <summary>Words with fewer keys are left alone unless the dictionary and context both say otherwise.</summary>
    public int MinLength { get; init; } = 3;
}
