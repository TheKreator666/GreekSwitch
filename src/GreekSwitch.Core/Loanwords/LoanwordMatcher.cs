namespace GreekSwitch.Core.Loanwords;

/// <summary>An English word that sounds like the Greek one.</summary>
public sealed record LoanwordMatch(string Word, double Score, double Zipf);

/// <summary>
/// Finds the English words a Greek word sounds like: candidates from the skeleton and deletion
/// indexes, scored by weighted phonetic distance, near-ties ranked by English frequency.
/// Stateless apart from the immutable index, so safe to call from any thread.
/// </summary>
public sealed class LoanwordMatcher
{
    private readonly PhoneticIndex _index;

    public LoanwordMatcher(PhoneticIndex index)
    {
        _index = index;
    }

    public PhoneticIndex Index => _index;

    /// <summary>
    /// The best matches scoring at least <paramref name="minScore"/>, best first: the highest
    /// score, then the near-ties within <see cref="LoanwordOptions.TieMargin"/> of it by Zipf,
    /// then the rest by score. One result per English word.
    /// </summary>
    public IReadOnlyList<LoanwordMatch> Match(PhoneticWord greek, LoanwordOptions options, double minScore, int max = 5)
    {
        var skeleton = PhoneticSkeleton.Of(greek.Phonemes);
        if (skeleton.Length == 0 || greek.Length > PhoneticDistance.MaxLength)
        {
            return [];
        }

        var candidates = new HashSet<int>();
        _index.CollectCandidates(skeleton, candidates);

        var best = new Dictionary<int, double>();
        foreach (var entry in candidates)
        {
            var english = _index.Phonemes(entry);

            // Each length difference costs at least one cheap insertion: skip hopeless candidates.
            var longest = Math.Max(greek.Length, english.Length);
            var floor = Math.Abs(greek.Length - english.Length) * Math.Min(options.VowelIndel, options.ConsonantEdit);
            if (1 - (floor / longest) + options.StressBonus < minScore)
            {
                continue;
            }

            var score = PhoneticDistance.Score(greek, english, _index.StressFromEnd(entry), options);
            var word = _index.WordOf(entry);
            if (score >= minScore && (!best.TryGetValue(word, out var previous) || score > previous))
            {
                best[word] = score;
            }
        }

        return Rank(best.Select(kv => (Word: kv.Key, Score: kv.Value)), options.TieMargin, max);
    }

    private IReadOnlyList<LoanwordMatch> Rank(IEnumerable<(int Word, double Score)> scored, double tieMargin, int max)
    {
        var ordered = scored.OrderByDescending(s => s.Score).ThenByDescending(s => _index.Zipf(s.Word)).ToList();
        if (ordered.Count == 0)
        {
            return [];
        }

        var top = ordered[0].Score;
        return [.. ordered
            .Select((s, i) => (s.Word, s.Score, Tie: s.Score >= top - tieMargin, Order: i))
            .OrderByDescending(s => s.Tie)
            .ThenByDescending(s => s.Tie ? _index.Zipf(s.Word) : 0)
            .ThenBy(s => s.Order)
            .Take(max)
            .Select(s => new LoanwordMatch(_index.Word(s.Word), s.Score, _index.Zipf(s.Word)))];
    }
}
