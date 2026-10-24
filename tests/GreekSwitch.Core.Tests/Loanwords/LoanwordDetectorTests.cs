using System.Text;
using GreekSwitch.Core.Decisions;
using GreekSwitch.Core.Loanwords;
using GreekSwitch.Core.Tests.Support;

namespace GreekSwitch.Core.Tests.Loanwords;

/// <summary>The test table of docs/loanword-design.md, plus the tiers, promotion and rejection.</summary>
[Collection(LanguageDataGroup.Name)]
public class LoanwordDetectorTests(LanguageDataFixture data)
{
    private static readonly byte[] Key = Encoding.ASCII.GetBytes("test key, 32 bytes long........!");

    private LoanwordDetector Create(LoanwordOptions? options = null, LoanwordPairStore? pairs = null, bool load = true)
    {
        var detector = new LoanwordDetector(pairs ?? new LoanwordPairStore(hasher: new HmacWordHasher(Key)), data.Dictionaries, data.Frequencies, options: options);
        if (load)
        {
            detector.UseIndex(data.Phonetic);
        }

        return detector;
    }

    [Theory]
    [InlineData("κοντέινερ", "container")]
    [InlineData("κομπιούτερ", "computer")]
    [InlineData("σέρβερ", "server")]
    [InlineData("ντεπλόι", "deploy")] // tier 2 is off by default
    public void Suggests_the_English_word(string greek, string english)
    {
        Create().Check(greek).Should().BeOfType<LoanwordSuggest>().Which.Target.Should().Be(english);
    }

    [Fact]
    public void Tier_2_replaces_a_strong_match_on_a_non_dictionary_word_to_a_domain_term()
    {
        var detector = Create(new LoanwordOptions { AutoReplaceStrongMatches = true });

        detector.Check("ντεπλόι").Should().Be(new LoanwordReplace("deploy", LoanwordTier.StrongMatch));
    }

    [Fact]
    public void Tier_2_does_not_replace_dictionary_words_or_terms_off_the_list()
    {
        var detector = Create(new LoanwordOptions { AutoReplaceStrongMatches = true });

        detector.Check("κοντέινερ").Should().BeOfType<LoanwordSuggest>("κοντέινερ is in el_GR");

        var noTechTerms = new LoanwordDetector(new LoanwordPairStore(), data.Dictionaries, data.Frequencies, new DomainVocabulary([]), new LoanwordOptions { AutoReplaceStrongMatches = true });
        noTechTerms.UseIndex(data.Phonetic);
        noTechTerms.Check("ντεπλόι").Should().BeOfType<LoanwordSuggest>();
    }

    [Theory]
    [InlineData("καλημέρα")]
    [InlineData("σπίτι")]
    [InlineData("πράξεις")]
    [InlineData("καλός")]    // sounds like close, but it's a Greek word with a Greek ending
    [InlineData("μένει")]    // sounds like many: a function word
    [InlineData("τεστ")]     // a loanword Greek has adopted: frequent in Greek
    [InlineData("Ιράκ")]     // a name
    [InlineData("δουλειά")]
    [InlineData("server")]   // not Greek
    [InlineData("ψονταινερ")] // gibberish (the layout stage fixes this one)
    public void Leaves_ordinary_Greek_alone(string word)
    {
        Create(new LoanwordOptions { AutoReplaceStrongMatches = true }).Check(word).Should().Be(LoanwordResult.None);
    }

    [Theory]
    [InlineData("καρ")]
    [InlineData("μπα")]
    public void Ignores_words_shorter_than_MinLetters(string word)
    {
        Create().Check(word).Should().Be(LoanwordResult.None);
    }

    [Fact]
    public void Returns_None_until_the_index_is_loaded()
    {
        var detector = Create(load: false);

        detector.IsReady.Should().BeFalse();
        detector.Check("κοντέινερ").Should().Be(LoanwordResult.None);

        detector.UseIndex(data.Phonetic);
        detector.IsReady.Should().BeTrue();
        detector.Check("κοντέινερ").Should().BeOfType<LoanwordSuggest>();
    }

    [Theory]
    [InlineData("Κοντέινερ", "Container")]
    [InlineData("ΚΟΝΤΕΙΝΕΡ", "CONTAINER")]
    public void Cases_the_target_like_the_typed_word(string greek, string expected)
    {
        Create().Check(greek).Should().BeOfType<LoanwordSuggest>().Which.Target.Should().Be(expected);
    }

    [Fact]
    public void A_pair_from_Settings_is_replaced_at_once()
    {
        var pairs = new LoanwordPairStore([new LoanwordPair("ρεπό", "day off")], hasher: new HmacWordHasher(Key));

        Create(pairs: pairs).Check("ρεπό").Should().Be(new LoanwordReplace("day off", LoanwordTier.UserPair));
        Create(pairs: pairs).Check("Ρεπό").Should().Be(new LoanwordReplace("Day off", LoanwordTier.UserPair));
    }

    [Fact]
    public void Three_acceptances_promote_a_pair_to_tier_1()
    {
        var detector = Create();

        for (var i = 0; i < 2; i++)
        {
            detector.Check("κοντέινερ").Should().BeOfType<LoanwordSuggest>();
            detector.Accept("κοντέινερ", "container");
        }

        detector.Check("κοντέινερ").Should().BeOfType<LoanwordSuggest>("two acceptances are not enough");
        detector.Accept("κοντέινερ", "Container");

        detector.Check("κοντέινερ").Should().Be(new LoanwordReplace("container", LoanwordTier.UserPair));
    }

    [Fact]
    public void Rejecting_a_promoted_pair_demotes_it_and_repeated_rejections_hide_the_suggestion()
    {
        var detector = Create();
        for (var i = 0; i < 3; i++)
        {
            detector.Accept("κοντέινερ", "container");
        }

        detector.Reject("κοντέινερ", "container");
        detector.Check("κοντέινερ").Should().BeOfType<LoanwordSuggest>("an undo demotes the pair");

        detector.Reject("κοντέινερ", "container");
        detector.Reject("κοντέινερ", "container");
        detector.Check("κοντέινερ").Should().Be(LoanwordResult.None);
        detector.Check("κομπιούτερ").Should().BeOfType<LoanwordSuggest>("only that word is affected");
    }

    [Fact]
    public void An_accepted_target_wins_a_near_tie()
    {
        var detector = Create();
        detector.Check("σάιτ").Should().BeOfType<LoanwordSuggest>().Which.Target.Should().Be("site");

        detector.Accept("σάιτ", "sight");

        detector.Check("σάιτ").Should().BeOfType<LoanwordSuggest>().Which.Target.Should().Be("sight");
    }

    [Fact]
    public void Options_can_be_replaced_while_running()
    {
        var detector = Create();
        detector.Check("ντεπλόι").Should().BeOfType<LoanwordSuggest>();

        detector.Options = detector.Options with { AutoReplaceStrongMatches = true };

        detector.Check("ντεπλόι").Should().BeOfType<LoanwordReplace>();
    }
}
