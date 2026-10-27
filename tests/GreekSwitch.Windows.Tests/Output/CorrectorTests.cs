using GreekSwitch.Core;
using GreekSwitch.Core.Layout;
using GreekSwitch.Windows.Layouts;
using GreekSwitch.Windows.Native;
using GreekSwitch.Windows.Output;
using Microsoft.Extensions.Logging.Abstractions;

namespace GreekSwitch.Windows.Tests.Output;

/// <summary>
/// Regression for Windows 11 Notepad: injected Unicode text typed around a layout switch came out
/// as spaces or repeated letters. The corrector must switch first, wait until the switch is
/// visible, and then press the real keys of the target layout.
/// </summary>
public class CorrectorTests
{
    private const uint KeyUp = 0x0002;
    private const uint Unicode = 0x0004;

    private readonly FakeSwitcher _layouts = new(Lang.Greek);
    private readonly TypeAheadGuard _guard = new();
    private readonly FakeKeyboard _keyboard = new();
    private readonly FocusGuard _focus = new();
    private readonly FakeSender _sender;
    private nint _currentFocus = 0x100;
    private bool _commandWindow;
    private TextMatch _textMatch = TextMatch.Unknown;
    private string? _textChecked;
    private string? _screen; // what ReadBeforeCaret returns; null = can't be read

    public CorrectorTests()
    {
        _sender = new FakeSender(_layouts);
        _focus.Expect(0x100); // the word was typed into window 0x100
    }

    private readonly KeyHold _hold = new();

    /// <summary>
    /// With a low-level hook installed SendInput isn't atomic: a key pressed during the send
    /// would land inside the correction. The hook holds it, and the corrector replays it after.
    /// </summary>
    [Fact]
    public void Keys_pressed_while_the_correction_is_sent_are_replayed_after_it()
    {
        var pressed = new HeldKey(0x4B, 0x25, Extended: false, Up: false);
        _sender.DuringSend = () => _hold.TryHold(pressed).Should().BeTrue("the hook holds it");

        Create().Replace("δουλευει ", "δουλεύει ", Lang.Greek).Should().BeTrue();

        _sender.Batches.Should().HaveCount(2, "the correction, then the held key");
        var replay = _sender.Batches[1].Single();
        replay.U.ki.wVk.Should().Be(0x4B);
        replay.U.ki.dwExtraInfo.Should().Be(InputBuilder.Signature, "the hook ignores our own replay");
        _hold.TryHold(pressed).Should().BeFalse("holding ends with the correction");
    }

    [Fact]
    public void Held_keys_are_replayed_even_if_the_correction_could_not_be_sent()
    {
        _sender.Blocked = true;
        _sender.DuringSend = () => _hold.TryHold(new HeldKey(0x4B, 0x25, false, false));

        Create().Replace("δουλευει ", "δουλεύει ", Lang.Greek).Should().BeFalse();

        _sender.Attempts.Should().Be(2, "the user's key is never lost");
    }

    private Corrector Create(TimeSpan? timeout = null) => new(
        _layouts,
        StaticLayouts.For,
        _guard,
        _keyboard,
        NullLogger.Instance,
        _focus,
        _sender,
        vk => (ushort)(vk + 1000), // recognisable fake scan codes
        timeout ?? TimeSpan.FromMilliseconds(200),
        () => _currentFocus,
        _ => _commandWindow,
        new FakeVerifier(this),
        _hold);

    [Fact]
    public void Types_nothing_if_the_text_before_the_caret_was_changed_by_another_tool()
    {
        _textMatch = TextMatch.Mismatch; // e.g. Word AutoCorrect rewrote the word after Space

        Create().Replace("ηελλο ", "hello ", Lang.English).Should().BeFalse();

        _sender.Batches.Should().BeEmpty();
        _textChecked.Should().Be("ηελλο ", "it checks exactly the text it is about to delete");
    }

