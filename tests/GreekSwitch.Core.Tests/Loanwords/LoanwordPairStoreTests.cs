using System.Text;
using System.Text.Json;
using GreekSwitch.Core.Decisions;
using GreekSwitch.Core.Loanwords;

namespace GreekSwitch.Core.Tests.Loanwords;

public class LoanwordPairStoreTests
{
    private static readonly byte[] Key = Encoding.ASCII.GetBytes("test key, 32 bytes long........!");

    private static LoanwordPairStore Create(IEnumerable<LoanwordPair>? pairs = null, IEnumerable<LearnedLoanword>? learned = null, byte[]? key = null) =>
        new(pairs, learned, new HmacWordHasher(key ?? Key));

    [Fact]
    public void Settings_pairs_match_ignoring_case_and_accents_and_are_kept_as_text()
    {
        var store = Create([new LoanwordPair("Ρεπό", "day off")]);

        store.Find("ΡΕΠΟ").Should().Be(new LoanwordPairInfo("day off", Explicit: true, 0, 0));
        store.Snapshot().Should().Equal(new LoanwordPair("Ρεπό", "day off"));
    }

    [Fact]
    public void Learned_pairs_keep_the_Greek_word_only_as_a_hash()
    {
        var store = Create();

        store.RecordAccept("κοντέινερ", "Container").Should().Be(1);
        store.RecordAccept("Κοντεϊνερ", "container").Should().Be(2, "matched ignoring case and accents");

        store.Find("κοντέινερ").Should().Be(new LoanwordPairInfo("container", Explicit: false, 2, 0));
        store.Snapshot().Should().BeEmpty();
        var saved = JsonSerializer.Serialize(store.LearnedSnapshot());
        saved.Should().Contain("container").And.NotContain("κοντ").And.NotContainEquivalentOf("\\u03BA");
    }

    [Fact]
    public void Learned_pairs_survive_a_reload_with_the_same_key_only()
    {
        var first = Create();
        first.RecordAccept("κοντέινερ", "container");

        Create(learned: first.LearnedSnapshot()).Find("κοντέινερ").Should().NotBeNull();
        Create(learned: first.LearnedSnapshot(), key: Encoding.ASCII.GetBytes("another key, also 32 bytes long!"))
            .Find("κοντέινερ").Should().BeNull();
    }

    [Fact]
    public void Hashes_differ_from_the_never_convert_lists_for_the_same_word()
    {
        var never = new NeverConvertList(hasher: new HmacWordHasher(Key));
        never.Learn("κοντέινερ");
        var store = Create();
        store.RecordAccept("κοντέινερ", "container");

        store.LearnedSnapshot().Single().Hash.Should().NotBe(never.LearnedSnapshot().Single());
    }

    [Theory]
    [InlineData("κοντέινερ")]                                  // Greek: would store what the user typed
    [InlineData("")]
    [InlineData("user@example.com")]
    [InlineData("averyveryveryveryveryverylongwordthatisntreal")]
    public void Refuses_learned_targets_that_are_not_short_Latin_words(string target)
    {
        var store = Create();

        store.RecordAccept("κοντέινερ", target).Should().Be(0);
        store.RecordReject("κοντέινερ", target).Should().Be(0);
        store.LearnedCount.Should().Be(0);
    }

    [Fact]
    public void Accepting_another_target_starts_over_and_rejecting_resets_the_accepts()
    {
        var store = Create();
        store.RecordAccept("σάιτ", "site");
        store.RecordAccept("σάιτ", "site");

        store.RecordAccept("σάιτ", "sight").Should().Be(1);
        store.RecordReject("σάιτ", "sight").Should().Be(1);

        store.Find("σάιτ").Should().Be(new LoanwordPairInfo("sight", Explicit: false, 0, 1));
    }

    [Fact]
    public void Raises_changed_for_user_actions_but_not_for_bulk_replacement()
    {
        var store = Create();
        var changes = 0;
        store.Changed += (_, _) => changes++;

        store.Add("ρεπό", "day off");
        store.Add("ρεπό", "day off"); // no change
        store.RecordAccept("κοντέινερ", "container");
        store.RecordReject("κοντέινερ", "container");
        store.Remove("ρεπό").Should().BeTrue();
        changes.Should().Be(4);

        store.ReplaceAll([new LoanwordPair("ρεπό", "day off")]);
        store.ForgetLearned();
        changes.Should().Be(4);
        store.Snapshot().Should().HaveCount(1);
        store.LearnedCount.Should().Be(0);
    }

    [Fact]
    public void Is_safe_to_use_from_several_threads()
    {
        var store = Create();

        Parallel.For(0, 400, i =>
        {
            store.RecordAccept("λέξη" + "αβγδεζηθικλμνξοπρστυ"[i % 20], "word");
            store.Find("λέξηα");
            _ = store.LearnedSnapshot();
        });

        store.LearnedCount.Should().Be(20);
        store.LearnedSnapshot().Sum(l => l.Accepts).Should().Be(400);
    }
}
