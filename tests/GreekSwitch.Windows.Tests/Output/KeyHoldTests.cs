using GreekSwitch.Windows.Output;

namespace GreekSwitch.Windows.Tests.Output;

public class KeyHoldTests
{
    private static readonly HeldKey K = new(0x4B, 0x25, Extended: false, Up: false);

    [Fact]
    public void Holds_nothing_unless_a_correction_is_being_typed() =>
        new KeyHold().TryHold(K).Should().BeFalse();

    [Fact]
    public void Holds_keys_in_order_until_ended()
    {
        var hold = new KeyHold();
        hold.Begin();
        hold.TryHold(K).Should().BeTrue();
        hold.TryHold(K with { Up = true }).Should().BeTrue();
        hold.End();

        hold.TryHold(K).Should().BeFalse("holding ended");
        hold.Drain().Should().Equal(K, K with { Up = true });
        hold.Drain().Should().BeEmpty();
    }

    [Fact]
    public void Stops_holding_by_itself_after_the_cap()
    {
        var hold = new KeyHold();
        hold.Begin(); // and the corrector never ends it

        Thread.Sleep(KeyHold.Cap + TimeSpan.FromMilliseconds(50));

        hold.TryHold(K).Should().BeFalse("the keyboard must never get stuck");
    }

    [Fact]
    public void Replays_as_our_own_events_with_up_and_extended_flags()
    {
        var inputs = KeyHold.ToInputs([K, new HeldKey(0x25, 0x4B, Extended: true, Up: true)]);

        inputs.Should().HaveCount(2);
        inputs.Should().OnlyContain(i => i.U.ki.dwExtraInfo == InputBuilder.Signature);
        inputs[0].U.ki.dwFlags.Should().Be(0u);
        inputs[1].U.ki.dwFlags.Should().Be(0x0001u | 0x0002u); // extended, key-up
        inputs[1].U.ki.wScan.Should().Be(0x4B);
    }
}
