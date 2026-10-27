using GreekSwitch.Core.Session;
using GreekSwitch.Core.Tests.Support;

namespace GreekSwitch.Core.Tests.Session;

/// <summary>
/// Greek typed without the tonos key gets its accents when the user pauses, in one correction,
/// never while typing goes on (a correction racing the user's keys can lose letters).
/// </summary>
[Collection(LanguageDataGroup.Name)]
public class AccentTests(LanguageDataFixture data)
{
    private static readonly SessionOptions On = new() { RestoreAccents = true };

    private TypingSimulator Sim(bool follow = true, SessionOptions? options = null) =>
        new(data, followLayoutSwitches: follow, session: options ?? On);

    [Fact]
    public void Adds_accents_at_a_pause_in_one_correction()
    {
        var sim = Sim().TypeIn(Lang.Greek, "ειναι πολυ ωραιο, ετσι; ");
        sim.Screen.Should().Be("ειναι πολυ ωραιο, ετσι; ", "nothing is retyped while the user types");
        sim.Replacements.Should().Be(0);

        sim.Pause();

        sim.Screen.Should().Be("είναι πολύ ωραίο, έτσι; ");
        sim.Replacements.Should().Be(1);
    }

    [Fact]
    public void Only_retypes_from_the_first_word_that_changes()
    {
        var sim = Sim().TypeIn(Lang.Greek, "και να ειναι ").Pause();

        sim.Screen.Should().Be("και να είναι ");
        sim.LastReplaced.Should().Be("ειναι ", "και and να stay as they are on screen");
    }

    [Fact]
    public void Waits_for_the_word_being_typed_to_end()
    {
        var sim = Sim().TypeIn(Lang.Greek, "ειναι πολ");
        sim.Session.AccentsPending.Should().BeFalse();

        sim.TypeIn(Lang.Greek, "υ ").Pause();

        sim.Screen.Should().Be("είναι πολύ ");
    }

    [Fact]
    public void Wrong_layout_sentence_gets_accents_at_the_pause()
    {
        // After "poy" is fixed (at once) the layout is Greek, so the rest arrives as Greek letters.
        var sim = Sim(follow: true).SetActiveLayout(Lang.English);
        sim.TypeIntended("Θελω να φτιαξω ενα προγραμμα που θα κανει πραξεις ", Lang.Greek);
        sim.Pause();

        sim.Screen.Should().Be("Θέλω να φτιάξω ένα πρόγραμμα που θα κάνει πράξεις ");
    }

    [Theory]
    [InlineData("και να που ")]           // no accent in their usual spelling
    [InlineData("είναι πολύ ")]           // already accented
    [InlineData("ΕΙΝΑΙ ΠΟΛΥ ")]           // all caps are written without accents
    [InlineData("σπιτακιαδακι ")]         // not in the table
    public void Leaves_other_Greek_words_alone(string typed) =>
        Sim().TypeIn(Lang.Greek, typed).Pause().Screen.Should().Be(typed);

    [Fact]
    public void Off_by_option() =>
        Sim(options: new SessionOptions()).TypeIn(Lang.Greek, "ειναι ").Pause().Screen.Should().Be("ειναι ");

    [Fact]
    public void Enter_ends_the_stretch_so_it_is_never_retyped() =>
        Sim().TypeIn(Lang.Greek, "ειναι\nπολυ ").Pause().Screen.Should().Be("ειναι\nπολύ ");

    [Fact]
    public void Deleting_into_earlier_text_drops_the_pending_accents()
    {
        var sim = Sim().TypeIn(Lang.Greek, "ειναι ").Backspace().Backspace();

        sim.Session.AccentsPending.Should().BeFalse("the screen no longer matches what was typed");
        sim.Pause().Screen.Should().Be("ειναι"[..4]);
    }

    [Fact]
    public void English_text_ends_the_stretch()
    {
        // Greek keys can't retype Latin letters: the Greek words before it stay as typed.
        var sim = Sim(follow: false).TypeIn(Lang.Greek, "ειναι ").TypeIn(Lang.English, "ok ").Pause();

        sim.Screen.Should().Be("ειναι ok ");
    }

    [Fact]
    public void Backspace_after_the_pause_edits_the_text_as_usual()
    {
        var sim = Sim().TypeIn(Lang.Greek, "ειναι ").Pause().Backspace();

        sim.Screen.Should().Be("είναι", "a Backspace after a pause is meant for the text, not an undo");
    }

    [Fact]
    public void Suggest_only_apps_get_no_accents()
    {
        var sim = Sim();
        sim.Session.SuggestOnly = true;

        sim.TypeIn(Lang.Greek, "ειναι ").Pause().Screen.Should().Be("ειναι ");
    }
}
