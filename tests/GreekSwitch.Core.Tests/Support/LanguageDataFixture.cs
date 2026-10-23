using GreekSwitch.Core.Scoring;

namespace GreekSwitch.Core.Tests.Support;

/// <summary>Loads the real embedded dictionaries and trigram models once for all tests that need them.</summary>
public sealed class LanguageDataFixture
{
    public LanguageDataFixture()
    {
        var dictionaries = HunspellDictionaryService.FromEmbeddedResources();
        dictionaries.Preload();
        Dictionaries = dictionaries;
    }

    public IDictionaryService Dictionaries { get; }
}

[CollectionDefinition(Name)]
public sealed class LanguageDataCollection : ICollectionFixture<LanguageDataFixture>
{
    public const string Name = "Language data";
}
