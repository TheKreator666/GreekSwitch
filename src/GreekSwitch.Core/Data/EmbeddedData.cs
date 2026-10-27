namespace GreekSwitch.Core.Data;

/// <summary>
/// The word lists (hashed lexicons), trigram models, frequency tables and phonetic index compiled into this assembly as resources, so
/// the published app is one file. Resource names are "GreekSwitch.{folder}.{file}".
/// </summary>
public static class EmbeddedData
{
    public const string GreekTrigrams = "GreekSwitch.ngrams.el.tri";
    public const string EnglishTrigrams = "GreekSwitch.ngrams.en.tri";
    public const string GreekLexicon = "GreekSwitch.lexicon.el.lex";
    public const string EnglishLexicon = "GreekSwitch.lexicon.en.lex";
    public const string GreekFrequencies = "GreekSwitch.frequency.el.zipf";
    public const string EnglishFrequencies = "GreekSwitch.frequency.en.zipf";
    public const string GreekAccents = "GreekSwitch.frequency.el.accents";
    public const string EnglishPhonetic = "GreekSwitch.phonetic.en.idx";

    public static Stream Open(string resourceName) =>
        typeof(EmbeddedData).Assembly.GetManifestResourceStream(resourceName)
        ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' is missing from the build.");
}
