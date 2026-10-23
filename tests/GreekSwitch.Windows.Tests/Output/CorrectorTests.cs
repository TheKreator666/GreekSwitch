using GreekSwitch.Core;
using GreekSwitch.Windows.Layouts;
using GreekSwitch.Windows.Native;
using GreekSwitch.Windows.Output;
using Microsoft.Extensions.Logging.Abstractions;

namespace GreekSwitch.Windows.Tests.Output;

/// <summary>
/// Regression: Windows 11 Notepad passes injected Unicode characters through the active layout,
/// so "application" typed while Greek was still active came out as spaces. The corrector must
/// switch first, wait until the switch is visible, and only then type.
/// </summary>
public class CorrectorTests
{
    private readonly FakeSwitcher _layouts = new(Lang.Greek);
    private readonly TypeAheadGuard _guard = new();
    private readonly FakeSender _sender;
    private bool _modifierHeld;

    public CorrectorTests() => _sender = new FakeSender(_layouts);

    private Corrector Create(TimeSpan? timeout = null) =>
        new(_layouts, _guard, NullLogger.Instance, _sender, () => _modifierHeld, timeout ?? TimeSpan.FromMilliseconds(200));

    [Fact]
    public void Switches_the_layout_before_typing_the_text()
    {
        _layouts.PollsUntilSwitch = 3; // the target app takes a moment to apply the switch

        var applied = Create().Replace(12, "application ", Lang.English);

        applied.Should().BeTrue();
        _sender.Batches.Should().ContainSingle();
        _sender.LayoutAtSend.Should().Equal(Lang.English);
    }

    [Fact]
    public void Does_not_request_a_switch_when_the_target_layout_is_already_active()
    {
        _layouts.Current = Lang.English;

        Create().Replace(4, "abc ", Lang.English).Should().BeTrue();

        _layouts.SwitchRequests.Should().BeEmpty();
    }

    [Fact]
    public void Types_nothing_when_the_switch_never_takes_effect()
    {
        _layouts.PollsUntilSwitch = int.MaxValue;

        var applied = Create(TimeSpan.FromMilliseconds(30)).Replace(12, "application ", Lang.English);

        applied.Should().BeFalse();
        _sender.Batches.Should().BeEmpty();
    }

    [Fact]
    public void Types_nothing_when_the_user_has_typed_ahead()
    {
        _guard.OnKeyCaptured(isTypingKey: true);   // the delimiter, being processed
        _guard.MarkProcessed(_guard.Captured);
        _guard.OnKeyCaptured(isTypingKey: true);   // a letter the user typed meanwhile

        Create().Replace(12, "application ", Lang.English).Should().BeFalse();

        _sender.Batches.Should().BeEmpty();
    }

    [Fact]
    public void Modifier_keys_pressed_meanwhile_do_not_count_as_typing_ahead()
    {
        _guard.OnKeyCaptured(isTypingKey: true);
        _guard.MarkProcessed(_guard.Captured);
        _guard.OnKeyCaptured(isTypingKey: false);  // e.g. Shift for the next word's capital

        Create().Replace(12, "application ", Lang.English).Should().BeTrue();
    }

    [Fact]
    public void Types_nothing_while_a_modifier_is_held()
    {
        _modifierHeld = true;

        Create().Replace(12, "application ", Lang.English).Should().BeFalse();

        _sender.Batches.Should().BeEmpty();
        _layouts.SwitchRequests.Should().BeEmpty();
    }

    [Fact]
    public void Reports_failure_when_SendInput_is_blocked()
    {
        _sender.Blocked = true;

        Create().Replace(12, "application ", Lang.English).Should().BeFalse();
    }

    private sealed class FakeSwitcher(Lang initial) : ILayoutSwitcher
    {
        private Lang? _pending;
        private int _polls;

        public Lang Current { get; set; } = initial;

        public int PollsUntilSwitch { get; set; }

        public List<Lang> SwitchRequests { get; } = [];

        public Lang? CurrentLayout()
        {
            if (_pending is { } target && ++_polls > PollsUntilSwitch)
            {
                Current = target;
                _pending = null;
            }

            return Current;
        }

        public void SwitchTo(Lang lang)
        {
            SwitchRequests.Add(lang);
            _pending = lang;
            _polls = 0;
        }
    }

    private sealed class FakeSender(FakeSwitcher layouts) : IInputSender
    {
        public bool Blocked { get; set; }

        public List<INPUT[]> Batches { get; } = [];

        public List<Lang> LayoutAtSend { get; } = [];

        public uint Send(INPUT[] inputs)
        {
            if (Blocked)
            {
                return 0;
            }

            Batches.Add(inputs);
            LayoutAtSend.Add(layouts.Current);
            return (uint)inputs.Length;
        }
    }
}
