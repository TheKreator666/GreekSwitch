using System.Diagnostics;
using System.Globalization;
using GreekSwitch.Core;
using GreekSwitch.Core.Loanwords;
using GreekSwitch.Core.Scoring;

// PhoneticIndexBuilder: CMUdict + the English frequency list → data/phonetic/en.idx, the
// phonetic index embedded in GreekSwitch.Core for loanword matching (data/phonetic/README.md).
//
//   dotnet run --project tools/PhoneticIndexBuilder -c Release -- --cmudict <cmudict.dict>
//       [--freq data/frequency/en.tsv] [--out data/phonetic/en.idx] [--max-pronunciations 2]
//
// Only words that matter are indexed: in the English frequency list (top ~50k by Zipf), letters
// a–z only, at least 3 letters, and a lowercase entry of the English dictionary (so names such
// as "maria" are left out), with their first pronunciations from CMUdict.
string? cmudict = null;
var freqPath = Path.Combine("data", "frequency", "en.tsv");
var outPath = Path.Combine("data", "phonetic", "en.idx");
var maxPronunciations = 2;
for (var i = 0; i + 1 < args.Length; i += 2)
{
    switch (args[i])
    {
        case "--cmudict": cmudict = args[i + 1]; break;
        case "--freq": freqPath = args[i + 1]; break;
        case "--out": outPath = args[i + 1]; break;
        case "--max-pronunciations": maxPronunciations = int.Parse(args[i + 1], CultureInfo.InvariantCulture); break;
        default: cmudict = null; i = args.Length; break;
    }
}

if (cmudict is null || maxPronunciations < 1)
{
    Console.Error.WriteLine("usage: PhoneticIndexBuilder --cmudict <cmudict.dict> [--freq <en.tsv>] [--out <en.idx>] [--max-pronunciations N]");
    return 2;
}

// CMUdict lines: "word P R O N" or "word(2) P R O N", optionally followed by "# comment".
var pronunciations = new Dictionary<string, List<PhoneticWord>>(StringComparer.Ordinal);
int lines = 0, unmapped = 0;
foreach (var line in File.ReadLines(cmudict))
{
    var content = line.Split('#', 2)[0].Trim();
    if (content.Length == 0)
    {
        continue;
    }

    lines++;
    var parts = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    var word = parts[0];
    var paren = word.IndexOf('(', StringComparison.Ordinal);
    if (paren > 0)
    {
        word = word[..paren];
    }

    if (ArpabetPhonetics.Transcribe(parts.Skip(1)) is not { } phonetic)
    {
        unmapped++;
        continue;
    }

    if (!pronunciations.TryGetValue(word, out var list))
    {
        pronunciations[word] = list = [];
    }

    if (list.Count < maxPronunciations && !list.Any(p => p.Phonemes.SequenceEqual(phonetic.Phonemes)))
    {
        list.Add(phonetic);
    }
}

var dictionary = LexiconDictionaryService.FromEmbeddedResources();
var words = new List<PhoneticIndexWord>();
int listed = 0, notWord = 0, notInCmu = 0;
var seen = new HashSet<string>(StringComparer.Ordinal);
foreach (var line in File.ReadLines(freqPath))
{
    if (line.Length == 0 || line[0] == '#')
    {
        continue;
    }

    var fields = line.Split('\t');
    var word = fields[0];
    listed++;
    if (word.Length < 3 || !word.All(c => c is >= 'a' and <= 'z') || !seen.Add(word))
    {
        continue;
    }

    if (!dictionary.IsWord(word, Lang.English))
    {
        notWord++;
        continue;
    }

    if (!pronunciations.TryGetValue(word, out var prons))
    {
        notInCmu++;
        continue;
    }

    words.Add(new PhoneticIndexWord(word, double.Parse(fields[1], CultureInfo.InvariantCulture), prons));
}

var index = PhoneticIndex.Build(words);
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
using (var stream = File.Create(outPath))
{
    index.Write(stream);
}

var watch = Stopwatch.StartNew();
using (var stream = File.OpenRead(outPath))
{
    PhoneticIndex.Read(stream);
}

Console.WriteLine($"CMUdict: {lines} pronunciations, {unmapped} unmapped, {pronunciations.Count} words");
Console.WriteLine($"Frequency list: {listed} words; {notWord} not lowercase dictionary words, {notInCmu} not in CMUdict");
Console.WriteLine($"{outPath}: {index.WordCount} words, {index.EntryCount} pronunciations, {new FileInfo(outPath).Length / 1024} KB, read back in {watch.ElapsedMilliseconds} ms");
return 0;