    /// <summary>
    /// Live run: accents typed just before a switch to English came out as `fti;ajv` (the switch
    /// reached the app before the queued Greek keys). Accents are now fixed in place: arrows to the
    /// changed letters, retype only those, arrows back. English text is never retyped, no switch.
    /// </summary>
    [Fact]
    public void Retouch_retypes_only_the_changed_letters_and_returns_to_the_end()
    {
        _screen = "x ενα ok ";

        Create().Retouch("ενα ok ", "ένα ok ").Should().BeTrue();

        var downs = Downs(_sender.Batches.Single());
        downs.Should().Equal(
            [.. Enumerable.Repeat((ushort)VirtualKeys.Left, 6), // to the right of ε
             (ushort)VirtualKeys.Back, (ushort)VirtualKeys.Oem1, (ushort)'E', // ; then e: έ
             .. Enumerable.Repeat((ushort)VirtualKeys.Right, 6)]);
        _layouts.SwitchRequests.Should().BeEmpty("the layout is never switched");
    }

    [Fact]
    public void Retouch_fixes_several_words_right_to_left()
    {
        Create().Retouch("φτιαξω ενα x ", "φτιάξω ένα x ").Should().BeTrue();

        var downs = Downs(_sender.Batches.Single());
        downs.Count(vk => vk == VirtualKeys.Back).Should().Be(2, "one letter in each word");
        downs.Count(vk => vk == VirtualKeys.Left).Should().Be(9, "to ε (6), then on to α (3)");
        downs.Count(vk => vk == VirtualKeys.Right).Should().Be(9, "back to the end");
    }

    [Fact]
    public void Retouch_sends_arrows_as_extended_keys()
    {
        Create().Retouch("ενα ", "ένα ").Should().BeTrue();

        // Without the flag Windows reads the numpad key, which types 4 or 6 with Num Lock on.
        _sender.Batches.Single().Where(i => i.U.ki.wVk is VirtualKeys.Left or VirtualKeys.Right)
            .Should().OnlyContain(i => (i.U.ki.dwFlags & 0x0001) != 0);
    }

    [Fact]
    public void Retouch_types_Unicode_when_the_English_layout_is_active()
    {
        _layouts.Current = Lang.English; // the sentence ended with an English word

        Create().Retouch("ενα ok ", "ένα ok ").Should().BeTrue();

        var batch = _sender.Batches.Single();
        batch.Should().Contain(i => (i.U.ki.dwFlags & Unicode) != 0 && i.U.ki.wScan == 'έ');
        _layouts.SwitchRequests.Should().BeEmpty();
    }

    [Fact]
    public void Retouch_keeps_the_capital_the_app_gave_the_first_letter()
    {
        _screen = "Ενα "; // Word: ενα → Ενα at a sentence start

        Create().Retouch("ενα ", "ένα ").Should().BeTrue();

        var downs = Downs(_sender.Batches.Single());
        downs.Should().ContainInConsecutiveOrder((ushort)VirtualKeys.Oem1, (ushort)VirtualKeys.Shift, (ushort)'E'); // Έ
    }

    /// <summary>
    /// Live run, Word: its AutoCorrect accented ένα itself, and the whole accent batch was skipped.
    /// A letter the app already gave its accent is left alone; the others are still fixed.
    /// </summary>
    [Fact]
    public void Retouch_keeps_accents_the_app_already_added()
    {
        _screen = "ένα κανει ";

        Create().Retouch("ενα κανει ", "ένα κάνει ").Should().BeTrue();

        var downs = Downs(_sender.Batches.Single());
        downs.Count(vk => vk == VirtualKeys.Back).Should().Be(1, "only κανει still needs its accent");
        downs.Count(vk => vk == VirtualKeys.Left).Should().Be(4, "back to α of κανει, not on to ένα");
    }

    [Theory]
    [InlineData("ενο ")]  // a different letter
    [InlineData("εν ")]   // shorter
    [InlineData("ενά ")]  // an accent we wouldn't add
    public void Retouch_types_nothing_if_the_text_was_changed(string screen)
    {
        _screen = screen;

        Create().Retouch("ενα ", "ένα ").Should().BeFalse();

        _sender.Batches.Should().BeEmpty();
    }

    [Fact]
    public void Retouch_types_nothing_if_the_user_typed_ahead()
    {
        _guard.OnKeyCaptured(isTypingKey: true);

        Create().Retouch("ενα ", "ένα ").Should().BeFalse();

        _sender.Batches.Should().BeEmpty();
    }

