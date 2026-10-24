using System.Text;
using GreekSwitch.Core.Decisions;
using GreekSwitch.Core.Loanwords;
using GreekSwitch.Core.Session;
using GreekSwitch.Core.Tests.Support;

namespace GreekSwitch.Core.Tests.Session;

/// <summary>Milestone 15 wiring: loanword suggestions and replacements, suggest-only apps, Tab to accept.</summary>
[Collection(LanguageDataGroup.Name)]
public class SuggestionTests(LanguageDataFixture data)
{
    private static readonly byte[] Key = Encoding.ASCII.GetBytes("test key, 32 bytes long........!");
    private static readonly SessionOptions WithLoanwords = new() { SuggestLoanwords = true };

    [Fact]
    public void Suggests_the_English_word_and_tab_accepts_it()
    {
        var sim = Simulator().TypeIn(Lang.Greek, "κοντέινερ ");

        sim.Screen.Should().Be("κοντέινερ ", "a suggestion changes nothing by itself");
        sim.Suggestion.Should().Be("container");

        sim.Tab();

        sim.Screen.Should().Be("container ");
        sim.ActiveLayout.Should().Be(Lang.Greek, "the user is writing Greek");
        sim.Suggestion.Should().BeNull();
    }

    [Fact]
    public void Any_other_key_dismisses_the_suggestion()
    {
        var sim = Simulator().TypeIn(Lang.Greek, "κοντέινερ κ");

        sim.Suggestion.Should().BeNull();
        sim.Screen.Should().Be("κοντέινερ κ");
        sim.Session.HasSuggestion.Should().BeFalse();
    }

    [Fact]
    public void Tab_without_a_suggestion_is_an_ordinary_tab()
    {
        var sim = Simulator().TypeIn(Lang.Greek, "σπίτι ");

        sim.Suggestion.Should().BeNull();
        sim.Tab();

        sim.Screen.Should().Be("σπίτι \t");
    }

    [Fact]
    public void Undoing_an_accepted_suggestion_restores_the_greek_word_without_learning_never_convert()
    {
        var sim = Simulator().TypeIn(Lang.Greek, "κοντέινερ ");
        sim.Tab();

        sim.Backspace();

        sim.Screen.Should().Be("κοντέινερ");
        sim.NeverConvert.LearnedCount.Should().Be(0, "a loanword isn't a layout mistake");

        sim.TypeIn(Lang.Greek, " ");
        sim.Suggestion.Should().BeNull("the word the user just took back isn't offered again straight away");
        sim.Screen.Should().Be("κοντέινερ ");
    }

    [Fact]
    public void Off_unless_enabled()
    {
        var sim = Simulator(new SessionOptions()).TypeIn(Lang.Greek, "κοντέινερ ");

        sim.Suggestion.Should().BeNull();
        sim.SuggestionsShown.Should().BeEmpty();
    }

    [Fact]
    public void Ordinary_greek_gets_no_suggestion()
    {
        var sim = Simulator().TypeIn(Lang.Greek, "καλημέρα σπίτι πράξεις ");

        sim.SuggestionsShown.Should().BeEmpty();
    }

    [Fact]
    public void A_strong_match_is_replaced_when_tier_2_is_on_and_undo_takes_it_back()
    {
        var sim = Simulator(options: new LoanwordOptions { AutoReplaceStrongMatches = true }).TypeIn(Lang.Greek, "ντεπλόι ");

        sim.Screen.Should().Be("deploy ");

        sim.Backspace();
        sim.Screen.Should().Be("ντεπλόι");
    }

    [Fact]
    public void After_three_accepts_the_pair_is_replaced_automatically()
    {
        var sim = Simulator();
        for (var i = 0; i < 3; i++)
        {
            sim.TypeIn(Lang.Greek, "κοντέινερ ");
            sim.Suggestion.Should().Be("container");
            sim.Tab();
            sim.Session.Reset();
        }

        sim.TypeIn(Lang.Greek, "κοντέινερ ");

        sim.Screen.Should().EndWith("container container ");
        sim.Suggestion.Should().BeNull();
    }

    [Fact]
    public void Accepts_are_not_counted_when_learning_is_off()
    {
        var sim = Simulator(new SessionOptions { SuggestLoanwords = true, LearnLoanwords = false });
        for (var i = 0; i < 3; i++)
        {
            sim.TypeIn(Lang.Greek, "κοντέινερ ");
            sim.Tab();
            sim.Session.Reset();
        }

        sim.TypeIn(Lang.Greek, "κοντέινερ ");

        sim.Suggestion.Should().Be("container", "still only a suggestion: nothing was learned");
    }

    [Fact]
    public void Suggest_only_offers_a_layout_correction_instead_of_making_it()
    {
        var sim = new TypingSimulator(data, session: new SessionOptions());
        sim.Session.SuggestOnly = true;

        sim.TypeIn(Lang.English, "kalhm;era ");

        sim.Screen.Should().Be("kalhm;era ");
        sim.Suggestion.Should().Be("καλημέρα");

        sim.Tab();

        sim.Screen.Should().Be("καλημέρα ");
        sim.ActiveLayout.Should().Be(Lang.Greek);

        sim.Backspace();
        sim.Screen.Should().Be("kalhm;era");
        sim.NeverConvert.LearnedCount.Should().Be(1, "undoing a layout correction teaches as usual");
    }

    [Fact]
    public void Suggestions_log_no_text()
    {
        var sim = Simulator().TypeIn(Lang.Greek, "κοντέινερ ");
        sim.Tab();
        sim.Backspace();
        var suggestOnly = new TypingSimulator(data, session: new SessionOptions());
        suggestOnly.Session.SuggestOnly = true;
        suggestOnly.TypeIn(Lang.English, "kalhm;era ");
        suggestOnly.Tab();

        var output = sim.Logger.AllOutput + suggestOnly.Logger.AllOutput;
        output.Should().Contain("Suggestion accepted");
        output.Should().NotContainAny("κοντέινερ", "container", "kalhm", "καλημέρα");
    }

    private TypingSimulator Simulator(SessionOptions? session = null, LoanwordOptions? options = null)
    {
        var detector = new LoanwordDetector(new LoanwordPairStore(hasher: new HmacWordHasher(Key)), data.Dictionaries, data.Frequencies, options: options);
        detector.UseIndex(data.Phonetic);
        return new TypingSimulator(data, session: session ?? WithLoanwords, loanwords: detector);
    }
}
