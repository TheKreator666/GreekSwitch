namespace GreekSwitch.Core.Scoring;

public interface ITrigramModel
{
    /// <summary>
    /// Average natural-log probability per predicted character of <paramref name="word"/> under
    /// the character trigram model for <paramref name="lang"/>. Closer to zero is more plausible;
    /// typical words score about -2 to -3, keyboard mash -5 or lower.
    /// </summary>
    double LogProb(string word, Lang lang);
}
