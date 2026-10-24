using System.Diagnostics;
using GreekSwitch.Core.Loanwords;
using GreekSwitch.Core.Tests.Support;
using Xunit.Abstractions;

namespace GreekSwitch.Core.Tests.Loanwords;

[Collection(LanguageDataGroup.Name)]
public class LoanwordMatcherTests(LanguageDataFixture data, ITestOutputHelper output)
{
    private static readonly LoanwordOptions Options = new();

    /// <summary>A fixed list for the timing test: loanwords, ordinary Greek, short and long words.</summary>
    private static readonly string[] TimingWords =
    [
        "κοντέινερ", "κομπιούτερ", "σέρβερ", "ντεπλόι", "καλημέρα", "σπίτι", "πράξεις", "λάπτοπ", "μίτινγκ",
        "πρότζεκτ", "κόμιτ", "ρίλιζ", "ίμεϊλ", "φάιλ", "σάιτ", "σόφτγουερ", "μάνατζερ", "μπλογκ", "λινκ",
        "γιούζερ", "ντελίτ", "άπντεϊτ", "μπράουζερ", "πάσγουορντ", "φίντμπακ", "μάρκετινγκ", "πρίντερ", "τεστ",
        "τσατ", "σκριπτ", "θάλασσα", "ευχαριστώ", "υπολογιστής", "αλγόριθμος", "γάτα", "τραπέζι", "δουλειά",
        "εργασία", "πρόβλημα", "κυβέρνηση", "αποτέλεσμα", "μέρα", "ώρα", "λέξη", "ελληνικά", "ταυτότητα",
    ];

    private LoanwordMatcher Matcher => new(data.Phonetic);

    [Theory]
    [InlineData("κοντέινερ", "container")]
    [InlineData("κομπιούτερ", "computer")]
    [InlineData("σέρβερ", "server")]
    [InlineData("ντεπλόι", "deploy")]
    [InlineData("λάπτοπ", "laptop")]
    [InlineData("μίτινγκ", "meeting")]
    [InlineData("κόμιτ", "commit")]
    [InlineData("λινκ", "link")]
    [InlineData("ΣΕΡΒΕΡ", "server")]
    public void Finds_the_English_word_a_transliteration_sounds_like(string greek, string english)
    {
        var matches = Matcher.Match(GreekPhonetics.Transcribe(greek)!, Options, Options.SuggestThreshold);

        matches.Should().NotBeEmpty();
        matches[0].Word.Should().Be(english);
    }

    [Fact]
    public void Ranks_near_ties_by_English_frequency()
    {
        // σάιτ sounds exactly like site, sight and cite; site is the most frequent.
        var matches = Matcher.Match(GreekPhonetics.Transcribe("σάιτ")!, Options, Options.SuggestThreshold);

        matches.Select(m => m.Word).Should().StartWith(["site", "sight", "cite"]);
    }

    [Fact]
    public void Returns_one_result_per_word_best_first()
    {
        var matches = Matcher.Match(GreekPhonetics.Transcribe("σέρβερ")!, Options, 0.8, max: 10);

        matches.Select(m => m.Word).Should().OnlyHaveUniqueItems();
        matches.Should().HaveCountLessThanOrEqualTo(10);
        matches[0].Score.Should().BeGreaterThan(1.0, "an exact match with matching stress");
    }

    [Fact]
    public void Median_lookup_takes_under_5_ms()
    {
        var matcher = Matcher;
        var words = TimingWords.Select(w => GreekPhonetics.Transcribe(w)!).ToList();
        foreach (var word in words)
        {
            matcher.Match(word, Options, Options.SuggestThreshold); // warm up
        }

        var times = new List<double>();
        for (var round = 0; round < 5; round++)
        {
            foreach (var word in words)
            {
                var watch = Stopwatch.StartNew();
                matcher.Match(word, Options, Options.SuggestThreshold);
                times.Add(watch.Elapsed.TotalMilliseconds);
            }
        }

        times.Sort();
        output.WriteLine($"Loanword lookup over {times.Count} runs: median {times[times.Count / 2]:F3} ms, 90th percentile {times[times.Count * 9 / 10]:F3} ms, max {times[^1]:F3} ms");
        times[times.Count / 2].Should().BeLessThan(5.0, "the per-word budget is generous, but scoring must stay within the skeleton buckets");
    }
}
