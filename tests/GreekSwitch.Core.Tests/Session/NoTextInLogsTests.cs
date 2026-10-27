using GreekSwitch.Core.Tests.Support;

namespace GreekSwitch.Core.Tests.Session;

[Collection(LanguageDataGroup.Name)]
public class NoTextInLogsTests(LanguageDataFixture data)
{
    [Fact]
    public void Pipeline_logs_events_but_never_the_words()
    {
        const string typed = "Θέλω να φτιάξω ένα αππλιψατιον poy ua κάνει πράξεις ηελλο ςορλδ user@mail.com myVariable";
        string[] produced = ["application", "που", "θα", "hello", "world"];

        var sim = new TypingSimulator(data).Type(typed + " ");
        sim.TypeIn(Lang.Greek, "τηινγ ").Backspace(); // a replacement, then an undo
        sim.TypeIn(Lang.English, " den ");             // kept, then converted by hotkey, then undone
        sim.Session.ConvertLastWord();
        sim.Backspace();
        sim.Replacements.Should().BeGreaterThan(3, "the run must exercise the replacement and undo log paths");

        var log = sim.Logger.AllOutput;
        log.Should().Contain("Replacement made").And.Contain("reverted").And.Contain("converted by hotkey");

        var words = typed.Split(' ', '@', '.').Concat(produced).Append("τηινγ").Append("thing").Append("den").Append("δεν")
            .Where(w => w.Length >= 2);
        foreach (var word in words)
        {
            log.Should().NotContainEquivalentOf(word, $"the log must not contain the typed word \"{word}\"");
        }
    }

    [Fact]
    public void Accent_path_logs_events_but_never_the_words()
    {
        var sim = new TypingSimulator(data, session: new() { RestoreAccents = true });
        sim.TypeIn(Lang.Greek, "ειναι πολυ ").Pause();   // accents added
        sim.RefuseReplacements = true;
        sim.TypeIn(Lang.Greek, "ωραιο ").Pause();        // a guard refused them
        sim.RefuseReplacements = false;
        sim.TypeIn(Lang.Greek, "φτιαξω ενα αππλιψατιον ").Pause(); // English in between: fixed in place

        var log = sim.Logger.AllOutput;
        log.Should().Contain("Accents added").And.Contain("Accents not added");
        foreach (var word in new[] { "ειναι", "είναι", "πολυ", "πολύ", "ωραιο", "ωραίο", "φτιαξω", "φτιάξω", "ενα", "ένα", "αππλιψατιον", "application" })
        {
            log.Should().NotContainEquivalentOf(word, $"the log must not contain the typed word \"{word}\"");
        }
    }
}
