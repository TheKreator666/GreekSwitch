using GreekSwitch.Core.Text;

namespace GreekSwitch.Core.Tests.Text;

public class GreekTextTests
{
    [Theory]
    [InlineData("σασ", "σας")]
    [InlineData("ςορλδ", "σορλδ")]
    [InlineData("πράξεισ", "πράξεις")]
    [InlineData("ςας;", "σας;")]
    [InlineData("σ", "σ")]
    [InlineData("ΣΑΣ", "ΣΑΣ")]
    [InlineData("hello", "hello")]
    public void FixFinalSigma_uses_final_form_only_at_word_end(string input, string expected)
    {
        GreekText.FixFinalSigma(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("καλημέρα", "καλημερα")]
    [InlineData("Ϊ ϋ ΐ ΰ", "Ι υ ι υ")]
    [InlineData("hello", "hello")]
    public void StripDiacritics_removes_tonos_and_dialytika(string input, string expected)
    {
        GreekText.StripDiacritics(input).Should().Be(expected);
    }

    [Fact]
    public void NormalizeForMatch_ignores_case_accents_and_final_sigma()
    {
        GreekText.NormalizeForMatch("Πράξεις").Should().Be(GreekText.NormalizeForMatch("πραξεισ"));
    }
}
