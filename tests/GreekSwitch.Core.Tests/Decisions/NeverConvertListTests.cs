using System.Text;
using GreekSwitch.Core.Decisions;

namespace GreekSwitch.Core.Tests.Decisions;

public class NeverConvertListTests
{
    private static readonly byte[] Key = Encoding.ASCII.GetBytes("test key, 32 bytes long........!");

    [Fact]
    public void Words_added_by_the_user_match_ignoring_case_and_accents()
    {
        var list = new NeverConvertList(["Καλημέρα"], hasher: new HmacWordHasher(Key));

        list.Contains("καλημερα").Should().BeTrue();
        list.Snapshot().Should().Equal("Καλημέρα");
    }

    [Fact]
    public void Learned_words_match_but_are_never_stored_as_text()
    {
        var list = new NeverConvertList(hasher: new HmacWordHasher(Key));

        list.Learn("kalimera");

        list.Contains("KALIMERA").Should().BeTrue();
        list.Snapshot().Should().BeEmpty();
        list.LearnedCount.Should().Be(1);
        list.LearnedSnapshot().Should().ContainSingle()
            .Which.Should().NotContainEquivalentOf("kalimera");
    }

    [Fact]
    public void Learned_hashes_survive_a_reload_with_the_same_key()
    {
        var first = new NeverConvertList(hasher: new HmacWordHasher(Key));
        first.Learn("kalimera");

        var reloaded = new NeverConvertList(learned: first.LearnedSnapshot(), hasher: new HmacWordHasher(Key));

        reloaded.Contains("kalimera").Should().BeTrue();
        reloaded.Contains("kalinixta").Should().BeFalse();
    }

    [Fact]
    public void A_different_key_cannot_match_the_learned_words()
    {
        var first = new NeverConvertList(hasher: new HmacWordHasher(Key));
        first.Learn("kalimera");

        var otherKey = Encoding.ASCII.GetBytes("another key, also 32 bytes long!");
        new NeverConvertList(learned: first.LearnedSnapshot(), hasher: new HmacWordHasher(otherKey))
            .Contains("kalimera").Should().BeFalse();
    }

    [Fact]
    public void Learning_raises_changed_and_forgetting_clears_only_learned_words()
    {
        var list = new NeverConvertList(["typed"], hasher: new HmacWordHasher(Key));
        var changes = 0;
        list.Changed += (_, _) => changes++;

        list.Learn("kalimera");
        list.Learn("KALIMERA"); // same word: no second entry
        list.ForgetLearned();

        changes.Should().Be(1);
        list.LearnedCount.Should().Be(0);
        list.Contains("typed").Should().BeTrue();
    }

    [Fact]
    public void With_learning_disabled_nothing_new_is_learned_but_existing_hashes_still_match()
    {
        var first = new NeverConvertList(hasher: new HmacWordHasher(Key));
        first.Learn("kalimera");
        var list = new NeverConvertList(learned: first.LearnedSnapshot(), hasher: new HmacWordHasher(Key)) { LearningEnabled = false };
        var changes = 0;
        list.Changed += (_, _) => changes++;

        list.Learn("kalinixta");

        list.Contains("kalinixta").Should().BeFalse();
        list.Contains("kalimera").Should().BeTrue();
        list.LearnedCount.Should().Be(1);
        changes.Should().Be(0, "nothing changed, so nothing is saved");

        list.Forget("kalimera");
        list.Contains("kalimera").Should().BeFalse("forgetting still works");
    }

    [Fact]
    public void Learning_disabled_still_allows_words_typed_into_settings()
    {
        var list = new NeverConvertList(hasher: new HmacWordHasher(Key)) { LearningEnabled = false };

        list.Add("typed");

        list.Contains("typed").Should().BeTrue();
    }
}
