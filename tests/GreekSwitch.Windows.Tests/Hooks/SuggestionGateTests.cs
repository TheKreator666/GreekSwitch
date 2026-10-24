using GreekSwitch.Windows.Hooks;

namespace GreekSwitch.Windows.Tests.Hooks;

/// <summary>The flag behind the hook's only key-swallowing exception.</summary>
public class SuggestionGateTests
{
    [Fact]
    public void Closed_by_default_so_tab_always_passes_through()
    {
        var gate = new SuggestionGate();

        gate.Visible.Should().BeFalse();
        gate.TryTake().Should().BeFalse();
    }

    [Fact]
    public void A_visible_suggestion_is_taken_exactly_once()
    {
        var gate = new SuggestionGate();
        gate.Open();

        gate.TryTake().Should().BeTrue();
        gate.TryTake().Should().BeFalse("the next Tab is an ordinary Tab");
        gate.Visible.Should().BeFalse();
    }

    [Fact]
    public void Closing_it_lets_tab_through_again()
    {
        var gate = new SuggestionGate();
        gate.Open();
        gate.Close();

        gate.TryTake().Should().BeFalse();
    }
}