    private static List<ushort> Downs(INPUT[] batch) =>
        [.. batch.Where(i => (i.U.ki.dwFlags & KeyUp) == 0).Select(i => i.U.ki.wVk)];

    [Fact]
    public void Keeps_the_capital_the_app_gave_the_first_letter()
    {
        _textMatch = TextMatch.FirstLetterCapitalised; // Word: ψονταινερ → Ψονταινερ at a sentence start

        Create().Replace("ηελλο ", "hello ", Lang.English).Should().BeTrue();

        var downs = _sender.Batches.Single().Where(i => (i.U.ki.dwFlags & KeyUp) == 0).Select(i => i.U.ki.wVk).ToList();
        downs.Count(vk => vk == VirtualKeys.Back).Should().Be(6, "capitalising doesn't change the length");
        downs.SkipWhile(vk => vk == VirtualKeys.Back).Take(2).Should().Equal((ushort)VirtualKeys.Shift, (ushort)'H');
        downs.Skip(8).Should().NotContain((ushort)VirtualKeys.Shift, "only the first letter is capitalised");
    }

    [Theory]
    [InlineData(TextMatch.Match)]
    [InlineData(TextMatch.Unknown)] // no TextPattern: the other guards still apply
    public void Types_when_the_text_matches_or_cannot_be_read(TextMatch match)
    {
        _textMatch = match;

        Create().Replace("ηελλο ", "hello ", Lang.English).Should().BeTrue();

        _sender.Batches.Single().Count(i => i.U.ki.wVk == VirtualKeys.Back && (i.U.ki.dwFlags & KeyUp) == 0)
            .Should().Be(6, "one Backspace per character being replaced");
    }

    [Fact]
    public void Types_nothing_if_focus_moved_to_another_control()
    {
        _currentFocus = 0x200; // e.g. a popup took focus after the word was typed

        Create().Replace(new string('x', 12), "application ", Lang.English).Should().BeFalse();

        _sender.Batches.Should().BeEmpty();
        _layouts.SwitchRequests.Should().BeEmpty();
    }

    [Fact]
    public void Types_nothing_if_focus_moves_during_the_layout_switch()
    {
        _layouts.PollsUntilSwitch = 2;
        _layouts.OnSwitched = () => _currentFocus = 0x200;

        Create().Replace(new string('x', 12), "application ", Lang.English).Should().BeFalse();

        _sender.Batches.Should().BeEmpty();
    }

    [Theory]
    [InlineData("application\n")]
    [InlineData("application\t")]
    public void Never_retypes_enter_or_tab_into_a_terminal(string text)
    {
        _commandWindow = true;

        Create().Replace(new string('x', 12), text, Lang.English).Should().BeFalse();

        _sender.Batches.Should().BeEmpty();
    }

    [Fact]
    public void Space_ended_corrections_still_work_in_a_terminal()
    {
        _commandWindow = true;

        Create().Replace(new string('x', 12), "application ", Lang.English).Should().BeTrue();
    }

    [Fact]
    public void Enter_ended_corrections_work_outside_terminals()
    {
        Create().Replace(new string('x', 12), "application\n", Lang.English).Should().BeTrue();
    }

    /// <summary>The characters a batch types, decoded the way the target layout would.</summary>
    private static string Typed(INPUT[] batch, Lang layout)
    {
        var strokes = new List<KeyStroke>();
        var shift = false;
        foreach (var ki in batch.Select(i => i.U.ki))
        {
            if (ki.wVk == VirtualKeys.Shift)
            {
                shift = (ki.dwFlags & KeyUp) == 0;
            }
            else if ((ki.dwFlags & KeyUp) == 0 && ki.wVk != VirtualKeys.Back)
            {
                strokes.Add(new KeyStroke(ki.wVk, shift));
            }
        }

        return new DualDecoder(StaticLayouts.Greek, StaticLayouts.English)
            .Decode(strokes.Where(s => s.VirtualKey != VirtualKeys.Space).ToList(), layout);
    }

    [Fact]
    public void Switches_the_layout_before_typing()
    {
        _layouts.PollsUntilSwitch = 3; // the target app takes a moment to apply the switch

        Create().Replace(new string('x', 12), "application ", Lang.English).Should().BeTrue();

        _sender.LayoutAtSend.Should().Equal(Lang.English);
    }

