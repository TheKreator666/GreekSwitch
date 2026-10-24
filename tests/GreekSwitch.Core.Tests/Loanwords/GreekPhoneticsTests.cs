using GreekSwitch.Core.Loanwords;

namespace GreekSwitch.Core.Tests.Loanwords;

public class GreekPhoneticsTests
{
    [Theory]
    [InlineData("κοντέινερ", "k o n t e i n e r")]  // έι: tonos on the first letter, two vowels; medial ντ = nt
    [InlineData("σέρβερ", "s e r v e r")]
    [InlineData("κομπιούτερ", "k o m p i u t e r")] // medial μπ = mp, ου = u
    [InlineData("ντεπλόι", "d e p l o i")]          // initial ντ = d, όι two vowels
    [InlineData("μπαγκ", "b a g")]                  // initial μπ = b, γκ = g
    [InlineData("φίντμπακ", "f i d b a k")]         // ντ, μπ next to a consonant = d, b
    [InlineData("άπντεϊτ", "a p d e i t")]          // ϊ: not a digraph
    [InlineData("ντάουνλοουντ", "d a u n l o u d")] // final ντ = d
    [InlineData("τσεκάρω", "t s e k a r o")]
    [InlineData("τζάμι", "d z a m i")]
    [InlineData("θάλασσα", "th a l a s a")]         // σσ is one s
    [InlineData("δέντρο", "dh e d r o")]           // ντ before a consonant = d
    [InlineData("χαρά", "h a r a")]
    [InlineData("ξύλο", "k s i l o")]
    [InlineData("ψάρι", "p s a r i")]
    [InlineData("καιρός", "k e r o s")]             // αι = e, ς = s
    [InlineData("είναι", "i n e")]                  // ει = i
    [InlineData("οικονομία", "i k o n o m i a")]    // οι = i
    [InlineData("υπολογιστής", "i p o l o y i s t i s")]
    [InlineData("ωραίο", "o r e o")]
    [InlineData("βουνό", "v u n o")]
    [InlineData("φως", "f o s")]
    public void Transcribes_letters_and_digraphs(string word, string expected)
    {
        GreekPhonetics.Transcribe(word)!.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData("αυτό", "a f t o")]   // before a voiceless consonant
    [InlineData("αύριο", "a v r i o")] // before a voiced one
    [InlineData("ευχή", "e f h i")]
    [InlineData("Εύα", "e v a")]
    [InlineData("ευ", "e f")]          // at the end
    [InlineData("άυλος", "a i l o s")] // tonos on α: not a digraph
    [InlineData("αϋπνία", "a i p n i a")] // dialytika on υ: not a digraph
    public void Reads_av_and_ev(string word, string expected)
    {
        GreekPhonetics.Transcribe(word)!.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData("γάλα", "gh a l a")]
    [InlineData("γέλιο", "y e l i o")]   // before ε
    [InlineData("γη", "y i")]            // before η
    [InlineData("γαίμα", "y e m a")]     // before αι (an e sound)
    [InlineData("γιαγιά", "y a y a")]    // γι + vowel is one y
    [InlineData("γίνεται", "y i n e t e")] // γί keeps its stressed i
    [InlineData("άγγελος", "a n g e l o s")]
    [InlineData("σόφτγουερ", "s o f t u e r")] // γου + vowel is English w
    [InlineData("γουέμπ", "u e b")]
    public void Reads_gamma_by_what_follows(string word, string expected)
    {
        GreekPhonetics.Transcribe(word)!.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData("κοντέινερ", 2)] // o e i e: stressed e is third from the end
    [InlineData("ντεπλόι", 1)]
    [InlineData("σέρβερ", 1)]
    [InlineData("κομπιούτερ", 1)]
    [InlineData("καιρός", 0)]    // single vowel e o, stress on o
    [InlineData("οικονομία", 1)]
    [InlineData("ντεπλοι", -1)]  // no tonos: unknown
    public void Keeps_the_tonos_position_counted_from_the_end(string word, int stressFromEnd)
    {
        GreekPhonetics.Transcribe(word)!.StressFromEnd.Should().Be(stressFromEnd);
    }

    [Fact]
    public void Ignores_case_and_final_sigma_form()
    {
        GreekPhonetics.Transcribe("ΣΕΡΒΕΡΣ")!.ToString().Should().Be("s e r v e r s");
        GreekPhonetics.Transcribe("Κοντέινερ")!.ToString().Should().Be("k o n t e i n e r");
    }

    [Theory]
    [InlineData("")]
    [InlineData("server")]
    [InlineData("σέρβερ1")]
    [InlineData("σέρ-βερ")]
    [InlineData("καλή μέρα")]
    public void Rejects_anything_but_Greek_letters(string word)
    {
        GreekPhonetics.Transcribe(word).Should().BeNull();
    }
}
