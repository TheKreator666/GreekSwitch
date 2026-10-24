using GreekSwitch.App.Policy;
using GreekSwitch.App.Settings;
using GreekSwitch.Core.Loanwords;
using Microsoft.Extensions.Logging.Abstractions;

namespace GreekSwitch.App.Tests;

/// <summary>Milestone 15 settings: loanword pairs, learned pairs (hashed) and suggest-only apps.</summary>
public sealed class LoanwordSettingsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "GreekSwitchTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void Pairs_learned_pairs_and_suggest_only_apps_round_trip()
    {
        var store = new SettingsStore(_dir, NullLogger.Instance);
        var settings = store.Load();
        settings.LoanwordPairs = [new LoanwordPair("κοντέινερ", "container")];
        settings.LoanwordLearned = [new LearnedLoanword("aGFzaA==", "deploy", 2, 0)];
        settings.SuggestOnlyProcesses = ["Teams.exe"];
        settings.AutoReplaceLoanwords = true;
        store.Save(settings);

        var loaded = new SettingsStore(_dir, NullLogger.Instance).Load();

        loaded.LoanwordPairs.Should().Equal(new LoanwordPair("κοντέινερ", "container"));
        loaded.LoanwordLearned.Should().Equal(new LearnedLoanword("aGFzaA==", "deploy", 2, 0));
        loaded.SuggestOnlyProcesses.Should().Equal("Teams.exe");
        loaded.AutoReplaceLoanwords.Should().BeTrue();
    }

    [Fact]
    public void Learned_pairs_store_no_greek_text()
    {
        var store = new SettingsStore(_dir, NullLogger.Instance);
        var settings = store.Load();
        var pairs = new LoanwordPairStore(hasher: Core.Decisions.HmacWordHasher.Ephemeral());
        pairs.RecordAccept("κοντέινερ", "container");
        settings.LoanwordLearned = [.. pairs.LearnedSnapshot()];
        store.Save(settings);

        File.ReadAllText(store.FilePath).Should().NotContain("κοντέινερ").And.Contain("container");
    }

    [Fact]
    public void Invalid_entries_are_dropped_on_load()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(
            Path.Combine(_dir, "settings.json"),
            "{ \"settingsVersion\": 3, \"loanwordLearned\": [ { \"hash\": \"x\", \"target\": \"not a word!\" } ], \"suggestOnlyProcesses\": [ \" \", \"Slack.exe \" ] }");

        var loaded = new SettingsStore(_dir, NullLogger.Instance).Load();

        loaded.LoanwordLearned.Should().BeEmpty();
        loaded.SuggestOnlyProcesses.Should().Equal("Slack.exe");
    }

    [Fact]
    public void Suggest_only_apps_are_matched_case_insensitively()
    {
        var policy = new AppPolicy();
        policy.Apply(new AppSettings { SuggestOnlyProcesses = ["Teams.exe"] }, ManagedPolicy.None);

        policy.IsSuggestOnly("teams.EXE").Should().BeTrue();
        policy.IsSuggestOnly("notepad.exe").Should().BeFalse();
    }
}
