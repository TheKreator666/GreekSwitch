using System.Runtime.InteropServices;
using GreekSwitch.Core.Layout;
using GreekSwitch.Windows.Native;
using GreekSwitch.Windows.Output;

namespace GreekSwitch.Windows.Tests.Output;

public class InputBuilderTests
{
    private const uint KeyUp = 0x0002;
    private const uint Unicode = 0x0004;

    [Fact]
    public void INPUT_has_the_size_SendInput_expects()
    {
        Marshal.SizeOf<INPUT>().Should().Be(IntPtr.Size == 8 ? 40 : 28);
    }

    [Fact]
    public void Backspaces_come_first_then_the_text_as_unicode_key_down_up_pairs()
    {
        var inputs = InputBuilder.Build(2, "πα");

        inputs.Should().HaveCount(8);
        inputs.Should().AllSatisfy(i => i.type.Should().Be(1u)); // INPUT_KEYBOARD

        var keys = inputs.Select(i => i.U.ki).ToArray();
        keys[0].Should().Match<KEYBDINPUT>(k => k.wVk == VirtualKeys.Back && k.dwFlags == 0);
        keys[1].Should().Match<KEYBDINPUT>(k => k.wVk == VirtualKeys.Back && k.dwFlags == KeyUp);
        keys[2].Should().Match<KEYBDINPUT>(k => k.wVk == VirtualKeys.Back && k.dwFlags == 0);
        keys[3].Should().Match<KEYBDINPUT>(k => k.wVk == VirtualKeys.Back && k.dwFlags == KeyUp);
        keys[4].Should().Match<KEYBDINPUT>(k => k.wVk == 0 && k.wScan == 'π' && k.dwFlags == Unicode);
        keys[5].Should().Match<KEYBDINPUT>(k => k.wVk == 0 && k.wScan == 'π' && k.dwFlags == (Unicode | KeyUp));
        keys[6].wScan.Should().Be('α');
        keys[7].wScan.Should().Be('α');
    }

    [Fact]
    public void Enter_and_tab_are_sent_as_virtual_keys()
    {
        var keys = InputBuilder.Build(0, "a\n\t").Select(i => i.U.ki).ToArray();

        keys.Should().HaveCount(6);
        keys[2].Should().Match<KEYBDINPUT>(k => k.wVk == VirtualKeys.Return && k.dwFlags == 0);
        keys[3].Should().Match<KEYBDINPUT>(k => k.wVk == VirtualKeys.Return && k.dwFlags == KeyUp);
        keys[4].wVk.Should().Be(VirtualKeys.Tab);
    }

    [Fact]
    public void Every_event_carries_the_GreekSwitch_signature()
    {
        InputBuilder.Build(1, "x").Should().AllSatisfy(i => i.U.ki.dwExtraInfo.Should().Be(InputBuilder.Signature));
    }

    [Fact]
    public void Empty_correction_is_empty()
    {
        InputBuilder.Build(0, "").Should().BeEmpty();
    }

    [Fact]
    public void Negative_backspaces_are_rejected()
    {
        var act = () => InputBuilder.Build(-1, "x");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
