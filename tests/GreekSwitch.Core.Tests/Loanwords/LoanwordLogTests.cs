using GreekSwitch.Core.Loanwords;
using GreekSwitch.Core.Tests.Support;
using GreekSwitch.Core.Text;

namespace GreekSwitch.Core.Tests.Loanwords;

/// <summary>The detector's equivalent of NoTextInLogsTests: every log path, and no word in any of them.</summary>
[Collection(LanguageDataGroup.Name)]
public class LoanwordLogTests(LanguageDataFixture data)
{
    [Fact]
    public void Detector_logs_tiers_and_scores_but_never_the_words()
    {
        var logger = new CapturingLogger();
        var pairs = new LoanwordPairStore([new LoanwordPair("ρεπό", "dayoff")]);
        var detector = new LoanwordDetector(pairs, data.Dictionaries, data.Frequencies, options: new LoanwordOptions { AutoReplaceStrongMatches = true }, logger: logger);
        detector.UseIndex(data.Phonetic);

        detector.Check("κοντέινερ").Should().BeOfType<LoanwordSuggest>();   // suggestion
        detector.Check("ντεπλόι").Should().BeOfType<LoanwordReplace>();     // tier 2
        detector.Check("ρεπό").Should().BeOfType<LoanwordReplace>();        // tier 1, Settings pair
        for (var i = 0; i < 3; i++)
        {
            detector.Accept("κομπιούτερ", "computer");                      // accepted, then promoted
        }

        detector.Check("κομπιούτερ").Should().BeOfType<LoanwordReplace>();  // tier 1, promoted
        detector.Accept("σέρβερ", "σέρβερ");                                  // refused target
        for (var i = 0; i < 3; i++)
        {
            detector.Reject("μίτινγκ", "meeting");                          // rejected, then suppressed
        }

        detector.Check("μίτινγκ").Should().Be(LoanwordResult.None);
        detector.Check("καλημέρα").Should().Be(LoanwordResult.None);

        var log = logger.AllOutput;
        log.Should().Contain("Loanword suggested").And.Contain("tier 2").And.Contain("promoted").And.Contain("accepted")
            .And.Contain("rejected").And.Contain("suppressed").And.Contain("user pair");

        string[] words = ["κοντέινερ", "container", "ντεπλόι", "deploy", "ρεπό", "dayoff", "κομπιούτερ", "computer", "σέρβερ", "server", "μίτινγκ", "meeting", "καλημέρα"];
        foreach (var word in words)
        {
            log.Should().NotContainEquivalentOf(word, $"the log must not contain \"{word}\"");
            log.Should().NotContainEquivalentOf(GreekText.StripDiacritics(word), $"the log must not contain \"{word}\" without accents");
        }
    }
}
