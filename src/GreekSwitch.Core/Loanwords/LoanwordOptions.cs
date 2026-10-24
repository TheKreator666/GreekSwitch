namespace GreekSwitch.Core.Loanwords;

/// <summary>
/// Costs and thresholds for loanword matching (docs/loanword-design.md), tuned against the
/// design's test table and the evaluation harness's nuisance rows. A match scores
/// <c>1 - distance / max(length)</c> over the two phoneme sequences, plus
/// <see cref="StressBonus"/> when the stressed vowels line up, so scores run from 0 to
/// 1 + StressBonus.
/// </summary>
public sealed record LoanwordOptions
{
    // ── Edit costs ────────────────────────────────────────────────────────────────────────

    /// <summary>One vowel for another (κ<b>ο</b>ντέινερ vs c<b>o</b>ntainer, where CMU has AH).</summary>
    public double VowelSubstitution { get; init; } = 0.3;

    /// <summary>y for i (κομπ<b>ι</b>ούτερ vs comp<b>y</b>uter).</summary>
    public double SemivowelSubstitution { get; init; } = 0.2;

    /// <summary>Inserting or deleting a vowel or y.</summary>
    public double VowelIndel { get; init; } = 0.5;

    /// <summary>Consonants that differ only in voicing (p/b, t/d, k/g, f/v, s/z, θ/δ).</summary>
    public double SimilarConsonantSubstitution { get; init; } = 0.5;

    /// <summary>u ↔ v, the two ways Greek writes English w.</summary>
    public double WSubstitution { get; init; } = 0.3;

    /// <summary>b ↔ mp/mb, d ↔ nt/nd, g ↔ ng/nk: how μπ, ντ, γκ are read either way.</summary>
    public double ClusterConfusion { get; init; } = 0.3;

    /// <summary>Any other substitution, and a consonant inserted or deleted.</summary>
    public double ConsonantEdit { get; init; } = 1.0;

    /// <summary>Added when the tonos and CMUdict's primary stress fall on the same vowel (counted from the end).</summary>
    public double StressBonus { get; init; } = 0.05;

    // ── Ranking ───────────────────────────────────────────────────────────────────────────

    /// <summary>Matches within this much of the best score are ties, ranked by English Zipf frequency.</summary>
    public double TieMargin { get; init; } = 0.015;

    /// <summary>Added to a target the user has accepted for this word before.</summary>
    public double AcceptedBonus { get; init; } = 0.05;

    // ── Deciding ──────────────────────────────────────────────────────────────────────────

    /// <summary>Greek words with fewer letters are never considered.</summary>
    public int MinLetters { get; init; } = 4;

    /// <summary>Tier 3: show a suggestion from this score on.</summary>
    public double SuggestThreshold { get; init; } = 0.85;

    /// <summary>Tier 2: replace from this score on (with the other tier-2 conditions).</summary>
    public double StrongThreshold { get; init; } = 0.93;

    /// <summary>
    /// Tier 2, off by default: replace a strong match automatically when the Greek word is not
    /// in the Greek dictionary and the target is on the domain vocabulary list.
    /// </summary>
    public bool AutoReplaceStrongMatches { get; init; }

    /// <summary>
    /// A Greek word at least this frequent (Zipf) is established Greek and is never matched:
    /// ordinary words (σπίτι 5.7, πράξεις 4.8) and loanwords Greek has adopted (τεστ 4.7,
    /// ίντερνετ 4.4), where a suggestion would only be in the way. Ad-hoc transliterations are
    /// rarer (κομπιούτερ 3.5, κοντέινερ 3.3) or not in the table at all (σέρβερ, ντεπλόι).
    /// </summary>
    public double MaxGreekZipf { get; init; } = 4.2;

    /// <summary>
    /// When the Greek word is in the frequency table, the target must be at least this much more
    /// frequent in English than the Greek word is in Greek (κομπιούτερ 3.5 vs computer 5.0).
    /// </summary>
    public double MinEnglishZipfAdvantage { get; init; } = 0.5;

    /// <summary>
    /// Targets more frequent than this are function words (many, much, very, now), which Greeks
    /// don't transliterate, so a match to one is a coincidence (μένει, ματς).
    /// </summary>
    public double MaxEnglishZipf { get; init; } = 5.5;

    /// <summary>
    /// A word in the Greek dictionary is only considered if it ends the way Greek words don't: in
    /// a consonant other than ς or ν (κοντέινερ, μίτινγκ, πρότζεκτ). Greek words end in a vowel,
    /// ς or ν (καλός, βόρειας, όρια), whatever English word they happen to sound like.
    /// </summary>
    public bool DictionaryWordsNeedForeignEnding { get; init; } = true;

    /// <summary>Tier 1 promotion: a pair accepted this many times is replaced automatically.</summary>
    public int PromoteAfterAccepts { get; init; } = 3;

    /// <summary>A suggestion rejected (undone or dismissed explicitly) this many times is no longer shown.</summary>
    public int SuppressAfterRejects { get; init; } = 3;
}
