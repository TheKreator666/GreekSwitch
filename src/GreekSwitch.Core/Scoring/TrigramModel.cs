using System.Globalization;
using GreekSwitch.Core.Text;

namespace GreekSwitch.Core.Scoring;

/// <summary>
/// Character trigram model for one language with add-k smoothing:
/// P(c | ab) = (count(abc) + k) / (count(ab·) + k·V), where V is the symbol vocabulary size.
/// Words are padded as "^^word$" (see <see cref="TrigramText"/>), and the score is averaged
/// over the predictions so long and short words are comparable.
/// </summary>
public sealed class TrigramModel
{
    public const string Header = "# GreekSwitch trigram model v1";

    private readonly Dictionary<string, long> _trigrams;
    private readonly Dictionary<string, long> _contexts;
    private readonly double _k;
    private readonly int _vocabulary;

    public TrigramModel(Lang language, IReadOnlyDictionary<string, long> trigramCounts, double k = 0.1)
    {
        Language = language;
        _k = k;
        _trigrams = new Dictionary<string, long>(trigramCounts, StringComparer.Ordinal);
        _contexts = new Dictionary<string, long>(StringComparer.Ordinal);

        var symbols = new HashSet<char> { TrigramText.Unknown };
        foreach (var (trigram, count) in _trigrams)
        {
            var context = trigram[..2];
            _contexts[context] = _contexts.GetValueOrDefault(context) + count;
            symbols.Add(trigram[2]);
        }

        _vocabulary = symbols.Count;
    }

    public Lang Language { get; }

    public int TrigramCount => _trigrams.Count;

    /// <summary>
    /// Reads the text format written by NgramBuilder: UTF-8 lines of "abc&lt;TAB&gt;count";
    /// lines starting with '#' are comments.
    /// </summary>
    public static TrigramModel Load(Stream stream, Lang language, double k = 0.1)
    {
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        var lineNumber = 0;
        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var tab = line.IndexOf('\t', StringComparison.Ordinal);
            if (tab != 3 || !long.TryParse(line.AsSpan(4), NumberStyles.None, CultureInfo.InvariantCulture, out var count))
            {
                throw new FormatException($"Malformed trigram model line {lineNumber}.");
            }

            counts[line[..3]] = count;
        }

        return new TrigramModel(language, counts, k);
    }

    public double LogProb(string word)
    {
        var padded = TrigramText.Pad(TrigramText.Normalize(word, Language));
        var total = 0.0;
        var predictions = 0;
        for (var i = 2; i < padded.Length; i++)
        {
            var trigram = padded.Substring(i - 2, 3);
            var count = _trigrams.GetValueOrDefault(trigram);
            var context = _contexts.GetValueOrDefault(trigram[..2]);
            total += Math.Log((count + _k) / (context + (_k * _vocabulary)));
            predictions++;
        }

        return total / predictions;
    }
}
