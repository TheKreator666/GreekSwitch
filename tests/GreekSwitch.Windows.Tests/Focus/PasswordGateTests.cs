using GreekSwitch.Windows.Focus;

namespace GreekSwitch.Windows.Tests.Focus;

public class PasswordGateTests
{
    private static readonly FocusKey Notepad = new(0x100, 0);
    private static readonly FocusKey LoginBox = new(0x200, 0);
    private static readonly FocusKey WebField = new(0x300, 17);

    [Fact]
    public void Blocks_until_ui_automation_answers()
    {
        var gate = new PasswordGate();

        gate.OnFocus(Notepad, wouldCapture: true).Should().BeTrue("a check must be started");
        gate.Blocked.Should().BeTrue("fail closed while waiting");

        gate.OnAnswer(Notepad, new PasswordCheck(false, Notepad.Window));
        gate.Blocked.Should().BeFalse();
    }

    [Fact]
    public void A_password_box_stays_blocked()
    {
        var gate = new PasswordGate();
        gate.OnFocus(LoginBox, wouldCapture: true);

        gate.OnAnswer(LoginBox, new PasswordCheck(true, LoginBox.Window));

        gate.Blocked.Should().BeTrue();
    }

    [Fact]
    public void A_timeout_keeps_it_blocked()
    {
        var gate = new PasswordGate();
        gate.OnFocus(Notepad, wouldCapture: true);

        gate.OnAnswer(Notepad, null);

        gate.Blocked.Should().BeTrue();
    }

    [Fact]
    public void Native_controls_are_cached_and_not_queried_again()
    {
        var gate = new PasswordGate();
        gate.OnFocus(Notepad, wouldCapture: true);
        gate.OnAnswer(Notepad, new PasswordCheck(false, Notepad.Window));
        gate.OnFocus(LoginBox, wouldCapture: true);

        gate.OnFocus(Notepad, wouldCapture: true).Should().BeFalse("the answer is cached");
        gate.Blocked.Should().BeFalse();
    }

    [Fact]
    public void Windowless_fields_are_never_cached()
    {
        // In a browser every field shares one window: an answer for one field says nothing about the next.
        var gate = new PasswordGate();
        gate.OnFocus(WebField, wouldCapture: true);
        gate.OnAnswer(WebField, new PasswordCheck(false, 0));

        gate.OnFocus(WebField with { ChildId = 18 }, wouldCapture: true).Should().BeTrue();
        gate.Blocked.Should().BeTrue();
    }

    [Fact]
    public void An_answer_about_another_window_is_not_cached()
    {
        // Focus moved on before the query ran, so UIA described a different control.
        var gate = new PasswordGate();
        gate.OnFocus(Notepad, wouldCapture: true);
        gate.OnAnswer(Notepad, new PasswordCheck(false, LoginBox.Window));

        gate.OnFocus(Notepad, wouldCapture: true).Should().BeTrue("nothing reliable was learned about Notepad");
    }

    [Fact]
    public void A_stale_answer_does_not_unblock_the_current_field()
    {
        var gate = new PasswordGate();
        gate.OnFocus(Notepad, wouldCapture: true);
        gate.OnFocus(LoginBox, wouldCapture: true);

        gate.OnAnswer(Notepad, new PasswordCheck(false, Notepad.Window)).Should().BeFalse();
        gate.Blocked.Should().BeTrue("the login box is still unanswered");
    }

    [Fact]
    public void Nothing_is_queried_when_capture_would_be_off_anyway()
    {
        var gate = new PasswordGate();

        gate.OnFocus(Notepad, wouldCapture: false).Should().BeFalse();
    }
}
