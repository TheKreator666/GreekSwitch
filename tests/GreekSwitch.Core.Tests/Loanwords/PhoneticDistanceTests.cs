using GreekSwitch.Core.Loanwords;

namespace GreekSwitch.Core.Tests.Loanwords;

public class PhoneticDistanceTests
{
    private static readonly LoanwordOptions Options = new();

    [Theory]
    [InlineData("σέρβερ", "S ER1 V ER0", 0.0)]                // identical
    [InlineData("κοντέινερ", "K AH0 N T EY1 N ER0", 0.3)]     // o for a: one vowel substitution
    [InlineData("κομπιούτερ", "K AH0 M P Y UW1 T ER0", 0.5)]  // o/a 0.3 + i/y 0.2
    [InlineData("μπάτζετ", "B AH1 JH AH0 T", 0.3)]            // b d z e t vs b a d z a t: e/a 0.3
    [InlineData("λόμπι", "L AA1 B IY0", 0.6)]                 // o/a 0.3 + mp for b (cluster) 0.3
    [InlineData("μπάγκ", "B AH1 G", 0.0)]
    [InlineData("σπιτ", "S P IH1 T IY0", 0.5)]               // a vowel more: 0.5
    [InlineData("σέβερ", "S ER1 V ER0", 1.0)]                 // a consonant fewer: 1.0
    [InlineData("σέρφερ", "S ER1 V ER0", 0.5)]                // f for v: voicing only
    [InlineData("γάτα", "G AA1 T AH0", 1.0)]                  // γ is not g (transliterations write γκ)
    public void Weighs_edits_by_kind(string greek, string arpabet, double expected)
    {
        var g = GreekPhonetics.Transcribe(greek)!;
        var e = ArpabetPhonetics.Transcribe(arpabet.Split(' '))!;

        PhoneticDistance.Distance(g.Phonemes, e.Phonemes, Options).Should().BeApproximately(expected, 1e-9);
    }

    [Fact]
    public void Score_is_normalised_by_length_and_rewards_matching_stress()
    {
        var greek = GreekPhonetics.Transcribe("κοντέινερ")!;
        var container = ArpabetPhonetics.Transcribe("K AH0 N T EY1 N ER0".Split(' '))!;

        PhoneticDistance.Score(greek, container.Phonemes, container.StressFromEnd, Options)
            .Should().BeApproximately(1 - (0.3 / 9) + Options.StressBonus, 1e-9);
        PhoneticDistance.Score(greek, container.Phonemes, englishStressFromEnd: 0, Options)
            .Should().BeApproximately(1 - (0.3 / 9), 1e-9);
    }

    [Fact]
    public void Distance_is_symmetric_for_clusters()
    {
        Phoneme[] mp = [Phoneme.L, Phoneme.O, Phoneme.M, Phoneme.P, Phoneme.I];
        Phoneme[] b = [Phoneme.L, Phoneme.O, Phoneme.B, Phoneme.I];

        PhoneticDistance.Distance(mp, b, Options).Should().Be(PhoneticDistance.Distance(b, mp, Options)).And.Be(Options.ClusterConfusion);
    }
}
