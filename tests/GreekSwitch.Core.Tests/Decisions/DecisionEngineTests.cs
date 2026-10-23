using GreekSwitch.Core.Decisions;
using GreekSwitch.Core.Layout;
using GreekSwitch.Core.Scoring;
using GreekSwitch.Core.Text;
using GreekSwitch.Core.Tests.Support;

namespace GreekSwitch.Core.Tests.Decisions;

/// <summary>The decision rules in isolation, with a fake dictionary and a flat trigram model.</summary>
public class DecisionEngineTests
{
    private readonly FakeDictionary _dictionary = new();
    private readonly FakeTrigrams _trigrams = new();
    private readonly FakeFrequency _frequency = new();
    private readonly NeverConvertList _neverConvert = new();
    private readonly DecisionEngine _engine;

    public DecisionEngineTests()
    {
        _engine = new DecisionEngine(
            new DualDecoder(StaticLayouts.Greek, StaticLayouts.English),
            new Scorer(_dictionary, _trigrams, _frequency),
            _neverConvert);
    }

    /// <summary>"den" (Danish/lair, Zipf ~4) typed in Greek text means "δεν" (Zipf ~7).</summary>
    private void BothAreWords(string english, double englishZipf, string greek, double greekZipf)
    {
        _dictionary.Add(Lang.English, english);
        _dictionary.Add(Lang.Greek, greek);
        _frequency.Set(Lang.English, english, englishZipf);
        _frequency.Set(Lang.Greek, greek, greekZipf);
    }

    [Fact]
    public void Both_words_the_far_more_frequent_one_wins_when_context_agrees()
    {
        BothAreWords("den", 4.0, "δεν", 7.0);
        _engine.SeedContext(Lang.Greek);

        Decide("den", Lang.English).Should().BeEquivalentTo(
            new Replace("den", "δεν", Lang.English, Lang.Greek, ReplaceReason.FrequencyInContext));
    }

    [Fact]
    public void Both_words_without_context_are_kept()
    {
        BothAreWords("den", 4.0, "δεν", 7.0);

        Decide("den", Lang.English).Should().BeOfType<Keep>();
    }

    [Fact]
    public void Both_words_with_context_for_the_current_language_are_kept()
    {
        BothAreWords("den", 4.0, "δεν", 7.0);
        _engine.SeedContext(Lang.English);

        Decide("den", Lang.English).Should().BeOfType<Keep>();
    }

    [Fact]
    public void Both_words_need_a_clear_frequency_margin()
    {
        BothAreWords("to", 7.43, "το", 7.48);
        _engine.SeedContext(Lang.Greek);

        Decide("to", Lang.English).Should().BeOfType<Keep>();
    }

    [Fact]
    public void Single_letters_are_never_replaced_on_frequency()
    {
        BothAreWords("h", 3.0, "η", 7.2);
        _engine.SeedContext(Lang.Greek);

        Decide("h", Lang.English).Should().BeOfType<Keep>();
    }

    private Decision Decide(string typed, Lang layout) => _engine.Decide(Typist.Type(typed, layout), layout);

    [Fact]
    public void Replaces_when_only_the_alternative_is_a_word()
    {
        _dictionary.Add(Lang.English, "hello");

        Decide("ηελλο", Lang.Greek).Should().BeEquivalentTo(
            new Replace("ηελλο", "hello", Lang.Greek, Lang.English, ReplaceReason.Dictionary));
    }

    [Fact]
    public void Keeps_when_both_readings_are_words()
    {
        _dictionary.Add(Lang.English, "hello");
        _dictionary.Add(Lang.Greek, "ηελλο");

        Decide("ηελλο", Lang.Greek).Should().Be(new Keep(KeepReason.Plausible));
    }

    [Fact]
    public void Replaces_on_a_large_score_margin_even_without_dictionary_words()
    {
        _trigrams.Set(Lang.Greek, "ηελλο", -6.0);
        _trigrams.Set(Lang.English, "hello", -2.0);

        Decide("ηελλο", Lang.Greek).Should().BeOfType<Replace>()
            .Which.Reason.Should().Be(ReplaceReason.ScoreMargin);
    }

    [Fact]
    public void Both_readings_words_are_never_replaced_by_the_trigram_margin()
    {
        _dictionary.Add(Lang.Greek, "ηελλο");
        _dictionary.Add(Lang.English, "hello");
        _trigrams.Set(Lang.Greek, "ηελλο", -20.0); // an absurd margin
        _trigrams.Set(Lang.English, "hello", -1.0);

        Decide("ηελλο", Lang.Greek).Should().Be(new Keep(KeepReason.Plausible));
    }

