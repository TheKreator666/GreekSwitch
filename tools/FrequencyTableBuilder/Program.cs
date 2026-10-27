using System.Globalization;
using GreekSwitch.Core;
using GreekSwitch.Core.Scoring;
using GreekSwitch.Core.Text;

// FrequencyTableBuilder: data/frequency/{el,en}.tsv (word<TAB>zipf, from export_wordfreq.py)
// → data/frequency/{el,en}.zipf, the compact binary table embedded in GreekSwitch.Core,
// and data/frequency/el.accents, the accented spelling of each Greek key (GreekAccentTable).
//
//   dotnet run --project tools/FrequencyTableBuilder -c Release -- data/frequency
//
// Keys are normalised the way WordFrequencyTable looks them up (Greek: no accents, lowercase,
// σ; English: lowercase). When several words share a key (που/πού), the most frequent wins.
var dir = args.Length > 0 ? args[0] : Path.Combine("data", "frequency");
foreach (var (lang, name) in new[] { (Lang.Greek, "el"), (Lang.English, "en") })
{
    var best = new Dictionary<string, (double Zipf, string Word)>(StringComparer.Ordinal);
    foreach (var line in File.ReadLines(Path.Combine(dir, name + ".tsv")))
    {
        if (line.Length == 0 || line[0] == '#')
        {
            continue;
        }

        var parts = line.Split('\t');
        var key = WordFrequencyTable.Key(parts[0], lang);
        var zipf = double.Parse(parts[1], CultureInfo.InvariantCulture);
        if (!best.TryGetValue(key, out var existing) || zipf > existing.Zipf)
        {
            best[key] = (zipf, parts[0]);
        }
    }

    var ordered = best.OrderByDescending(kv => kv.Value.Zipf).ThenBy(kv => kv.Key, StringComparer.Ordinal).ToList();
    var output = Path.Combine(dir, name + ".zipf");
    using (var stream = File.Create(output))
    {
        WordFrequencyTable.Write(stream, ordered.Select(kv => (kv.Key, kv.Value.Zipf)));
    }

    Console.WriteLine($"{output}: {best.Count} keys, {new FileInfo(output).Length / 1024} KB");

    if (lang == Lang.Greek)
    {
        // Only keys whose most frequent spelling carries an accent: "που" (not "πού") stays out.
        var accented = ordered
            .Select(kv => kv.Value.Word.ToLowerInvariant())
            .Where(w => w.All(GreekText.IsGreekLetter) && GreekText.StripDiacritics(w) != w)
            .ToList();
        var accents = Path.Combine(dir, name + ".accents");
        using (var stream = File.Create(accents))
        {
            GreekAccentTable.Write(stream, accented);
        }

        Console.WriteLine($"{accents}: {accented.Count} spellings, {new FileInfo(accents).Length / 1024} KB");
    }
}
