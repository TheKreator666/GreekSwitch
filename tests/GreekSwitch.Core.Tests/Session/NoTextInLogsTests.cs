using GreekSwitch.Core.Tests.Support;

namespace GreekSwitch.Core.Tests.Session;

[Collection(LanguageDataCollection.Name)]
public class NoTextInLogsTests(LanguageDataFixture data)
{
    [Fact]
    public void Pipeline_logs_events_but_never_the_words()
    {
        const string typed = "Θέλω να φτιάξω ένα αππλιψατιον poy ua κάνει πράξεις ηελλο ςορλδ user@mail.com myVariable";
        string[] produced = ["application", "που", "θα", "hello", "world"];

        var sim = new TypingSimulator(data).Type(typed + " ");
        sim.TypeIn(Lang.Greek, "τηινγ ").Backspace(); // a replacement, then an undo
        sim.Replacements.Should().BeGreaterThan(3, "the run must exercise the replacement and undo log paths");

        var log = sim.Logger.AllOutput;
        log.Should().Contain("Replacement made").And.Contain("reverted");

        var words = typed.Split(' ', '@', '.').Concat(produced).Append("τηινγ").Append("thing")
            .Where(w => w.Length >= 2);
        foreach (var word in words)
        {
            log.Should().NotContainEquivalentOf(word, $"the log must not contain the typed word \"{word}\"");
        }
    }
}
