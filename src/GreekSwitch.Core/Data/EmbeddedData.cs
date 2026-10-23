namespace GreekSwitch.Core.Data;

/// <summary>
/// The Hunspell dictionaries and trigram models compiled into this assembly as resources, so
/// the published app is one file. Resource names are "GreekSwitch.{folder}.{file}".
/// </summary>
public static class EmbeddedData
{
    public const string GreekDictionary = "GreekSwitch.dictionaries.el_GR.dic";
    public const string GreekAffixes = "GreekSwitch.dictionaries.el_GR.aff";
    public const string EnglishDictionary = "GreekSwitch.dictionaries.en_US.dic";
    public const string EnglishAffixes = "GreekSwitch.dictionaries.en_US.aff";
    public const string GreekTrigrams = "GreekSwitch.ngrams.el.tri";
    public const string EnglishTrigrams = "GreekSwitch.ngrams.en.tri";

    public static Stream Open(string resourceName) =>
        typeof(EmbeddedData).Assembly.GetManifestResourceStream(resourceName)
        ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' is missing from the build.");
}
