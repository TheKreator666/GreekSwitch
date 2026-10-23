using GreekSwitch.Core.Input;
using GreekSwitch.Core.Layout;

namespace GreekSwitch.Core.Tests.Input;

public class HotkeyTextTests
{
    [Theory]
    [InlineData("Ctrl+Alt+Space", VirtualKeys.Space, true, true, false, false)]
    [InlineData("ctrl + shift + alt + f12", 0x7B, true, true, true, false)]
    [InlineData("Pause", 0x13, false, false, false, false)]
    [InlineData("Win+Shift+K", 'K', false, false, true, true)]
    [InlineData("Ctrl+Alt+7", '7', true, true, false, false)]
    public void Parses_hotkey_text(string text, int vk, bool ctrl, bool alt, bool shift, bool win)
    {
        HotkeyText.Parse(text).Should().Be(new KeyChord(vk, ctrl, alt, shift, win));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ctrl+Alt")]          // no key
    [InlineData("Ctrl+Nope")]
    [InlineData("Ctrl+A+B")]          // two keys
    public void Rejects_invalid_text(string text)
    {
        HotkeyText.Parse(text).Should().BeNull();
    }

    [Theory]
    [InlineData("Ctrl+Alt+Space")]
    [InlineData("Ctrl+Alt+Shift+F12")]
    [InlineData("Pause")]
    [InlineData("Win+Shift+K")]
    public void Formats_back_to_the_same_text(string text)
    {
        HotkeyText.Format(HotkeyText.Parse(text)!.Value).Should().Be(text);
    }

    [Fact]
    public void A_chord_matches_only_its_exact_modifiers()
    {
        var chord = HotkeyText.Parse("Ctrl+Alt+Space")!.Value;

        chord.Matches(new KeyInput(VirtualKeys.Space, Control: true, Alt: true)).Should().BeTrue();
        chord.Matches(new KeyInput(VirtualKeys.Space, Shift: true, Control: true, Alt: true)).Should().BeFalse();
        chord.Matches(new KeyInput(VirtualKeys.Space, Control: true)).Should().BeFalse();
    }
}
