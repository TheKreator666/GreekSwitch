using GreekSwitch.Core.Scoring;
using GreekSwitch.Core.Tests.Support;

namespace GreekSwitch.Core.Tests.Scoring;

[Collection(LanguageDataGroup.Name)]
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
    [InlineData("Ελλάδα")]        // proper noun, capitalised
    [InlineData("ΕΛΛΑΔΑ")]        // proper noun, all caps
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
    [InlineData("ςασ")]           // "was" typed in the GR layout: ς mid-word can't be Greek, even though σας is
    [InlineData("ςαντ")]          // "want"
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
    [InlineData("London")]        // proper noun, capitalised
    [InlineData("LONDON")]
    public void English_accepts_words_in_any_case(string word)
    {
        _dictionaries.IsWord(word, Lang.English).Should().BeTrue();
    }

    /// <summary>
    /// A lowercase reading must not match an entry that only exists capitalised: otherwise "na"
    /// (Na, sodium) or "se" (Se) count as English and block "να", "σε".
    /// </summary>
    [Theory]
    [InlineData("na", Lang.English)]
    [InlineData("se", Lang.English)]
    [InlineData("london", Lang.English)]
    [InlineData("ελλάδα", Lang.Greek)]
    public void Lowercase_input_does_not_match_capitalised_only_entries(string word, Lang lang)
    {
        _dictionaries.IsWord(word, lang).Should().BeFalse();
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
