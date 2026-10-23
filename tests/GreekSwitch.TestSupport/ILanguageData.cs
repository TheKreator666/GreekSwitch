using GreekSwitch.Core.Scoring;

namespace GreekSwitch.TestSupport;

/// <summary>The real language data (dictionaries, trigram models), loaded once by a test fixture.</summary>
public interface ILanguageData
{
    IDictionaryService Dictionaries { get; }

    ITrigramModel Trigrams { get; }
}
