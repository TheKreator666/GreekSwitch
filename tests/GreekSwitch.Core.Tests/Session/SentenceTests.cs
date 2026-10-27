using GreekSwitch.Core.Tests.Support;
using Xunit.Abstractions;

namespace GreekSwitch.Core.Tests.Session;

/// <summary>
/// End to end: what the user physically pressed in, corrected text out. Each input is written
/// as it would appear with no corrector running, and each word's script says which layout was
/// active when it was typed.
/// </summary>
[Collection(LanguageDataGroup.Name)]
public class SentenceTests(LanguageDataFixture data, ITestOutputHelper output)
{
    public static TheoryData<string, string> Sentences() => new()
    {
        { "Θέλω να φτιάξω ένα αππλιψατιον poy ua κάνει πράξεις", "Θέλω να φτιάξω ένα application που θα κάνει πράξεις" },
        { "ηελλο ςορλδ", "hello world" },
        { "καλημέρα", "καλημέρα" },
        { "hello", "hello" },
        { "the", "the" },
        { "ok", "ok" },
        { "C#", "C#" },
        { "user@mail.com", "user@mail.com" },
        { "v2", "v2" },
        { "myVariable", "myVariable" },
        { "I love ελλάδα", "I love ελλάδα" },
    };

    /// <summary>
    /// Greek typed in the English layout without the tonos key: the converted words get their
    /// accents back. (Words typed without accents in the Greek layout are the user's own: kept.)
    /// </summary>
    [Theory]
    [InlineData("Θέλω να φτιάξω ένα πρόγραμμα poy ua kanei prajeis", "Θέλω να φτιάξω ένα πρόγραμμα που θα κάνει πράξεις")]
    [InlineData("Kalhmera", "Καλημέρα")]
    public void Puts_back_accents_the_wrong_layout_left_out(string typed, string expected)
    {
        var sim = new TypingSimulator(data, followLayoutSwitches: false).Type(typed + " ");

        sim.Screen.Should().Be(expected + " ");
    }

    /// <summary>The user keeps typing each word in the layout it shows, whatever we switch to.</summary>
    [Theory]
    [MemberData(nameof(Sentences))]
    public void Corrects_words_typed_in_the_wrong_layout(string typed, string expected)
    {
        var sim = new TypingSimulator(data, followLayoutSwitches: false).Type(typed + " ");
        output.WriteLine(sim.Screen);

        sim.Screen.Should().Be(expected + " ");
    }

    /// <summary>
    /// Like <see cref="Sentences"/>, with names capitalised as people type them: a lowercase
    /// "ελλάδα" typed in the wrong layout is deliberately not treated as the name Ελλάδα.
    /// </summary>
    public static TheoryData<string, string> SentencesAsTyped()
    {
        var data = new TheoryData<string, string>();
        foreach (var row in Sentences())
        {
            data.Add(((string)row[0]).Replace("ελλάδα", "Ελλάδα"), ((string)row[1]).Replace("ελλάδα", "Ελλάδα"));
        }

        return data;
    }

    /// <summary>Real behaviour: after a correction, the switched layout applies to the next keys.</summary>
    [Theory]
    [MemberData(nameof(SentencesAsTyped))]
    public void Gives_the_same_text_when_layout_switches_take_effect(string typed, string expected)
    {
        var sim = new TypingSimulator(data, followLayoutSwitches: true).Type(typed + " ");

        sim.Screen.Should().Be(expected + " ");
    }

    [Fact]
    public void Example_sentence_switches_layout_after_each_correction()
    {
        var sim = new TypingSimulator(data, followLayoutSwitches: true)
            .Type("Θέλω να φτιάξω ένα αππλιψατιον poy ua κάνει πράξεις ");

        // αππλιψατιον → application (switch to EN), then the P O Y keys arrive in English as
        // "poy" → που (switch back to GR); "ua" is then typed in Greek and comes out as θα.
        sim.LayoutSwitches.Should().Equal(Lang.English, Lang.Greek);
        sim.Replacements.Should().Be(2);
    }

    [Fact]
    public void Short_word_converts_when_context_agrees()
    {
        // Two Greek words of context, then the U A keys in the English layout.
        var sim = new TypingSimulator(data, followLayoutSwitches: false).Type("είπε ότι ua έρθει ");

        sim.Screen.Should().Be("είπε ότι θα έρθει ");
    }

    [Fact]
    public void Short_word_without_context_is_kept()
    {
        var sim = new TypingSimulator(data).Type("ua ");

        sim.Screen.Should().Be("ua ");
    }

    [Fact]
    public void Punctuation_delimiter_is_retyped_after_the_corrected_word()
    {
        var sim = new TypingSimulator(data).TypeIn(Lang.Greek, "ηελλο,");

        sim.Screen.Should().Be("hello,");
    }

    [Fact]
    public void Greek_typed_in_english_layout_gets_accents_and_final_sigma()
    {
        // Physically: k a l h m ; e r a  — the ';' key is the tonos dead key in Greek.
        var sim = new TypingSimulator(data).TypeIn(Lang.English, "kalhm;era ");
        sim.Screen.Should().Be("καλημέρα ");

        sim = new TypingSimulator(data).TypeIn(Lang.English, "pr;ajeis ");
        sim.Screen.Should().Be("πράξεις ");
    }

    [Fact]
    public void Enter_ends_the_word_without_correcting_by_default()
    {
        var sim = new TypingSimulator(data).TypeIn(Lang.Greek, "ηελλο\n");

        sim.Screen.Should().Be("ηελλο\n");
    }

    [Fact]
    public void Enter_corrects_when_enabled()
    {
        var sim = new TypingSimulator(data, session: new() { CorrectOnEnterAndTab = true }).TypeIn(Lang.Greek, "ηελλο\n");

        sim.Screen.Should().Be("hello\n");
    }

    [Fact]
    public void Never_convert_words_are_left_alone()
    {
        var sim = new TypingSimulator(data, neverConvert: ["ηελλο"]).TypeIn(Lang.Greek, "ηελλο ");

        sim.Screen.Should().Be("ηελλο ");
    }

    [Fact]
    public void Word_after_a_reset_is_judged_on_its_own_keys()
    {
        var sim = new TypingSimulator(data).TypeIn(Lang.Greek, "ηε");
        sim.Session.Reset(); // e.g. mouse click moved the caret
        sim.TypeIn(Lang.Greek, "λλο ");

        sim.Replacements.Should().Be(0);
    }
}
