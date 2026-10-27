using System.Text;
using GreekSwitch.Core.Data;

namespace GreekSwitch.Core.Text;

public interface IGreekAccents
{
    /// <summary>
    /// <paramref name="word"/> with its tonos and dialytika put back ("κανει" → "κάνει",
    /// "Κανει" → "Κάνει"), or unchanged if it already has any, is ALL CAPS (written without
    /// accents in Greek), or isn't known.
    /// </summary>
    string Restore(string word);
}

/// <summary>No accent data: words stay as typed.</summary>
public sealed class NoGreekAccents : IGreekAccents
{
    public string Restore(string word) => word;
}

/// <summary>
/// The usual accented spelling of each frequent Greek word, by its accent-free key. The
/// dictionaries match Greek ignoring accents, so a word typed in the English layout without
/// the tonos key ("kanei") is still recognised, and would otherwise come out as "κανει".
/// Built from the wordfreq list by tools/FrequencyTableBuilder: when several spellings share a
/// key (αλλά/άλλα, που/πού) the most frequent one wins, and keys whose winner has no accent
/// (που, και) are left out. Binary format v1, see data/frequency/README.md: "GSAC", version 1,
/// entry count (int32 LE), then per entry: length (byte) and the lowercase spelling (UTF-8).
/// </summary>
public sealed class GreekAccentTable : IGreekAccents
{
    public const string Magic = "GSAC";
    public const byte Version = 1;

    private readonly Dictionary<string, string> _spellings;

    public GreekAccentTable(IEnumerable<string> spellings)
    {
        _spellings = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var spelling in spellings)
        {
            _spellings.TryAdd(GreekText.NormalizeForMatch(spelling), spelling);
        }
    }

    public int Count => _spellings.Count;

    public static GreekAccentTable FromEmbeddedResources()
    {
        using var stream = EmbeddedData.Open(EmbeddedData.GreekAccents);
        return new GreekAccentTable(Read(stream));
    }

    public static List<string> Read(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != Magic || reader.ReadByte() != Version)
        {
            throw new FormatException("Not a GreekSwitch accent table (v1).");
        }

        var count = reader.ReadInt32();
        var spellings = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            spellings.Add(Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadByte())));
        }

        return spellings;
    }

    public static void Write(Stream stream, IReadOnlyCollection<string> spellings)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Encoding.ASCII.GetBytes(Magic));
        writer.Write(Version);
        writer.Write(spellings.Count);
        foreach (var spelling in spellings)
        {
            var bytes = Encoding.UTF8.GetBytes(spelling);
            if (bytes.Length > byte.MaxValue)
            {
                throw new ArgumentException("Spelling too long.", nameof(spellings));
            }

            writer.Write((byte)bytes.Length);
            writer.Write(bytes);
        }
    }

    public string Restore(string word)
    {
        if (GreekText.StripDiacritics(word) != word || IsAllCaps(word)
            || !_spellings.TryGetValue(GreekText.NormalizeForMatch(word), out var spelling) || spelling.Length != word.Length)
        {
            return word;
        }

        // Only the accents are taken from the table: the word keeps its own case and σ/ς.
        var chars = word.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            var accented = spelling[i];
            var lower = char.ToLowerInvariant(chars[i]);
            if (accented != lower && GreekText.StripDiacritics(accented.ToString()) == lower.ToString())
            {
                chars[i] = char.IsUpper(chars[i]) ? char.ToUpperInvariant(accented) : accented;
            }
        }

        return new string(chars);
    }

    private static bool IsAllCaps(string word) =>
        word.Count(char.IsLetter) >= 2 && !word.Any(char.IsLower);
}
