namespace GreekSwitch.Core.Loanwords;

/// <summary>
/// Weighted edit distance between two phoneme sequences, with the costs of
/// <see cref="LoanwordOptions"/>. Besides one-for-one edits it knows the one-for-two cluster
/// confusions b ≈ mp/mb, d ≈ nt/nd, g ≈ ng/nk.
/// </summary>
public static class PhoneticDistance
{
    /// <summary>Longer sequences aren't compared (no real word gets near this).</summary>
    public const int MaxLength = 48;

    /// <summary>
    /// <c>1 - distance / max(length)</c>, plus the stress bonus when both stresses are known and
    /// equal. 0 when either side is empty or too long.
    /// </summary>
    public static double Score(PhoneticWord greek, ReadOnlySpan<Phoneme> english, int englishStressFromEnd, LoanwordOptions options)
    {
        var longest = Math.Max(greek.Length, english.Length);
        if (greek.Length == 0 || english.Length == 0 || longest > MaxLength)
        {
            return 0;
        }

        var score = 1 - (Distance(greek.Phonemes, english, options) / longest);
        if (greek.StressFromEnd >= 0 && greek.StressFromEnd == englishStressFromEnd)
        {
            score += options.StressBonus;
        }

        return Math.Max(0, score);
    }

    public static double Distance(ReadOnlySpan<Phoneme> a, ReadOnlySpan<Phoneme> b, LoanwordOptions options)
    {
        if (a.Length > MaxLength || b.Length > MaxLength)
        {
            throw new ArgumentException("Sequence too long.", nameof(a));
        }

        var width = b.Length + 1;
        Span<double> d = stackalloc double[(a.Length + 1) * width];
        d[0] = 0;
        for (var j = 1; j <= b.Length; j++)
        {
            d[j] = d[j - 1] + Indel(b[j - 1], options);
        }

        for (var i = 1; i <= a.Length; i++)
        {
            var row = i * width;
            d[row] = d[row - width] + Indel(a[i - 1], options);
            for (var j = 1; j <= b.Length; j++)
            {
                var best = Math.Min(
                    d[row - width + j] + Indel(a[i - 1], options),
                    d[row + j - 1] + Indel(b[j - 1], options));
                best = Math.Min(best, d[row - width + j - 1] + Substitution(a[i - 1], b[j - 1], options));

                if (j >= 2 && IsCluster(a[i - 1], b[j - 2], b[j - 1]))
                {
                    best = Math.Min(best, d[row - width + j - 2] + options.ClusterConfusion);
                }

                if (i >= 2 && IsCluster(b[j - 1], a[i - 2], a[i - 1]))
                {
                    best = Math.Min(best, d[row - (2 * width) + j - 1] + options.ClusterConfusion);
                }

                d[row + j] = best;
            }
        }

        return d[^1];
    }

    private static double Indel(Phoneme p, LoanwordOptions options) =>
        Phonemes.IsVowelLike(p) ? options.VowelIndel : options.ConsonantEdit;

    private static double Substitution(Phoneme x, Phoneme y, LoanwordOptions options)
    {
        if (x == y)
        {
            return 0;
        }

        if (Phonemes.IsVowelLike(x) && Phonemes.IsVowelLike(y))
        {
            return (x, y) is (Phoneme.Y, Phoneme.I) or (Phoneme.I, Phoneme.Y)
                ? options.SemivowelSubstitution
                : options.VowelSubstitution;
        }

        if ((x, y) is (Phoneme.U, Phoneme.V) or (Phoneme.V, Phoneme.U))
        {
            return options.WSubstitution;
        }

        // γ vs g is not cheap: transliterations write English g as γκ, so a plain γ (γάτα) is Greek.
        return x != Phoneme.Gh && y != Phoneme.Gh
            && !Phonemes.IsVowelLike(x) && !Phonemes.IsVowelLike(y) && Phonemes.SkeletonClass(x) == Phonemes.SkeletonClass(y)
            ? options.SimilarConsonantSubstitution
            : options.ConsonantEdit;
    }

    /// <summary>True if <paramref name="single"/> is a cheap stand-in for the pair (b for mp, d for nt…).</summary>
    private static bool IsCluster(Phoneme single, Phoneme first, Phoneme second) => (single, first, second) switch
    {
        (Phoneme.B, Phoneme.M, Phoneme.P or Phoneme.B) => true,
        (Phoneme.D, Phoneme.N, Phoneme.T or Phoneme.D) => true,
        (Phoneme.G, Phoneme.N, Phoneme.G or Phoneme.K) => true,
        _ => false,
    };
}
