using System.Globalization;
using System.Text;
using GreekSwitch.Core;
using GreekSwitch.Core.Scoring;
using GreekSwitch.Core.Text;
using NgramBuilder;

// NgramBuilder: corpus → character trigram counts (format: see data/ngrams/README.md).
//
//   NgramBuilder --lang el --aff el_GR.aff --dic el_GR.dic [--corpus extra.txt ...] --out el.tri
//
// --dic/--aff expand a Hunspell dictionary with its affix rules and count each form once.
// --corpus adds plain UTF-8 text, counting every word token (the hook for a real corpus later).
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var options = ParseArgs(args);
if (options is null)
{
    Console.Error.WriteLine("usage: NgramBuilder --lang el|en --out <file.tri> [--aff <aff> --dic <dic>] [--corpus <txt>]...");
    return 2;
}

var (lang, outPath, aff, dic, corpora) = options.Value;
var counts = new Dictionary<string, long>(StringComparer.Ordinal);
long words = 0, skipped = 0;

void Count(string word)
{
    var normalized = TrigramText.Normalize(word, lang);
    if (normalized.Length == 0 || normalized.Contains(TrigramText.Unknown, StringComparison.Ordinal))
    {
        skipped++;
        return;
    }

    var padded = TrigramText.Pad(normalized);
    for (var i = 2; i < padded.Length; i++)
    {
        var trigram = padded.Substring(i - 2, 3);
        counts[trigram] = counts.GetValueOrDefault(trigram) + 1;
    }

    words++;
}

var sources = new List<string>();
if (aff is not null && dic is not null)
{
    var expander = AffixExpander.Load(aff);
    var seen = new HashSet<string>(StringComparer.Ordinal);
    foreach (var line in File.ReadLines(dic, expander.Encoding).Skip(1))
    {
        foreach (var form in expander.Expand(line))
        {
            if (seen.Add(TrigramText.Normalize(form, lang)))
            {
                Count(form);
            }
        }
    }

    sources.Add(Path.GetFileName(dic) + " (affix-expanded)");
}

foreach (var corpus in corpora)
{
    foreach (var line in File.ReadLines(corpus, Encoding.UTF8))
    {
        foreach (var token in Tokenize(line))
        {
            Count(token);
        }
    }

    sources.Add(Path.GetFileName(corpus));
}

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
using (var writer = new StreamWriter(outPath, false, new UTF8Encoding(false)) { NewLine = "\n" })
{
    writer.WriteLine(TrigramModel.Header);
    writer.WriteLine($"# lang={(lang == Lang.Greek ? "el" : "en")} words={words} trigrams={counts.Count}");
    writer.WriteLine($"# sources={string.Join(", ", sources)}");
    foreach (var (trigram, count) in counts.OrderBy(kv => kv.Key, StringComparer.Ordinal))
    {
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{trigram}\t{count}"));
    }
}

Console.WriteLine($"{outPath}: {words} words, {counts.Count} trigrams, {skipped} skipped");
return 0;

static IEnumerable<string> Tokenize(string line)
{
    var sb = new StringBuilder();
    foreach (var c in line)
    {
        if (char.IsLetter(c) || c == '\'')
        {
            sb.Append(c);
        }
        else if (sb.Length > 0)
        {
            yield return sb.ToString();
            sb.Clear();
        }
    }

    if (sb.Length > 0)
    {
        yield return sb.ToString();
    }
}

static (Lang Lang, string Out, string? Aff, string? Dic, List<string> Corpora)? ParseArgs(string[] args)
{
    Lang? lang = null;
    string? output = null, aff = null, dic = null;
    var corpora = new List<string>();
    for (var i = 0; i + 1 < args.Length; i += 2)
    {
        switch (args[i])
        {
            case "--lang": lang = args[i + 1] == "el" ? Lang.Greek : args[i + 1] == "en" ? Lang.English : null; break;
            case "--out": output = args[i + 1]; break;
            case "--aff": aff = args[i + 1]; break;
            case "--dic": dic = args[i + 1]; break;
            case "--corpus": corpora.Add(args[i + 1]); break;
            default: return null;
        }
    }

    var hasDictionary = aff is not null && dic is not null;
    if (lang is null || output is null || (!hasDictionary && corpora.Count == 0) || (aff is null) != (dic is null))
    {
        return null;
    }

    return (lang.Value, output, aff, dic, corpora);
}
