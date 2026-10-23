using System.Text;
using GreekSwitch.Core.Data;
using GreekSwitch.Core.Text;

namespace GreekSwitch.Core.Scoring;

/// <summary>
/// Dictionary lookups against build-time word lists: every form of el_GR and en_US, expanded
/// with the affix rules by tools/NgramBuilder, normalised (Greek: lowercase, no accents, σ;
/// English: lowercase) and stored as sorted 64-bit FNV-1a hashes. About 8 bytes per form instead
/// of Hunspell's in-memory structures (≈ 6 MB vs ≈ 110 MB for el_GR), one binary search per
/// lookup, and nothing to parse at start-up.
/// <para>
/// Two lists per language: forms whose dictionary entry is lowercase, and forms of entries that
/// are capitalised (names, acronyms). Lowercase input only matches the first ("na" is not a word
/// because "Na" is); capitalised or all-caps input matches either.
/// </para>
/// Binary format v1 (data/lexicon/README.md): "GSLX", version 1, lowercase count and
/// capitalised count (int32 LE), then both sorted uint64 arrays.
/// </summary>
public sealed class LexiconDictionaryService : IDictionaryService
{
    public const string Magic = "GSLX";
    public const byte Version = 1;

    private readonly Lexicon _greek;
    private readonly Lexicon _english;

    public LexiconDictionaryService(Lexicon greek, Lexicon english)
    {
        _greek = greek;
        _english = english;
    }

    public static LexiconDictionaryService FromEmbeddedResources()
    {
        using var greek = EmbeddedData.Open(EmbeddedData.GreekLexicon);
        using var english = EmbeddedData.Open(EmbeddedData.EnglishLexicon);
        return new LexiconDictionaryService(Lexicon.Read(greek), Lexicon.Read(english));
    }

    /// <summary>The normalised form a word is stored and looked up as.</summary>
    public static string Key(string word, Lang lang) =>
        lang == Lang.Greek ? GreekText.NormalizeForMatch(word) : word.ToLowerInvariant();

    /// <summary>64-bit FNV-1a over the UTF-16 code units of a normalised key.</summary>
    public static ulong Hash(string key)
    {
        var hash = 14695981039346656037UL;
        foreach (var c in key)
        {
            hash = (hash ^ c) * 1099511628211UL;
        }

        return hash;
    }

    public bool IsWord(string word, Lang lang)
    {
        if (word.Length == 0)
        {
            return false;
        }

        if (lang == Lang.Greek)
        {
            if (!word.Any(GreekText.IsGreekLetter) || word.Any(c => c is >= 'A' and <= 'z') || GreekText.HasMisplacedFinalSigma(word))
            {
                return false;
            }
        }
        else if (word.Any(GreekText.IsGreekLetter))
        {
            return false;
        }

        var lexicon = lang == Lang.Greek ? _greek : _english;
        var hash = Hash(Key(word, lang));
        return lexicon.ContainsLowercase(hash) || (word.Any(char.IsUpper) && lexicon.ContainsCapitalised(hash));
    }
}

/// <summary>One language's word forms as sorted hashes: lowercase entries and capitalised entries.</summary>
public sealed class Lexicon
{
    private readonly ulong[] _lowercase;
    private readonly ulong[] _capitalised;

    public Lexicon(ulong[] lowercase, ulong[] capitalised)
    {
        _lowercase = lowercase;
        _capitalised = capitalised;
    }

    public int Count => _lowercase.Length + _capitalised.Length;

    public bool ContainsLowercase(ulong hash) => Array.BinarySearch(_lowercase, hash) >= 0;

    public bool ContainsCapitalised(ulong hash) => Array.BinarySearch(_capitalised, hash) >= 0;

    public static Lexicon Read(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != LexiconDictionaryService.Magic || reader.ReadByte() != LexiconDictionaryService.Version)
        {
            throw new FormatException("Not a GreekSwitch lexicon (v1).");
        }

        var lowercase = new ulong[reader.ReadInt32()];
        var capitalised = new ulong[reader.ReadInt32()];
        for (var i = 0; i < lowercase.Length; i++)
        {
            lowercase[i] = reader.ReadUInt64();
        }

        for (var i = 0; i < capitalised.Length; i++)
        {
            capitalised[i] = reader.ReadUInt64();
        }

        return new Lexicon(lowercase, capitalised);
    }

    /// <summary>Writes the v1 format (hashes are sorted and de-duplicated here).</summary>
    public static void Write(Stream stream, IEnumerable<ulong> lowercase, IEnumerable<ulong> capitalised)
    {
        var lower = lowercase.Distinct().Order().ToArray();
        var capital = capitalised.Distinct().Order().ToArray();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Encoding.ASCII.GetBytes(LexiconDictionaryService.Magic));
        writer.Write(LexiconDictionaryService.Version);
        writer.Write(lower.Length);
        writer.Write(capital.Length);
        foreach (var h in lower)
        {
            writer.Write(h);
        }

        foreach (var h in capital)
        {
            writer.Write(h);
        }
    }
}
