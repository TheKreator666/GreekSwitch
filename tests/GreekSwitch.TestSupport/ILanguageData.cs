using GreekSwitch.Core.Scoring;
using GreekSwitch.Core.Text;

namespace GreekSwitch.TestSupport;

/// <summary>The real language data (dictionaries, trigram models, accents), loaded once by a test fixture.</summary>
public interface ILanguageData
{
    IDictionaryService Dictionaries { get; }

    ITrigramModel Trigrams { get; }

    IWordFrequency Frequencies { get; }

    IGreekAccents Accents { get; }
}
