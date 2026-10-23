using GreekSwitch.Core.Scoring;
using GreekSwitch.TestSupport;

namespace GreekSwitch.Core.Tests.Support;

/// <summary>Loads the real embedded dictionaries and trigram models once for all tests that need them.</summary>
public sealed class LanguageDataFixture : ILanguageData
{
    public LanguageDataFixture()
    {
        var dictionaries = HunspellDictionaryService.FromEmbeddedResources();
        dictionaries.Preload();
        Dictionaries = dictionaries;
        Trigrams = TrigramModels.FromEmbeddedResources();
    }

    public IDictionaryService Dictionaries { get; }

    public ITrigramModel Trigrams { get; }
}

[CollectionDefinition(Name)]
public sealed class LanguageDataGroup : ICollectionFixture<LanguageDataFixture>
{
    public const string Name = "Language data";
}
