using GreekSwitch.Core.Tests.Support;

namespace GreekSwitch.Core.Tests.Session;

[Collection(LanguageDataGroup.Name)]
public class UndoTests(LanguageDataFixture data)
{
    [Fact]
    public void Backspace_right_after_a_replacement_restores_the_original_and_remembers_it()
    {
        var sim = new TypingSimulator(data).TypeIn(Lang.Greek, "αππλιψατιον ");
        sim.Screen.Should().Be("application ");
        sim.ActiveLayout.Should().Be(Lang.English);

        sim.Backspace();

        sim.Screen.Should().Be("αππλιψατιον");
        sim.ActiveLayout.Should().Be(Lang.Greek);
        sim.NeverConvert.Contains("αππλιψατιον").Should().BeTrue();
    }

    [Fact]
    public void After_undo_the_word_is_not_converted_again()
    {
        var sim = new TypingSimulator(data).TypeIn(Lang.Greek, "αππλιψατιον ").Backspace();

        sim.Press(new(Core.Layout.VirtualKeys.Space));
        sim.TypeIn(Lang.Greek, "αππλιψατιον ");

        sim.Screen.Should().Be("αππλιψατιον αππλιψατιον ");
    }

    [Fact]
    public void Backspace_after_another_key_is_an_ordinary_backspace()
    {
        var sim = new TypingSimulator(data).TypeIn(Lang.Greek, "αππλιψατιον ");
        sim.TypeIn(Lang.English, "x").Backspace();

        sim.Screen.Should().Be("application ");
        sim.NeverConvert.LearnedCount.Should().Be(0);
    }

    [Fact]
    public void Undo_is_disarmed_by_a_reset()
    {
        var sim = new TypingSimulator(data).TypeIn(Lang.Greek, "αππλιψατιον ");
        sim.Session.Reset();
        sim.Backspace();

        sim.Screen.Should().Be("application");
        sim.NeverConvert.LearnedCount.Should().Be(0);
    }
}
