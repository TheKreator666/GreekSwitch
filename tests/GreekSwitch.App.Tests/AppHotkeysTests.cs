using GreekSwitch.App.Settings;
using GreekSwitch.Core.Input;

namespace GreekSwitch.App.Tests;

public sealed class AppHotkeysTests
{
    [Fact]
    public void Parses_the_three_default_hotkeys()
    {
        AppHotkeys.Parse(new AppSettings()).Should().HaveCount(3);
    }

    [Fact]
    public void An_empty_or_invalid_hotkey_is_off()
    {
        var settings = new AppSettings { ConvertLastWordHotkey = "", ToggleHotkey = "Ctrl+Alt+Nope" };

        AppHotkeys.Parse(settings).Keys.Should().Equal(HotkeyAction.ConvertSelection);
    }

    [Fact]
    public void The_same_chord_twice_is_registered_once()
    {
        var settings = new AppSettings { ConvertLastWordHotkey = "Ctrl+Alt+K", ConvertSelectionHotkey = "alt+ctrl+k" };

        var chords = AppHotkeys.Parse(settings);

        chords.Should().ContainKey(HotkeyAction.ConvertLastWord).And.NotContainKey(HotkeyAction.ConvertSelection);
        chords[HotkeyAction.ConvertLastWord].Should().Be(new KeyChord('K', Control: true, Alt: true));
    }
}
