using GreekSwitch.Windows.Hooks;

namespace GreekSwitch.Windows.Tests.Hooks;

/// <summary>
/// Keys typed while capture was off make the word in progress incomplete when capture resumes,
/// but only keys typed in the field being typed into now.
/// </summary>
public class MissedKeysTests
{
    [Fact]
    public void Keys_dropped_while_capture_was_off_in_the_same_field_count()
    {
        var missed = new MissedKeys();
        missed.CaptureStopped(dropped: 5); // focus arrived, password check pending

        missed.MissedOnResume(dropped: 7).Should().BeTrue("the user kept typing during the check");
    }

    [Fact]
    public void Nothing_dropped_means_nothing_missed()
    {
        var missed = new MissedKeys();
        missed.CaptureStopped(dropped: 5);

        missed.MissedOnResume(dropped: 5).Should().BeFalse();
    }

    /// <summary>
    /// Live run: a password typed in the browser (capture off), then Notepad opened. Capture was
    /// already off at the focus change, so the first word in Notepad was wrongly judged partial.
    /// </summary>
    [Fact]
    public void Keys_dropped_in_the_previous_field_do_not_count()
    {
        var missed = new MissedKeys();
        missed.CaptureStopped(dropped: 0); // into the password field
        missed.FocusChanged(droppedAtFocus: 12); // 12 password keys, then Notepad got focus

        missed.MissedOnResume(dropped: 12).Should().BeFalse("those keys went to the password field");
    }

    [Fact]
    public void Keys_dropped_after_the_focus_change_still_count()
    {
        var missed = new MissedKeys();
        missed.CaptureStopped(dropped: 0);
        missed.FocusChanged(droppedAtFocus: 12);

        missed.MissedOnResume(dropped: 14).Should().BeTrue("two keys were typed into the new field during the check");
    }

    [Fact]
    public void A_focus_event_handled_late_uses_the_count_from_when_it_happened()
    {
        var missed = new MissedKeys();
        missed.CaptureStopped(dropped: 0);
        missed.FocusChanged(droppedAtFocus: 12); // read on the hook thread at the focus change

        // The worker gets to it after 3 more keys were dropped in the new field.
        missed.MissedOnResume(dropped: 15).Should().BeTrue();
    }
}
