using GreekSwitch.Core.Layout;
using GreekSwitch.Core.Text;

namespace GreekSwitch.Core.Tests.Text;

public class GreekAccentTableTests
{
    private readonly GreekAccentTable _table = new(["κάνει", "πράξεις", "αλλά", "προϊόν"]);

    [Theory]
    [InlineData("κανει", "κάνει")]
    [InlineData("Κανει", "Κάνει")]
    [InlineData("πραξεις", "πράξεις")]
    [InlineData("προιον", "προϊόν")]
    public void Puts_the_accents_back(string word, string expected) =>
        _table.Restore(word).Should().Be(expected);

    [Theory]
    [InlineData("ΚΑΝΕΙ")]     // all caps are written without accents
    [InlineData("κάνει")]     // already accented
    [InlineData("κανεί")]     // accented differently: the user chose
    [InlineData("σπιτι")]     // not in the table
    [InlineData("καν")]       // a different key
    public void Leaves_other_words_alone(string word) =>
        _table.Restore(word).Should().Be(word);

    [Theory]
    [InlineData("κανει;", "κάνει;")]     // the Greek question mark is typed with Q, inside the word
    [InlineData("«κανει»", "«κάνει»")]
    [InlineData(";", ";")]
    public void Leaves_surrounding_marks_as_they_are(string word, string expected) =>
        _table.Restore(word).Should().Be(expected);

    [Fact]
    public void Keeps_the_words_own_sigma() =>
        _table.Restore("πραξεισ").Should().Be("πράξεισ");

    [Fact]
    public void Round_trips_through_the_binary_format()
    {
        using var stream = new MemoryStream();
        GreekAccentTable.Write(stream, ["κάνει", "αλλά"]);
        stream.Position = 0;

        GreekAccentTable.Read(stream).Should().Equal("κάνει", "αλλά");
    }

    [Theory]
    [InlineData("κανει", "κάνει")]
    [InlineData("καλημερα", "καλημέρα")]
    [InlineData("αλλα", "αλλά")]
    [InlineData("ειναι", "είναι")]
    [InlineData("που", "που")]   // πού is far less frequent than που
    [InlineData("και", "και")]
    public void Embedded_table_uses_the_most_frequent_spelling(string word, string expected) =>
        GreekAccentTable.FromEmbeddedResources().Restore(word).Should().Be(expected);

    [Fact]
    public void Convert_selection_puts_the_accents_back() =>
        new TextConverter(StaticLayouts.Greek, StaticLayouts.English, _table).Convert("kanei prajeis")
            .Should().Be("κάνει πράξεις");
}
