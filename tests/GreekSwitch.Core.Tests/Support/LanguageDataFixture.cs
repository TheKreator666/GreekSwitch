using GreekSwitch.Core.Loanwords;
using GreekSwitch.Core.Scoring;
using GreekSwitch.Core.Text;
using GreekSwitch.TestSupport;

namespace GreekSwitch.Core.Tests.Support;

/// <summary>Loads the real embedded dictionaries, trigram models, frequencies and phonetic index once for all tests that need them.</summary>
public sealed class LanguageDataFixture : ILanguageData
{
    public LanguageDataFixture()
    {
        Dictionaries = LexiconDictionaryService.FromEmbeddedResources();
        Trigrams = TrigramModels.FromEmbeddedResources();
        Frequencies = WordFrequencyTable.FromEmbeddedResources();
        Accents = GreekAccentTable.FromEmbeddedResources();
        Phonetic = PhoneticIndex.FromEmbeddedResources();
    }

    public IDictionaryService Dictionaries { get; }

    public ITrigramModel Trigrams { get; }

    public IWordFrequency Frequencies { get; }

    public IGreekAccents Accents { get; }

    public PhoneticIndex Phonetic { get; }
}

[CollectionDefinition(Name)]
public sealed class LanguageDataGroup : ICollectionFixture<LanguageDataFixture>
{
    public const string Name = "Language data";
}
