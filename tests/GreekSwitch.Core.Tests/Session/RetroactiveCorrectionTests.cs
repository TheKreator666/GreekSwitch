using GreekSwitch.Core.Layout;
using GreekSwitch.Core.Tests.Support;

namespace GreekSwitch.Core.Tests.Session;

/// <summary>
/// Milestone 11: after a confident correction, the last few words of the same run are judged
/// again with the new context and fixed in the same batch.
/// </summary>
[Collection(LanguageDataGroup.Name)]
public class RetroactiveCorrectionTests(LanguageDataFixture data)
{
    [Fact]
    public void A_confident_correction_fixes_the_words_before_it()
    {
        // The whole sentence typed in the EN layout: "den" has no Greek context until "u;elv" (θέλω).
        var sim = new TypingSimulator(data).TypeIn(Lang.English, "den u;elv ");

        sim.Screen.Should().Be("δεν θέλω ");
        sim.Replacements.Should().Be(1, "both words are fixed in a single batch");
    }

    [Fact]
    public void Whole_wrong_layout_sentence_comes_out_right()
    {
        var sim = new TypingSimulator(data).TypeIn(Lang.English, "den u;elv na ;eruv ");

        sim.Screen.Should().Be("δεν θέλω να έρθω ");
    }

    [Fact]
    public void Undo_reverts_the_whole_batch_and_learns_every_word()
    {
        var sim = new TypingSimulator(data).TypeIn(Lang.English, "den u;elv ");

        sim.Backspace();

        sim.Screen.Should().Be("den u;elv");
        sim.ActiveLayout.Should().Be(Lang.English);
        sim.NeverConvert.LearnedCount.Should().Be(2);
    }

    [Fact]
    public void Punctuation_ends_the_run()
    {
        var sim = new TypingSimulator(data).TypeIn(Lang.English, "den, u;elv ");

        sim.Screen.Should().Be("den, θέλω ");
    }

    [Fact]
    public void A_reset_ends_the_run()
    {
        var sim = new TypingSimulator(data).TypeIn(Lang.English, "den ");
        sim.Session.Reset(); // e.g. a click
        sim.TypeIn(Lang.English, "u;elv ");

        sim.Screen.Should().Be("den θέλω ");
    }

    [Fact]
    public void Backspacing_into_the_previous_word_ends_the_run()
    {
        var sim = new TypingSimulator(data).TypeIn(Lang.English, "den ").Backspace();
        sim.TypeIn(Lang.English, " u;elv ");

        sim.Screen.Should().Be("den θέλω ");
    }

    [Fact]
    public void Only_the_last_three_words_are_reconsidered()
    {
        var sim = new TypingSimulator(data).TypeIn(Lang.English, "den den den den u;elv ");

        sim.Screen.Should().Be("den δεν δεν δεν θέλω ");
    }

    [Fact]
    public void Reconsidering_stops_at_the_first_word_that_stays()
    {
        // "Microsoft" stays English in any context, so "den" before it is out of reach.
        var sim = new TypingSimulator(data).TypeIn(Lang.English, "den Microsoft u;elv ");

        sim.Screen.Should().Be("den Microsoft θέλω ");
    }

    [Fact]
    public void Never_convert_words_are_not_reconsidered()
    {
        var sim = new TypingSimulator(data, neverConvert: ["den"]).TypeIn(Lang.English, "den u;elv ");

        sim.Screen.Should().Be("den θέλω ");
    }

    [Fact]
    public void Correct_words_before_a_correction_are_left_alone()
    {
        var sim = new TypingSimulator(data).TypeIn(Lang.Greek, "είπε ότι ").TypeIn(Lang.English, "u;elv ");

        sim.Screen.Should().Be("είπε ότι θέλω ");
    }

    [Fact]
    public void Nothing_typed_text_is_logged_for_a_batch()
    {
        var sim = new TypingSimulator(data).TypeIn(Lang.English, "den u;elv ").Backspace();

        foreach (var word in new[] { "den", "u;elv", "δεν", "θέλω" })
        {
            sim.Logger.AllOutput.Should().NotContainEquivalentOf(word);
        }
    }
}
