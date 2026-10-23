using GreekSwitch.Core.Input;
using GreekSwitch.Core.Layout;
using GreekSwitch.Core.Scoring;
using GreekSwitch.Core.Tests.Support;

namespace GreekSwitch.Core.Tests.Session;

/// <summary>Milestone 13: the user's escape hatches.</summary>
[Collection(LanguageDataGroup.Name)]
public class HotkeyCommandTests(LanguageDataFixture data)
{
    [Fact]
    public void Convert_last_word_converts_a_word_the_engine_kept()
    {
        // "den" alone is kept: it's a word in both languages and there is no context.
        var sim = new TypingSimulator(data).TypeIn(Lang.English, "den ");
        sim.Screen.Should().Be("den ");

        sim.Session.ConvertLastWord().Should().BeTrue();

        sim.Screen.Should().Be("δεν ");
        sim.ActiveLayout.Should().Be(Lang.Greek);
    }

    [Fact]
    public void Converting_a_kept_word_learns_to_convert_it_next_time()
    {
        var sim = new TypingSimulator(data).TypeIn(Lang.English, "den ");
        sim.Session.ConvertLastWord();
        sim.Session.Reset();

        sim.TypeIn(Lang.English, "den ");

        sim.Screen.Should().Be("δεν δεν ", "the user's choice (hashed) now applies automatically");
        sim.AlwaysConvert.LearnedCount.Should().Be(1);
    }

    [Fact]
    public void Converting_a_corrected_word_back_learns_never_to_convert_it()
    {
        var sim = new TypingSimulator(data).TypeIn(Lang.Greek, "αππλιψατιον ");
        sim.Screen.Should().Be("application ");

        sim.Session.ConvertLastWord();

        sim.Screen.Should().Be("αππλιψατιον ");
        sim.ActiveLayout.Should().Be(Lang.Greek);
        sim.NeverConvert.LearnedCount.Should().Be(1);
    }

    [Fact]
    public void Backspace_right_after_convert_last_word_undoes_it()
    {
        var sim = new TypingSimulator(data).TypeIn(Lang.English, "den ");
        sim.Session.ConvertLastWord();

        sim.Backspace();

        sim.Screen.Should().Be("den");
    }

    [Fact]
    public void Convert_last_word_converts_the_word_being_typed()
    {
        var sim = new TypingSimulator(data).TypeIn(Lang.English, "kalhm;era");

        sim.Session.ConvertLastWord().Should().BeTrue();

        sim.Screen.Should().Be("καλημέρα");
    }

    [Fact]
    public void Convert_last_word_with_nothing_typed_does_nothing()
    {
        var sim = new TypingSimulator(data);

        sim.Session.ConvertLastWord().Should().BeFalse();
        sim.Replacements.Should().Be(0);
    }

    [Fact]
    public void A_registered_hotkey_chord_does_not_reset_the_word()
    {
        var chord = HotkeyText.Parse("Ctrl+Alt+Space")!.Value;
        var sim = new TypingSimulator(data, session: new() { HotkeyChords = [chord] }).TypeIn(Lang.English, "den ");

        // The hook sees the hotkey's keys before Windows turns them into WM_HOTKEY.
        sim.Press(new KeyInput(VirtualKeys.Space, Control: true, Alt: true));
        sim.Session.ConvertLastWord().Should().BeTrue();

        sim.Screen.Should().Be("δεν ");
    }

    [Fact]
    public void Other_shortcuts_still_reset()
    {
        var sim = new TypingSimulator(data).TypeIn(Lang.English, "den ");
        sim.Press(new KeyInput('Z', Control: true)); // Ctrl+Z may have changed the text

        sim.Session.ConvertLastWord().Should().BeFalse();
    }

    [Fact]
    public void Accidental_caps_lock_is_fixed_and_turned_off_when_enabled()
    {
        var sim = new TypingSimulator(data, scoring: new ScoringOptions { FixAccidentalCapsLock = true });

        // With Caps Lock on, the user types Shift+h, e, l, l, o: "hELLO".
        sim.TypeIn(Lang.English, "hELLO ", capsLock: true);

        sim.Screen.Should().Be("Hello ");
        sim.CapsLockTurnedOff.Should().BeTrue();
    }

    [Fact]
    public void Accidental_caps_lock_is_left_alone_by_default()
    {
        var sim = new TypingSimulator(data).TypeIn(Lang.English, "hELLO ", capsLock: true);

        sim.Screen.Should().Be("hELLO ");
    }

    [Fact]
    public void Deliberate_all_caps_with_caps_lock_is_left_alone()
    {
        var sim = new TypingSimulator(data, scoring: new ScoringOptions { FixAccidentalCapsLock = true })
            .TypeIn(Lang.English, "HELLO ", capsLock: true);

        sim.Screen.Should().Be("HELLO ");
        sim.CapsLockTurnedOff.Should().BeFalse();
    }
}
