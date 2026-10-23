using System.Text;
using GreekSwitch.Core.Data;
using GreekSwitch.Core.Decisions;
using GreekSwitch.Core.Layout;
using GreekSwitch.Core.Scoring;
using GreekSwitch.Core.Tests.Support;
using Xunit.Abstractions;

namespace GreekSwitch.Core.Tests.Decisions;

/// <summary>
/// Real dictionary words typed in the right layout must stay as they are. A deterministic sample
/// of each dictionary goes through the engine, and the rate of wrong replacements must stay tiny.
/// </summary>
[Collection(LanguageDataGroup.Name)]
public class FalsePositiveTests(LanguageDataFixture data, ITestOutputHelper output)
{
    [Theory]
    [InlineData(Lang.Greek, EmbeddedData.GreekDictionary, "ISO-8859-7", 400)]
    [InlineData(Lang.English, EmbeddedData.EnglishDictionary, "UTF-8", 20)]
    public void Correctly_typed_dictionary_words_are_rarely_replaced(Lang lang, string resource, string encoding, int every)
    {
        var words = SampleWords(resource, encoding, every, lang);
        var engine = new DecisionEngine(
            new DualDecoder(StaticLayouts.Greek, StaticLayouts.English),
            new Scorer(data.Dictionaries, data.Trigrams),
            new NeverConvertList());

        var replaced = new List<string>();
        foreach (var word in words)
        {
            engine.ClearContext();
            if (engine.Decide(Typist.Type(word, lang), lang) is Replace r)
            {
                replaced.Add($"{word} → {r.Text}");
            }
        }

        var rate = (double)replaced.Count / words.Count;
        output.WriteLine($"{lang}: {replaced.Count}/{words.Count} replaced ({rate:P2})");
        foreach (var line in replaced.Take(40))
        {
            output.WriteLine("  " + line);
        }

        rate.Should().BeLessThan(0.005);
    }

    [Theory]
    [InlineData(Lang.Greek, EmbeddedData.GreekDictionary, "ISO-8859-7", 400)]
    [InlineData(Lang.English, EmbeddedData.EnglishDictionary, "UTF-8", 20)]
    public void Dictionary_words_typed_in_the_wrong_layout_are_mostly_corrected(Lang lang, string resource, string encoding, int every)
    {
        var words = SampleWords(resource, encoding, every, lang);
        var engine = new DecisionEngine(
            new DualDecoder(StaticLayouts.Greek, StaticLayouts.English),
            new Scorer(data.Dictionaries, data.Trigrams),
            new NeverConvertList());

        var missed = new List<string>();
        var wrong = new List<string>();
        foreach (var word in words)
        {
            engine.ClearContext();
            var decision = engine.Decide(Typist.Type(word, lang), lang.Other());
            if (decision is not Replace r)
            {
                missed.Add(word);
            }
            else if (r.Text != (lang == Lang.Greek ? Core.Text.GreekText.FixFinalSigma(word) : word))
            {
                wrong.Add($"{word} → {r.Text}");
            }
        }

        var recall = 1 - ((double)missed.Count / words.Count);
        output.WriteLine($"{lang} typed in {lang.Other()}: corrected {recall:P2}; missed {missed.Count}, wrong text {wrong.Count}");
        output.WriteLine("  missed: " + string.Join(", ", missed.Take(40)));
        output.WriteLine("  wrong: " + string.Join(", ", wrong.Take(20)));

        recall.Should().BeGreaterThan(0.95);
        wrong.Should().BeEmpty();
    }

    private static List<string> SampleWords(string resource, string encoding, int every, Lang lang)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var reader = new StreamReader(EmbeddedData.Open(resource), Encoding.GetEncoding(encoding));
        reader.ReadLine(); // entry count
        var words = new List<string>();
        var index = 0;
        while (reader.ReadLine() is { } line)
        {
            if (index++ % every != 0)
            {
                continue;
            }

            var word = line.Split('/', '\t', ' ')[0];
            var typeable = lang == Lang.Greek
                ? word.All(c => char.IsLetter(c) && c is >= 'Ά' and <= 'ώ')
                : word.All(c => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z'));
            if (word.Length >= 3 && typeable && !word.Skip(1).Any(char.IsUpper))
            {
                words.Add(word);
            }
        }

        return words;
    }
}
