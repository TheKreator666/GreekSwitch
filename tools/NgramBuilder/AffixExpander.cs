using System.Text;
using System.Text.RegularExpressions;

namespace NgramBuilder;

/// <summary>
/// Minimal Hunspell affix expander: reads SET, PFX and SFX (strip, add, condition, cross-product)
/// and yields each stem plus its affixed forms. It covers what el_GR and en_US use for ordinary
/// words; compounding, twofold affixes and morphology are ignored, as they barely move
/// character statistics.
/// </summary>
internal sealed class AffixExpander
{
    private readonly Dictionary<char, AffixClass> _prefixes = [];
    private readonly Dictionary<char, AffixClass> _suffixes = [];

    private AffixExpander(Encoding encoding) => Encoding = encoding;

    public Encoding Encoding { get; }

    public static AffixExpander Load(string affPath)
    {
        var encoding = DetectEncoding(affPath);
        var expander = new AffixExpander(encoding);
        AffixClass? current = null;
        var remaining = 0;

        foreach (var raw in File.ReadLines(affPath, encoding))
        {
            var line = StripComment(raw);
            var parts = line.Split((char[])[' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                continue;
            }

            if (parts[0] == "FLAG")
            {
                throw new NotSupportedException($"FLAG {parts[1]} is not supported; only single-character flags.");
            }

            if (parts[0] is not ("PFX" or "SFX") || parts.Length < 4)
            {
                continue;
            }

            var isPrefix = parts[0] == "PFX";
            var flag = parts[1][0];
            if (remaining == 0)
            {
                // Header: PFX/SFX flag cross_product count
                current = new AffixClass(isPrefix, parts[2] == "Y");
                (isPrefix ? expander._prefixes : expander._suffixes)[flag] = current;
                remaining = int.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture);
                continue;
            }

            // Rule: PFX/SFX flag strip add [condition]
            var strip = parts[2] == "0" ? "" : parts[2];
            var add = parts[3].Split('/')[0];
            add = add == "0" ? "" : add;
            var condition = parts.Length > 4 ? parts[4] : ".";
            current!.Rules.Add(new AffixRule(strip, add, BuildCondition(condition, isPrefix)));
            remaining--;
        }

        return expander;
    }

    /// <summary>Yields the stem and every form produced by its affix flags.</summary>
    public IEnumerable<string> Expand(string dicLine)
    {
        var entry = dicLine.Split('\t', ' ')[0];
        var slash = entry.IndexOf('/', StringComparison.Ordinal);
        var stem = slash < 0 ? entry : entry[..slash];
        var flags = slash < 0 ? "" : entry[(slash + 1)..];
        if (stem.Length == 0)
        {
            yield break;
        }

        yield return stem;

        var suffixed = new List<(string Word, bool Cross)>();
        foreach (var flag in flags)
        {
            if (_suffixes.TryGetValue(flag, out var sfx))
            {
                foreach (var rule in sfx.Rules)
                {
                    if (rule.Condition.IsMatch(stem) && stem.EndsWith(rule.Strip, StringComparison.Ordinal))
                    {
                        var word = stem[..^rule.Strip.Length] + rule.Add;
                        suffixed.Add((word, sfx.CrossProduct));
                        yield return word;
                    }
                }
            }
        }

        foreach (var flag in flags)
        {
            if (!_prefixes.TryGetValue(flag, out var pfx))
            {
                continue;
            }

            foreach (var rule in pfx.Rules)
            {
                if (rule.Condition.IsMatch(stem) && stem.StartsWith(rule.Strip, StringComparison.Ordinal))
                {
                    yield return rule.Add + stem[rule.Strip.Length..];
                }

                if (!pfx.CrossProduct)
                {
                    continue;
                }

                foreach (var (word, cross) in suffixed)
                {
                    if (cross && rule.Condition.IsMatch(word) && word.StartsWith(rule.Strip, StringComparison.Ordinal))
                    {
                        yield return rule.Add + word[rule.Strip.Length..];
                    }
                }
            }
        }
    }

    private static Encoding DetectEncoding(string affPath)
    {
        foreach (var line in File.ReadLines(affPath, Encoding.Latin1))
        {
            if (line.StartsWith("SET ", StringComparison.Ordinal))
            {
                var name = line[4..].Trim();
                return name.Equals("UTF-8", StringComparison.OrdinalIgnoreCase)
                    ? new UTF8Encoding(false)
                    : Encoding.GetEncoding(name.Replace("ISO8859", "ISO-8859", StringComparison.OrdinalIgnoreCase));
            }
        }

        return Encoding.Latin1;
    }

    private static string StripComment(string line)
    {
        var hash = line.IndexOf('#', StringComparison.Ordinal);
        return hash < 0 ? line : line[..hash];
    }

    /// <summary>Hunspell conditions are character-class patterns anchored at the affix end.</summary>
    private static Regex BuildCondition(string condition, bool isPrefix) =>
        condition == "."
            ? new Regex("", RegexOptions.CultureInvariant)
            : new Regex(isPrefix ? "^" + condition : condition + "$", RegexOptions.CultureInvariant);

    private sealed record AffixRule(string Strip, string Add, Regex Condition);

    private sealed class AffixClass(bool isPrefix, bool crossProduct)
    {
        public bool IsPrefix { get; } = isPrefix;

        public bool CrossProduct { get; } = crossProduct;

        public List<AffixRule> Rules { get; } = [];
    }
}
