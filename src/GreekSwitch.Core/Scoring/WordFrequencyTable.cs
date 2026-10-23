using System.Text;
using GreekSwitch.Core.Data;
using GreekSwitch.Core.Text;

namespace GreekSwitch.Core.Scoring;

public interface IWordFrequency
{
    /// <summary>
    /// Zipf frequency of <paramref name="word"/> (log10 of occurrences per billion words; about
    /// 7 for "και"/"the", 3 for rare words), or 0 if the word isn't in the table. Greek is
    /// matched ignoring accents, case and σ/ς form; English ignoring case.
    /// </summary>
    double Zipf(string word, Lang lang);
}

/// <summary>No frequency data: every word is 0 (frequency rules then never fire).</summary>
public sealed class NoWordFrequency : IWordFrequency
{
    public double Zipf(string word, Lang lang) => 0;
}

/// <summary>
/// The embedded frequency tables (built from wordfreq by tools/FrequencyTableBuilder).
/// Binary format v1, see data/frequency/README.md: "GSZF", version 1, entry count (int32 LE),
/// then per entry: quantised Zipf (byte, Zipf × 25), key length (byte), UTF-8 key.
/// </summary>
public sealed class WordFrequencyTable : IWordFrequency
{
    public const string Magic = "GSZF";
    public const byte Version = 1;
    public const double Quantum = 25.0;

    private readonly Dictionary<string, byte> _greek;
    private readonly Dictionary<string, byte> _english;

    public WordFrequencyTable(Dictionary<string, byte> greek, Dictionary<string, byte> english)
    {
        _greek = greek;
        _english = english;
    }

    public static WordFrequencyTable FromEmbeddedResources()
    {
        using var greek = EmbeddedData.Open(EmbeddedData.GreekFrequencies);
        using var english = EmbeddedData.Open(EmbeddedData.EnglishFrequencies);
        return new WordFrequencyTable(Read(greek), Read(english));
    }

    /// <summary>The lookup key a word is stored under.</summary>
    public static string Key(string word, Lang lang) =>
        lang == Lang.Greek ? GreekText.NormalizeForMatch(word) : word.ToLowerInvariant();

    public static Dictionary<string, byte> Read(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != Magic || reader.ReadByte() != Version)
        {
            throw new FormatException("Not a GreekSwitch frequency table (v1).");
        }

        var count = reader.ReadInt32();
        var table = new Dictionary<string, byte>(count, StringComparer.Ordinal);
        for (var i = 0; i < count; i++)
        {
            var zipf = reader.ReadByte();
            var key = Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadByte()));
            table[key] = zipf;
        }

        return table;
    }

    public static void Write(Stream stream, IEnumerable<(string Key, double Zipf)> entries)
    {
        var list = entries.ToList();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Encoding.ASCII.GetBytes(Magic));
        writer.Write(Version);
        writer.Write(list.Count);
        foreach (var (key, zipf) in list)
        {
            var bytes = Encoding.UTF8.GetBytes(key);
            if (bytes.Length > byte.MaxValue)
            {
                throw new ArgumentException("Key too long.", nameof(entries));
            }

            writer.Write((byte)Math.Clamp(Math.Round(zipf * Quantum), 0, 255));
            writer.Write((byte)bytes.Length);
            writer.Write(bytes);
        }
    }

    public double Zipf(string word, Lang lang)
    {
        var table = lang == Lang.Greek ? _greek : _english;
        return table.TryGetValue(Key(word, lang), out var q) ? q / Quantum : 0;
    }
}
