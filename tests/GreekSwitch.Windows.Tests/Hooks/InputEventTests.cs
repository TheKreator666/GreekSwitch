using GreekSwitch.Core;
using GreekSwitch.Core.Input;
using GreekSwitch.Windows.Hooks;

namespace GreekSwitch.Windows.Tests.Hooks;

public class InputEventTests
{
    [Fact]
    public void Key_language_comes_from_the_layout_captured_in_the_hook()
    {
        var greek = InputEvent.ForKey(new KeyInput('A'), 1, 0x100, 0x04080408);

        greek.KeyLanguage(() => Lang.English).Should().Be(Lang.Greek, "the worker must not re-read a layout that may have changed since");
    }

    [Fact]
    public void Falls_back_to_reading_the_layout_when_the_hook_had_none()
    {
        var unknown = InputEvent.ForKey(new KeyInput('A'), 1, 0x100, 0);

        unknown.KeyLanguage(() => Lang.English).Should().Be(Lang.English);
    }

    [Fact]
    public void A_third_language_layout_gives_no_language()
    {
        var german = InputEvent.ForKey(new KeyInput('A'), 1, 0x100, 0x04070407);

        german.KeyLanguage(() => Lang.English).Should().BeNull();
    }
}
