using GreekSwitch.Windows.Health;

namespace GreekSwitch.Windows.Tests.Health;

public class HookWatchdogTests
{
    [Theory]
    [InlineData(10_000u, 10_000u, false)]  // the hooks saw the latest input
    [InlineData(11_000u, 10_000u, false)]  // a second of lag is normal
    [InlineData(13_000u, 10_000u, true)]   // input 3 s after the hooks last saw anything
    [InlineData(9_000u, 10_000u, false)]   // the hooks saw something newer (e.g. injected input)
    public void Detects_input_the_hooks_never_saw(uint lastInput, uint lastHook, bool silent)
    {
        HookWatchdog.HooksLookSilent(lastInput, lastHook).Should().Be(silent);
    }

    [Fact]
    public void Survives_the_tick_counter_wrapping_around()
    {
        // GetTickCount wraps every 49.7 days: just before and just after the wrap.
        HookWatchdog.HooksLookSilent(lastInputTick: 500, lastHookTick: uint.MaxValue - 499).Should().BeFalse();
        HookWatchdog.HooksLookSilent(lastInputTick: 5_000, lastHookTick: uint.MaxValue - 499).Should().BeTrue();
    }
}
