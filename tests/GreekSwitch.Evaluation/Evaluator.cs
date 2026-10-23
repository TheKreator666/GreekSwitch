using GreekSwitch.Core;
using GreekSwitch.Core.Decisions;
using GreekSwitch.Evaluation.Support;

namespace GreekSwitch.Evaluation;

/// <summary>
/// Runs the decision engine over frequency-weighted word lists and real sentences. Measures:
/// "wrongly changed" (a correctly typed word was replaced: false positives, the costly error)
/// and "corrected" (a word typed in the wrong layout came out right).
/// </summary>
public static class Evaluator
{
    public const int TopWords = 5000;

    /// <param name="data">The language data the engine under test uses.</param>
    /// <param name="caseReference">
    /// Decides how list words are capitalised (see <see cref="AsTyped"/>); fixed across runs so
    /// before/after comparisons type the same text. Defaults to <paramref name="data"/>.
    /// </param>
    public static IReadOnlyList<Tally> Run(ILanguageData data, ILanguageData? caseReference = null) =>
        [.. WordLevel(data, caseReference: caseReference), LatinInGreek(data), .. Sentences(data)];

    public static IEnumerable<Tally> WordLevel(ILanguageData data, int top = TopWords, ILanguageData? caseReference = null)
    {
        caseReference ??= data;
        var engine = EngineFactory.Create(data);
        foreach (var lang in new[] { Lang.Greek, Lang.English })
        {
            var other = lang.Other();
            var correct = new Tally($"{Name(lang)} typed correctly, {Name(lang)} context", "wrongly changed");
            var noContext = new Tally($"{Name(lang)} typed in the {other.Code()} layout, no context", "corrected");
            var context = new Tally($"{Name(lang)} typed in the {other.Code()} layout, {Name(lang)} context", "corrected");

            foreach (var (listed, zipf) in EvaluationData.Frequencies(lang, top))
            {
                var word = AsTyped(listed, lang, caseReference);
                if (TryType(word, lang) is not { } keys)
                {
                    continue;
                }

                var weight = Math.Pow(10, zipf);

                engine.SeedContext(lang);
                var kept = engine.Decide(keys, lang);
                correct.Add(weight, kept is Replace, kept is Replace r ? $"{word}→{r.Text}" : null);

                engine.ClearContext();
                Score(noContext, engine.Decide(keys, other), word, weight);

                engine.SeedContext(lang);
                Score(context, engine.Decide(keys, other), word, weight);
            }

            yield return correct;
            yield return noContext;
            yield return context;
        }
    }

    /// <summary>English and Latin-script tokens typed on purpose inside Greek prose (office jargon, brands).</summary>
    public static Tally LatinInGreek(ILanguageData data)
    {
        var engine = EngineFactory.Create(data);
        var tally = new Tally("Latin tokens inside Greek prose (EN layout, Greek context)", "wrongly changed");
        foreach (var token in EvaluationData.Lines("latin-in-greek.txt").Select(l => l.Trim()))
        {
            if (TryType(token, Lang.English) is not { } keys)
            {
                continue;
            }

            engine.SeedContext(Lang.Greek);
            var decision = engine.Decide(keys, Lang.English);
            tally.Add(1, decision is Replace, decision is Replace r ? $"{token}→{r.Text}" : null);
        }

        return tally;
    }

    /// <summary>
    /// Each sentence is typed with its intended keys; from a random word on, the wrong layout is
    /// active, as when the user forgot to switch. Layout switches made by corrections apply to
    /// the following words, as in the real app.
    /// </summary>
    public static IEnumerable<Tally> Sentences(ILanguageData data)
    {
        foreach (var lang in new[] { Lang.Greek, Lang.English })
        {
            var corrected = new Tally($"Sentences ({Name(lang)}): words typed in the wrong layout", "corrected");
            var changed = new Tally($"Sentences ({Name(lang)}): words typed correctly", "wrongly changed");

            foreach (var (id, text) in EvaluationData.Sentences(lang))
            {
                var tokens = text.Split(' ');
                var start = new Random(int.Parse(id, System.Globalization.CultureInfo.InvariantCulture)).Next(tokens.Length);
                var sim = new TypingSimulator(data, followLayoutSwitches: true).SetActiveLayout(lang);

                var typed = new List<(string Word, bool WrongLayout)>();
                if (start > 0)
                {
                    typed.AddRange(sim.TypeIntended(string.Join(' ', tokens[..start]) + " ", lang));
                }

                sim.SetActiveLayout(lang.Other());
                typed.AddRange(sim.TypeIntended(string.Join(' ', tokens[start..]) + " ", lang));

                var result = Words(sim.Screen);
                for (var i = 0; i < typed.Count; i++)
                {
                    var (word, wrong) = typed[i];
                    var final = i < result.Count ? result[i] : "";
                    if (wrong)
                    {
                        corrected.Add(1, final == word, final == word ? null : $"{word}≠{final}");
                    }
                    else
                    {
                        changed.Add(1, final != word, final != word ? $"{word}→{final}" : null);
                    }
                }
            }

            yield return corrected;
            yield return changed;
        }
    }

    /// <summary>
    /// Frequency lists are lowercase, but people capitalise names: a word whose only dictionary
    /// entry is capitalised ("syria" → Syria, "τραμπ" stays: not in the dictionary at all) is
    /// typed capitalised, as in running text.
    /// </summary>
    private static string AsTyped(string word, Lang lang, ILanguageData data)
    {
        if (word.Length == 0 || data.Dictionaries.IsWord(word, lang))
        {
            return word;
        }

        var capitalised = char.ToUpperInvariant(word[0]) + word[1..];
        return data.Dictionaries.IsWord(capitalised, lang) ? capitalised : word;
    }

    private static void Score(Tally tally, Decision decision, string intended, double weight)
    {
        var ok = decision is Replace r && r.Text == intended;
        tally.Add(weight, ok, ok ? null : decision is Replace wrong ? $"{intended}≠{wrong.Text}" : intended);
    }

    private static IReadOnlyList<KeyStroke>? TryType(string word, Lang lang)
    {
        try
        {
            return Typist.Type(word, lang);
        }
        catch (ArgumentException)
        {
            return null; // a character this layout can't type
        }
    }

    private static List<string> Words(string text) =>
        [.. text.Split(TypingSimulator.DelimiterChars.ToCharArray(), StringSplitOptions.RemoveEmptyEntries)];

    private static string Name(Lang lang) => lang == Lang.Greek ? "Greek" : "English";
}