    [Fact]
    public void Types_the_replacement_as_real_keys_of_the_target_layout()
    {
        Create().Replace(new string('x', 12), "application ", Lang.English).Should().BeTrue();

        var batch = _sender.Batches.Single();
        batch.Should().NotContain(i => (i.U.ki.dwFlags & Unicode) != 0);
        batch.Take(24).Should().AllSatisfy(i => i.U.ki.wVk.Should().Be(VirtualKeys.Back));
        Typed(batch, Lang.English).Should().Be("application");
        batch.Where(i => i.U.ki.wVk == 'A').Should().AllSatisfy(i => i.U.ki.wScan.Should().Be(1000 + 'A'));
    }

    [Fact]
    public void Greek_with_tonos_and_final_sigma_is_typed_with_dead_key_and_W()
    {
        _layouts.Current = Lang.English;

        Create().Replace(new string('x', 9), "πράξεις ", Lang.Greek).Should().BeTrue();

        Typed(_sender.Batches.Single(), Lang.Greek).Should().Be("πράξεις");
    }

    [Fact]
    public void Caps_lock_is_taken_into_account()
    {
        _layouts.Current = Lang.English;
        _keyboard.CapsLockOn = true;

        Create().Replace(new string('x', 4), "Ωρα ", Lang.Greek).Should().BeTrue();

        // With Caps Lock on, Ω needs no Shift and ρ, α need Shift.
        var keys = _sender.Batches.Single().Select(i => i.U.ki).Where(k => k.wVk != VirtualKeys.Back).ToList();
        keys.TakeWhile(k => k.wVk != 'V').Should().NotContain(k => k.wVk == VirtualKeys.Shift);
        keys.Count(k => k.wVk == VirtualKeys.Shift).Should().Be(4); // down+up for ρ and for α
    }

    [Fact]
    public void Falls_back_to_unicode_for_characters_no_key_types()
    {
        _layouts.Current = Lang.English;

        Create().Replace(new string('x', 2), "€ ", Lang.English).Should().BeTrue();

        _sender.Batches.Single().Should().Contain(i => (i.U.ki.dwFlags & Unicode) != 0 && i.U.ki.wScan == '€');
    }

    [Fact]
    public void Does_not_request_a_switch_when_the_target_layout_is_already_active()
    {
        _layouts.Current = Lang.English;

        Create().Replace(new string('x', 4), "abc ", Lang.English).Should().BeTrue();

        _layouts.SwitchRequests.Should().BeEmpty();
    }

    [Fact]
    public void Falls_back_to_the_users_layout_hotkey_once_when_the_switch_request_is_ignored()
    {
        _layouts.PollsUntilSwitch = int.MaxValue; // the app ignores WM_INPUTLANGCHANGEREQUEST
        _layouts.HotkeyWorks = true;

        Create(TimeSpan.FromMilliseconds(30)).Replace(new string('x', 12), "application ", Lang.English).Should().BeTrue();

        _layouts.HotkeyPresses.Should().Be(1);
        _sender.LayoutAtSend.Should().Equal(Lang.English);
    }

    [Fact]
    public void Types_nothing_when_the_switch_never_takes_effect()
    {
        _layouts.PollsUntilSwitch = int.MaxValue;

        Create(TimeSpan.FromMilliseconds(30)).Replace(new string('x', 12), "application ", Lang.English).Should().BeFalse();

        _sender.Batches.Should().BeEmpty();
        _layouts.HotkeyPresses.Should().Be(1, "the hotkey fallback is tried once, then it gives up");
    }

    [Fact]
    public void Types_nothing_when_the_user_has_typed_ahead()
    {
        _guard.OnKeyCaptured(isTypingKey: true);   // the delimiter, being processed
        _guard.MarkProcessed(_guard.Captured);
        _guard.OnKeyCaptured(isTypingKey: true);   // a letter typed meanwhile

        Create().Replace(new string('x', 12), "application ", Lang.English).Should().BeFalse();

        _sender.Batches.Should().BeEmpty();
    }

