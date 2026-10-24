using GreekSwitch.Core;
using GreekSwitch.Core.Loanwords;
using GreekSwitch.Evaluation.Support;

namespace GreekSwitch.Evaluation;

/// <summary>
/// Runs the loanword detector (milestone 15) over ordinary Greek and over English words written
/// in Greek letters. "suggested" on ordinary Greek is the nuisance rate: a popup the user didn't
/// want. "wrongly changed" is tier 2 (switched on here) replacing an ordinary Greek word.
/// Rates are per word typed: words too short for the detector count as words without a popup.
/// The word list is the whole Greek frequency list (≈43k words, Zipf ≥ 3), not just the top
/// 5,000: those are all frequent enough to be skipped as established Greek
/// (<see cref="LoanwordOptions.MaxGreekZipf"/>), and loanwords live in the rarer part. It also
/// contains real transliterations (κομπιούτερ), so the rate is an upper bound.
/// </summary>
public static class LoanwordEvaluator
{
    public static IEnumerable<Tally> Run(ILanguageData data, PhoneticIndex index)
    {
        var suggestOnly = Create(data, index, new LoanwordOptions());
        var tier2 = Create(data, index, new LoanwordOptions { AutoReplaceStrongMatches = true });

        var suggested = new Tally("Loanwords: Greek frequency list, per word", "suggested");
        var replaced = new Tally("Loanwords, tier 2 on: Greek frequency list, per word", "wrongly changed");
        foreach (var (listed, zipf) in EvaluationData.FrequencyList(Lang.Greek, int.MaxValue))
        {
            var word = Evaluator.AsTyped(listed, Lang.Greek, data);
            var weight = Math.Pow(10, zipf);
            var result = suggestOnly.Check(word);
            suggested.Add(weight, result is LoanwordSuggest, result is LoanwordSuggest s ? $"{word}→{s.Target}" : null);

            var strong = tier2.Check(word);
            replaced.Add(weight, strong is LoanwordReplace, strong is LoanwordReplace r ? $"{word}→{r.Target}" : null);
        }

        yield return suggested;
        yield return replaced;

        var inSentences = new Tally("Loanwords: Greek sentences, per Greek word", "suggested");
        foreach (var (_, text) in EvaluationData.Sentences(Lang.Greek))
        {
            foreach (var token in text.Split(' '))
            {
                var word = token.Trim(token.Where(c => !char.IsLetter(c)).Distinct().ToArray());
                if (word.Length == 0 || GreekPhonetics.Transcribe(word) is null)
                {
                    continue; // not a Greek word: never reaches the detector
                }

                var result = suggestOnly.Check(word);
                inSentences.Add(1, result is LoanwordSuggest, result is LoanwordSuggest s ? $"{word}→{s.Target}" : null);
            }
        }

        yield return inSentences;

        var matched = new Tally("Loanwords: English words written in Greek (Data/loanwords.tsv)", "right suggestion");
        var autoReplaced = new Tally("Loanwords, tier 2 on: English words written in Greek (Data/loanwords.tsv)", "replaced right");
        foreach (var line in EvaluationData.Lines("loanwords.tsv"))
        {
            var (greek, english) = (line.Split('\t')[0], line.Split('\t')[1]);
            var result = suggestOnly.Check(greek);
            var ok = result is LoanwordSuggest s && s.Target == english;
            matched.Add(1, ok, ok ? null : $"{greek}≠{Describe(result)}");

            var strong = tier2.Check(greek);
            autoReplaced.Add(1, strong is LoanwordReplace r && r.Target == english, strong is LoanwordReplace ? null : greek);
        }

        yield return matched;
        yield return autoReplaced;
    }

    private static LoanwordDetector Create(ILanguageData data, PhoneticIndex index, LoanwordOptions options)
    {
        var detector = new LoanwordDetector(new LoanwordPairStore(), data.Dictionaries, data.Frequencies, options: options);
        detector.UseIndex(index);
        return detector;
    }

    private static string Describe(LoanwordResult result) => result switch
    {
        LoanwordSuggest s => s.Target,
        LoanwordReplace r => r.Target,
        _ => "nothing",
    };
}
