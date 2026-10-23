using GreekSwitch.Core.Layout;
using GreekSwitch.Windows.Layouts;

namespace GreekSwitch.Windows.Tests.Layouts;

public class LayoutHotkeyTests
{
    [Theory]
    [InlineData("1", null, LayoutHotkey.AltShift)]
    [InlineData("2", null, LayoutHotkey.CtrlShift)]
    [InlineData("4", null, LayoutHotkey.Grave)]
    [InlineData("3", null, LayoutHotkey.None)]          // "not assigned"
    [InlineData(null, "2", LayoutHotkey.CtrlShift)]     // older Windows: only "Hotkey"
    [InlineData(null, null, LayoutHotkey.AltShift)]     // nothing set: Windows' default
    [InlineData("garbage", null, LayoutHotkey.AltShift)]
    public void Reads_the_language_hotkey_setting(string? languageHotkey, string? hotkey, LayoutHotkey expected)
    {
        LayoutHotkeys.FromRegistry(languageHotkey, hotkey).Should().Be(expected);
    }

    [Theory]
    [InlineData(LayoutHotkey.AltShift, new[] { VirtualKeys.LMenu, VirtualKeys.LShift })]
    [InlineData(LayoutHotkey.CtrlShift, new[] { VirtualKeys.LControl, VirtualKeys.LShift })]
    [InlineData(LayoutHotkey.Grave, new[] { VirtualKeys.Oem3 })]
    [InlineData(LayoutHotkey.None, new[] { VirtualKeys.LWin, VirtualKeys.Space })] // Win+Space always works
    public void Maps_each_setting_to_the_keys_to_press(LayoutHotkey hotkey, int[] keys)
    {
        LayoutHotkeys.Keys(hotkey).Should().Equal(keys);
    }
}
