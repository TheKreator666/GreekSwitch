using GreekSwitch.Core.Input;

namespace GreekSwitch.Core.Tests.Input;

public class WordBufferTests
{
    private static readonly Delimiter Space = new(DelimiterKind.Space, " ");
    private static readonly Delimiter Period = new(DelimiterKind.Punctuation, ".");

    private static KeyStroke K(char c) => new(c);

    [Fact]
    public void Backspace_mid_word_pops_the_last_key()
    {
        var buffer = new WordBuffer();
        foreach (var c in "HELP")
        {
            buffer.Append(K(c), Lang.English);
        }

        buffer.Pop().Should().BeTrue();
        buffer.Append(K('L'), Lang.English);
        buffer.Append(K('O'), Lang.English);

        buffer.Keys.Select(k => (char)k.VirtualKey).Should().Equal("HELLO");
    }

    [Fact]
    public void Reset_clears_the_buffer()
    {
        var buffer = new WordBuffer();
        buffer.Append(K('A'), Lang.Greek);
        buffer.Append(K('B'), Lang.Greek);

        buffer.Reset();

        buffer.IsEmpty.Should().BeTrue();
        buffer.Complete(Space).Should().BeNull();
    }

    [Fact]
    public void Complete_returns_the_word_with_layout_and_delimiter_then_starts_fresh()
    {
        var buffer = new WordBuffer();
        buffer.Append(K('U'), Lang.English);
        buffer.Append(K('A'), Lang.Greek); // layout of the first key wins

        var word = buffer.Complete(Space)!;

        word.Keys.Should().HaveCount(2);
        word.Layout.Should().Be(Lang.English);
        word.Delimiter.Should().Be(Space);
        buffer.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Backspace_past_the_start_taints_the_next_word()
    {
        var buffer = new WordBuffer();
        buffer.Pop().Should().BeFalse();
        buffer.Append(K('A'), Lang.English);

        buffer.Complete(Space)!.Tainted.Should().BeTrue();

        buffer.Append(K('A'), Lang.English);
        buffer.Complete(Space)!.Tainted.Should().BeFalse();
    }

    [Fact]
    public void A_word_glued_to_a_preceding_period_is_flagged()
    {
        var buffer = new WordBuffer();
        buffer.Append(K('M'), Lang.English);
        buffer.Complete(Period);
        buffer.Append(K('C'), Lang.English);

        buffer.Complete(Space)!.FollowsPeriod.Should().BeTrue();

        buffer.Append(K('C'), Lang.English);
        buffer.Complete(Space)!.FollowsPeriod.Should().BeFalse();
    }

    [Fact]
    public void Restore_reloads_keys_for_continued_typing()
    {
        var buffer = new WordBuffer();
        buffer.Restore([K('A'), K('B')], Lang.Greek);

        var word = buffer.Complete(Space)!;
        word.Keys.Should().HaveCount(2);
        word.Layout.Should().Be(Lang.Greek);
    }
}
