using GreekSwitch.Core.Decisions;
using GreekSwitch.Core.Layout;
using GreekSwitch.Core.Scoring;

namespace GreekSwitch.TestSupport;

/// <summary>Builds a DecisionEngine from the real language data, the way the app does.</summary>
public static class EngineFactory
{
    public static DecisionEngine Create(ILanguageData data, INeverConvertList? neverConvert = null, ScoringOptions? options = null) =>
        new(
            new DualDecoder(StaticLayouts.Greek, StaticLayouts.English),
            new Scorer(data.Dictionaries, data.Trigrams, data.Frequencies),
            neverConvert ?? new NeverConvertList(),
            options);
}
