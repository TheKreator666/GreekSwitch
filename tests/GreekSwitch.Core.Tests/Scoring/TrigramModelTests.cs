using System.Text;
using GreekSwitch.Core.Scoring;
using GreekSwitch.Core.Tests.Support;
using Xunit.Abstractions;

namespace GreekSwitch.Core.Tests.Scoring;

[Collection(LanguageDataGroup.Name)]
public class TrigramModelTests(LanguageDataFixture data, ITestOutputHelper output)
{
    private readonly ITrigramModel _model = data.Trigrams;

    [Fact]
    public void Cat_is_plausible_english_and_psat_is_implausible_greek()
    {
        var cat = _model.LogProb("cat", Lang.English);
        var psat = _model.LogProb("ψατ", Lang.Greek);
        output.WriteLine($"cat/EN {cat:F2}  ψατ/GR {psat:F2}");

        cat.Should().BeGreaterThan(-3.5);
        psat.Should().BeLessThan(-4.0);
        cat.Should().BeGreaterThan(psat + 1.0);
    }

    [Theory]
    [InlineData("application", Lang.English, "αππλιψατιον", Lang.Greek)]
    [InlineData("hello", Lang.English, "ηελλο", Lang.Greek)]
    [InlineData("world", Lang.English, "ςορλδ", Lang.Greek)]
    [InlineData("που", Lang.Greek, "poy", Lang.English)]
    [InlineData("καλημέρα", Lang.Greek, "kalhm;era", Lang.English)]
    [InlineData("πράξεις", Lang.Greek, "pr;ajeiw", Lang.English)]
    public void The_intended_reading_scores_higher_than_the_wrong_layout_reading(
        string intended, Lang intendedLang, string wrong, Lang wrongLang)
    {
        var good = _model.LogProb(intended, intendedLang);
        var bad = _model.LogProb(wrong, wrongLang);
        output.WriteLine($"{intended}/{intendedLang} {good:F2}  vs  {wrong}/{wrongLang} {bad:F2}");

        good.Should().BeGreaterThan(bad);
    }

    [Fact]
    public void Score_ignores_accents_and_case()
    {
        _model.LogProb("Καλημέρα", Lang.Greek).Should().BeApproximately(_model.LogProb("καλημερα", Lang.Greek), 1e-9);
        _model.LogProb("HELLO", Lang.English).Should().BeApproximately(_model.LogProb("hello", Lang.English), 1e-9);
    }

    [Fact]
    public void Final_sigma_in_the_middle_of_a_word_is_penalised()
    {
        _model.LogProb("ςασ", Lang.Greek).Should().BeLessThan(_model.LogProb("σας", Lang.Greek) - 1.0);
        _model.LogProb("σας", Lang.Greek).Should().BeApproximately(_model.LogProb("σασ", Lang.Greek), 1e-9);
    }

    [Fact]
    public void Characters_outside_the_alphabet_are_heavily_penalised()
    {
        _model.LogProb("hel;lo", Lang.English).Should().BeLessThan(_model.LogProb("hello", Lang.English) - 1.0);
    }

    [Fact]
    public void Load_reads_the_text_format_and_smooths_unseen_trigrams()
    {
        const string text = "# GreekSwitch trigram model v1\n^^a\t10\n^ab\t10\nab$\t10\n";
        var model = TrigramModel.Load(new MemoryStream(Encoding.UTF8.GetBytes(text)), Lang.English);

        model.TrigramCount.Should().Be(3);
        model.LogProb("ab").Should().BeGreaterThan(model.LogProb("ba"));
    }

    [Fact]
    public void Load_rejects_malformed_lines()
    {
        var act = () => TrigramModel.Load(new MemoryStream(Encoding.UTF8.GetBytes("abcd 1\n")), Lang.English);
        act.Should().Throw<FormatException>();
    }
}
