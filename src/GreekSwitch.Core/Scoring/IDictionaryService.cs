namespace GreekSwitch.Core.Scoring;

public interface IDictionaryService
{
    /// <summary>
    /// True if <paramref name="word"/> is a valid word in <paramref name="lang"/>. Greek matching
    /// ignores tonos, dialytika, final-sigma form and case.
    /// </summary>
    bool IsWord(string word, Lang lang);
}
