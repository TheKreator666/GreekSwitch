using System.Globalization;
using GreekSwitch.Core;
using GreekSwitch.Core.Scoring;

namespace GreekSwitch.Evaluation.Support;

/// <summary>Real dictionaries and trigram models, loaded once for all evaluation tests.</summary>
public sealed class EvaluationData : ILanguageData
{
    public EvaluationData()
    {
        var dictionaries = HunspellDictionaryService.FromEmbeddedResources();
        dictionaries.Preload();
        Dictionaries = dictionaries;
        Trigrams = TrigramModels.FromEmbeddedResources();
    }

    public IDictionaryService Dictionaries { get; }

    public ITrigramModel Trigrams { get; }

    public static string DataPath(params string[] parts) =>
        Path.Combine([AppContext.BaseDirectory, "Data", .. parts]);

    /// <summary>The <paramref name="top"/> most frequent words of a language: word and Zipf frequency.</summary>
    public static IReadOnlyList<(string Word, double Zipf)> Frequencies(Lang lang, int top) =>
        File.ReadLines(DataPath("frequency", lang == Lang.Greek ? "el.tsv" : "en.tsv"))
            .Where(l => l.Length > 0 && l[0] != '#')
            .Select(l => l.Split('\t'))
            .Select(p => (p[0], double.Parse(p[1], CultureInfo.InvariantCulture)))
            .Take(top)
            .ToList();

    /// <summary>Sampled Tatoeba sentences: id and text.</summary>
    public static IReadOnlyList<(string Id, string Text)> Sentences(Lang lang) =>
        File.ReadLines(DataPath(lang == Lang.Greek ? "sentences.el.tsv" : "sentences.en.tsv"))
            .Where(l => l.Length > 0 && l[0] != '#')
            .Select(l => l.Split('\t', 2))
            .Select(p => (p[0], p[1]))
            .ToList();

    /// <summary>Non-empty, non-comment lines of a data file.</summary>
    public static IEnumerable<string> Lines(string file) =>
        File.ReadLines(DataPath(file)).Where(l => l.Trim().Length > 0 && !l.TrimStart().StartsWith('#'));
}

[CollectionDefinition(Name)]
public sealed class EvaluationGroup : ICollectionFixture<EvaluationData>
{
    public const string Name = "Evaluation data";
}
