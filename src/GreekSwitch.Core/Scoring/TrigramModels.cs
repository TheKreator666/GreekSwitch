using GreekSwitch.Core.Data;

namespace GreekSwitch.Core.Scoring;

/// <summary>The Greek and English trigram models behind one <see cref="ITrigramModel"/>.</summary>
public sealed class TrigramModels : ITrigramModel
{
    private readonly TrigramModel _greek;
    private readonly TrigramModel _english;

    public TrigramModels(TrigramModel greek, TrigramModel english)
    {
        _greek = greek;
        _english = english;
    }

    public static TrigramModels FromEmbeddedResources()
    {
        using var greek = EmbeddedData.Open(EmbeddedData.GreekTrigrams);
        using var english = EmbeddedData.Open(EmbeddedData.EnglishTrigrams);
        return new TrigramModels(TrigramModel.Load(greek, Lang.Greek), TrigramModel.Load(english, Lang.English));
    }

    public double LogProb(string word, Lang lang) =>
        (lang == Lang.Greek ? _greek : _english).LogProb(word);
}
