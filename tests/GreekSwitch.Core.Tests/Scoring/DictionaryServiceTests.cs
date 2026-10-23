using GreekSwitch.Core.Scoring;
using GreekSwitch.Core.Tests.Support;

namespace GreekSwitch.Core.Tests.Scoring;

[Collection(LanguageDataCollection.Name)]
public class DictionaryServiceTests(LanguageDataFixture data)
{
    private readonly IDictionaryService _dictionaries = data.Dictionaries;

    [Theory]
    [InlineData("καλημέρα")]
    [InlineData("καλημερα")]      // tonos missing
    [InlineData("ΚΑΛΗΜΕΡΑ")]      // all caps, conventionally unaccented
    [InlineData("Καλημέρα")]
    [InlineData("καλημέρά")]      // tonos on the wrong vowel still normalises to the same word
    [InlineData("πράξεισ")]       // final sigma typed with S
    [InlineData("ςας")]           // initial sigma typed with W
    [InlineData("ελλάδα")]        // proper noun typed in lowercase
    [InlineData("θέλω")]
    [InlineData("να")]
    [InlineData("θα")]
    [InlineData("που")]
    [InlineData("πού")]
    [InlineData("προϊόν")]
    [InlineData("προιον")]        // dialytika missing as well as tonos
    public void Greek_matching_is_accent_case_and_sigma_insensitive(string word)
    {
        _dictionaries.IsWord(word, Lang.Greek).Should().BeTrue();
    }

    [Theory]
    [InlineData("αππλιψατιον")]
    [InlineData("ηελλο")]
    [InlineData("ςορλδ")]
    [InlineData("hello")]         // right word, wrong language
    public void Greek_rejects_non_words(string word)
    {
        _dictionaries.IsWord(word, Lang.Greek).Should().BeFalse();
    }

    [Theory]
    [InlineData("application")]
    [InlineData("hello")]
    [InlineData("Hello")]
    [InlineData("HELLO")]
    [InlineData("the")]
    [InlineData("don't")]
    [InlineData("london")]        // proper noun typed in lowercase
    public void English_accepts_words_in_any_case(string word)
    {
        _dictionaries.IsWord(word, Lang.English).Should().BeTrue();
    }

    [Theory]
    [InlineData("poy")]
    [InlineData("ua")]
    [InlineData("kalhm;era")]
    [InlineData("καλημέρα")]
    [InlineData("")]
    public void English_rejects_non_words(string word)
    {
        _dictionaries.IsWord(word, Lang.English).Should().BeFalse();
    }
}
