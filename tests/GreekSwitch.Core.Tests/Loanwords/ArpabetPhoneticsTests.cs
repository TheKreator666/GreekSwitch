using GreekSwitch.Core.Loanwords;

namespace GreekSwitch.Core.Tests.Loanwords;

public class ArpabetPhoneticsTests
{
    [Theory]
    [InlineData("K AH0 N T EY1 N ER0", "k a n t e i n e r", 2)] // container
    [InlineData("K AH0 M P Y UW1 T ER0", "k a m p y u t e r", 1)] // computer: Y is not a vowel
    [InlineData("S ER1 V ER0", "s e r v e r", 1)]             // server
    [InlineData("D IH0 P L OY1", "d i p l o i", 1)]           // deploy
    [InlineData("M IY1 T IH0 NG", "m i t i n g", 1)]          // meeting: NG = n g
    [InlineData("L IH1 NG K", "l i n k", 0)]                  // link: NG before K = n
    [InlineData("CH AE1 T", "t s a t", 0)]                    // chat
    [InlineData("JH AA1 B", "d z a b", 0)]                    // job
    [InlineData("W EH1 B", "u e b", 0)]                       // web
    [InlineData("SH OW1", "s o", 0)]                          // show
    [InlineData("TH IH1 NG K", "th i n k", 0)]
    [InlineData("DH AH0", "dh a", -1)]                        // no primary stress
    [InlineData("HH AW1 S", "h a u s", 1)]                   // AW = a u, stress on the a
    public void Maps_ARPAbet_onto_the_shared_alphabet(string arpabet, string expected, int stressFromEnd)
    {
        var word = ArpabetPhonetics.Transcribe(arpabet.Split(' '))!;

        word.ToString().Should().Be(expected);
        word.StressFromEnd.Should().Be(stressFromEnd);
    }

    [Fact]
    public void An_unknown_symbol_gives_null()
    {
        ArpabetPhonetics.Transcribe(["K", "XX1", "T"]).Should().BeNull();
        ArpabetPhonetics.Transcribe([]).Should().BeNull();
    }
}
