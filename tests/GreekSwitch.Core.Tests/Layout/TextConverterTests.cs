using GreekSwitch.Core.Layout;

namespace GreekSwitch.Core.Tests.Layout;

/// <summary>"Convert selection": every word re-read in the other layout.</summary>
public class TextConverterTests
{
    private readonly TextConverter _converter = new(StaticLayouts.Greek, StaticLayouts.English);

    [Theory]
    [InlineData("ηελλο ςορλδ", "hello world")]
    [InlineData("hello world", "ηελλο σορλδ")]     // Greek spelling: σ inside a word, never ς
    [InlineData("kalhm;era", "καλημέρα")]
    [InlineData("Kalhm;era sas", "Καλημέρα σας")]       // final sigma at the end of a Greek word
    [InlineData("καλημέρα", "kalhm;era")]
    [InlineData("ηελλο, ςορλδ!", "hello, world!")]      // punctuation that is the same in both layouts
    [InlineData("den u;elv na ;eruv", "δεν θέλω να έρθω")]
    [InlineData("ΗΕΛΛΟ", "HELLO")]
    public void Converts_each_word_to_the_other_layout(string text, string expected)
    {
        _converter.Convert(text).Should().Be(expected);
    }

    [Fact]
    public void Keeps_whitespace_and_line_breaks()
    {
        _converter.Convert("ηελλο\n\tςορλδ  !").Should().Be("hello\n\tworld  !");
    }

    [Fact]
    public void Mixed_text_converts_each_word_from_its_own_script()
    {
        _converter.Convert("I love ελλάδα").Should().Be("Ι λοωε ell;ada");
    }

    [Fact]
    public void Leaves_words_it_cannot_type_unchanged()
    {
        _converter.Convert("€uro 2024").Should().Be("€uro 2024");
    }
}
