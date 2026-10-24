using GreekSwitch.Core.Input;

namespace GreekSwitch.Windows.Tests;

public class HotkeysTests
{
    [Theory]
    [InlineData("Ctrl+Alt+Space", 0x4003u)]
    [InlineData("Ctrl+Alt+Shift+G", 0x4007u)]
    [InlineData("Win+Shift+K", 0x400Cu)]
    [InlineData("Pause", 0x4000u)]
    public void Maps_chords_to_RegisterHotKey_modifiers_without_auto_repeat(string text, uint expected)
    {
        Hotkeys.Modifiers(HotkeyText.Parse(text)!.Value).Should().Be(expected);
    }
}