    [Fact]
    public void Modifier_keys_pressed_meanwhile_do_not_count_as_typing_ahead()
    {
        _guard.OnKeyCaptured(isTypingKey: true);
        _guard.MarkProcessed(_guard.Captured);
        _guard.OnKeyCaptured(isTypingKey: false);  // e.g. Shift for the next word's capital

        Create().Replace(new string('x', 12), "application ", Lang.English).Should().BeTrue();
    }

    [Fact]
    public void Types_nothing_while_ctrl_alt_or_win_is_held()
    {
        _keyboard.CommandModifierHeld = true;

        Create().Replace(new string('x', 12), "application ", Lang.English).Should().BeFalse();

        _sender.Batches.Should().BeEmpty();
        _layouts.SwitchRequests.Should().BeEmpty();
    }

    [Fact]
    public void Waits_for_shift_to_be_released()
    {
        _keyboard.ShiftHeldPolls = 5;

        Create().Replace(new string('x', 12), "application ", Lang.English).Should().BeTrue();

        Typed(_sender.Batches.Single(), Lang.English).Should().Be("application");
    }

    [Fact]
    public void Types_nothing_if_shift_stays_held()
    {
        _keyboard.ShiftHeldPolls = int.MaxValue;

        Create(TimeSpan.FromMilliseconds(30)).Replace(new string('x', 12), "application ", Lang.English).Should().BeFalse();

        _sender.Batches.Should().BeEmpty();
    }

    [Fact]
    public void Turning_caps_lock_off_presses_caps_lock_only_when_it_is_on()
    {
        Create().TurnOffCapsLock();
        _sender.Batches.Should().BeEmpty();

        _keyboard.CapsLockOn = true;
        Create().TurnOffCapsLock();
        _sender.Batches.Single().Select(i => i.U.ki.wVk).Should().Equal((ushort)VirtualKeys.Capital, (ushort)VirtualKeys.Capital);
    }

    [Fact]
    public void Reports_failure_when_SendInput_is_blocked()
    {
        _sender.Blocked = true;

        Create().Replace(new string('x', 12), "application ", Lang.English).Should().BeFalse();
    }

    private sealed class FakeVerifier(CorrectorTests test) : ITextVerifier
    {
        public TextMatch BeforeCaret(string expected)
        {
            test._textChecked = expected;
            return test._textMatch;
        }

        public string? ReadBeforeCaret(int length) => test._screen;
    }

    private sealed class FakeKeyboard : IKeyboardState
    {
        public bool CommandModifierHeld { get; set; }

        public int ShiftHeldPolls { get; set; }

        public bool ShiftHeld => ShiftHeldPolls-- > 0;

        public bool CapsLockOn { get; set; }
    }

    private sealed class FakeSwitcher(Lang initial) : ILayoutSwitcher
    {
        private Lang? _pending;
        private int _polls;

        public Lang Current { get; set; } = initial;

        public int PollsUntilSwitch { get; set; }

        public List<Lang> SwitchRequests { get; } = [];

        public Action? OnSwitched { get; set; }

        public Lang? CurrentLayout()
        {
            if (_pending is { } target && ++_polls > PollsUntilSwitch)
            {
                Current = target;
                _pending = null;
                OnSwitched?.Invoke();
            }

            return Current;
        }

        public void SwitchTo(Lang lang)
        {
            SwitchRequests.Add(lang);
            _pending = lang;
            _polls = 0;
        }

        public bool HotkeyWorks { get; set; }

        public int HotkeyPresses { get; private set; }

        public void SwitchWithHotkey()
        {
            HotkeyPresses++;
            if (HotkeyWorks && _pending is { } target)
            {
                Current = target;
                _pending = null;
            }
        }
    }

    private sealed class FakeSender(FakeSwitcher layouts) : IInputSender
    {
        public bool Blocked { get; set; }

        public List<INPUT[]> Batches { get; } = [];

        public List<Lang> LayoutAtSend { get; } = [];

        /// <summary>Runs inside the first Send, as a key the user presses mid-send would.</summary>
        public Action? DuringSend { get; set; }

        public int Attempts { get; private set; }

        public uint Send(INPUT[] inputs)
        {
            Attempts++;
            if (DuringSend is { } during)
            {
                DuringSend = null;
                during();
            }

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