    [Fact]
    public void A_valid_current_word_is_never_replaced_by_the_trigram_margin()
    {
        _dictionary.Add(Lang.Greek, "ηελλο");
        _trigrams.Set(Lang.Greek, "ηελλο", -20.0);
        _trigrams.Set(Lang.English, "hello", -1.0);

        Decide("ηελλο", Lang.Greek).Should().Be(new Keep(KeepReason.Plausible));
    }

    [Fact]
    public void Keeps_when_the_margin_is_below_the_threshold()
    {
        _trigrams.Set(Lang.Greek, "ηελλο", -4.0);
        _trigrams.Set(Lang.English, "hello", -2.5);

        Decide("ηελλο", Lang.Greek).Should().Be(new Keep(KeepReason.Plausible));
    }

    [Theory]
    [InlineData("v2")]
    [InlineData("user@mail")]
    [InlineData("snake_case")]
    [InlineData("myVariable")]
    [InlineData("a/b")]
    public void Code_like_words_are_kept_before_anything_else(string typed)
    {
        _dictionary.Add(Lang.Greek, "anything");

        Decide(typed, Lang.English).Should().Be(new Keep(KeepReason.CodeLike));
    }

    [Fact]
    public void Never_convert_list_wins_over_the_dictionary()
    {
        _dictionary.Add(Lang.English, "hello");
        _neverConvert.Add("ηελλο");

        Decide("ηελλο", Lang.Greek).Should().Be(new Keep(KeepReason.NeverConvert));
    }

    [Fact]
    public void Short_words_need_context_to_convert()
    {
        _dictionary.Add(Lang.Greek, "θα");

        Decide("ua", Lang.English).Should().Be(new Keep(KeepReason.TooShort));
    }

    [Fact]
    public void Short_word_converts_when_dictionary_and_context_agree()
    {
        _dictionary.Add(Lang.Greek, "θα");
        _frequency.Set(Lang.Greek, "θα", 6.9);
        _dictionary.Add(Lang.Greek, "είπε");
        _dictionary.Add(Lang.Greek, "ότι");
        Decide("είπε", Lang.Greek);
        Decide("ότι", Lang.Greek);
        _engine.ContextLanguage.Should().Be(Lang.Greek);

        Decide("ua", Lang.English).Should().BeEquivalentTo(
            new Replace("ua", "θα", Lang.English, Lang.Greek, ReplaceReason.ShortWordInContext));
    }

    [Fact]
    public void Short_word_needs_a_frequent_alternative()
    {
        // "tv" typed in Greek text: τω is an archaic dictionary entry (Zipf 4.77), not a reason to convert.
        _dictionary.Add(Lang.Greek, "τω");
        _frequency.Set(Lang.Greek, "τω", 4.77);
        _engine.SeedContext(Lang.Greek);

        Decide("tv", Lang.English).Should().Be(new Keep(KeepReason.TooShort));
    }

    [Fact]
    public void Short_valid_word_is_kept_even_in_opposite_context()
    {
        _dictionary.Add(Lang.Greek, "είπε");
        _dictionary.Add(Lang.English, "ok");
        _dictionary.Add(Lang.Greek, "οκ");
        Decide("είπε", Lang.Greek);

        Decide("ok", Lang.English).Should().Be(new Keep(KeepReason.TooShort));
    }

    [Fact]
    public void Greek_replacement_uses_final_sigma()
    {
        _dictionary.Add(Lang.Greek, "σας");

        Decide("was", Lang.English).Should().BeOfType<Replace>().Which.Text.Should().Be("σας");
    }

    [Fact]
    public void Min_length_is_configurable()
    {
        _dictionary.Add(Lang.English, "hello");
        _engine.Options = _engine.Options with { MinLength = 6 };

        Decide("ηελλο", Lang.Greek).Should().Be(new Keep(KeepReason.TooShort));
    }

    private sealed class FakeDictionary : IDictionaryService
    {
        private readonly HashSet<(Lang, string)> _words = [];

        public void Add(Lang lang, string word) => _words.Add((lang, GreekText.NormalizeForMatch(word)));

        public bool IsWord(string word, Lang lang) => _words.Contains((lang, GreekText.NormalizeForMatch(word)));
    }

    private sealed class FakeFrequency : IWordFrequency
    {
        private readonly Dictionary<(Lang, string), double> _zipf = [];

        public void Set(Lang lang, string word, double zipf) => _zipf[(lang, word)] = zipf;

        public double Zipf(string word, Lang lang) => _zipf.GetValueOrDefault((lang, word));
    }

    private sealed class FakeTrigrams : ITrigramModel
    {
        private readonly Dictionary<(Lang, string), double> _scores = [];

        public void Set(Lang lang, string word, double logProb) => _scores[(lang, word)] = logProb;

        public double LogProb(string word, Lang lang) => _scores.GetValueOrDefault((lang, word), -3.0);
    }
}
