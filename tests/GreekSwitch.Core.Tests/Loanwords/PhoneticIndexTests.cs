using System.Diagnostics;
using System.Text;
using GreekSwitch.Core.Loanwords;
using Xunit.Abstractions;

namespace GreekSwitch.Core.Tests.Loanwords;

public class PhoneticIndexTests(ITestOutputHelper output)
{
    private static PhoneticIndexWord Word(string word, double zipf, params string[] arpabet) =>
        new(word, zipf, [.. arpabet.Select(a => ArpabetPhonetics.Transcribe(a.Split(' '))!)]);

    private static PhoneticIndex Small() => PhoneticIndex.Build(
    [
        Word("container", 4.04, "K AH0 N T EY1 N ER0"),
        Word("server", 4.48, "S ER1 V ER0"),
        Word("servers", 4.0, "S ER1 V ER0 Z"),
        Word("ever", 5.5, "EH1 V ER0"),
        Word("either", 5.2, "IY1 DH ER0", "AY1 DH ER0"),
    ]);

    [Fact]
    public void Round_trips_through_the_binary_format()
    {
        var index = Small();
        using var stream = new MemoryStream();
        index.Write(stream);
        stream.Position = 0;

        var read = PhoneticIndex.Read(stream);

        read.WordCount.Should().Be(5);
        read.EntryCount.Should().Be(6, "either has two pronunciations");
        read.Word(0).Should().Be("container");
        read.Zipf(1).Should().BeApproximately(4.48, 0.02);
        Phonemes.Format(read.Phonemes(0)).Should().Be("k a n t e i n e r");
        read.StressFromEnd(0).Should().Be(2);
        read.WordOf(5).Should().Be(4);
    }

    [Theory]
    [InlineData("σέρβερ", new[] { "server", "servers" })] // same skeleton; one consonant more
    [InlineData("σέρφερ", new[] { "server", "servers" })] // f and v share a class
    [InlineData("σέλβερ", new[] { "server" })]            // one consonant different
    [InlineData("σέβερ", new[] { "server", "ever" })]     // one consonant fewer than server, one more than ever
    [InlineData("έβερ", new[] { "ever", "either" })]      // server is two consonants away; either one (v/th)
    [InlineData("κοντέινερ", new[] { "container" })]
    public void Finds_words_within_one_consonant_edit(string greek, string[] expected)
    {
        var index = Small();
        var found = new HashSet<int>();

        index.CollectCandidates(PhoneticSkeleton.Of(GreekPhonetics.Transcribe(greek)!.Phonemes), found);

        found.Select(e => index.Word(index.WordOf(e))).Distinct().Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Rejects_other_files()
    {
        var act = () => PhoneticIndex.Read(new MemoryStream(Encoding.ASCII.GetBytes("GSZF\u0001 not an index")));

        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Rejects_a_truncated_file()
    {
        using var stream = new MemoryStream();
        Small().Write(stream);
        var truncated = stream.ToArray()[..^10];

        var act = () => PhoneticIndex.Read(new MemoryStream(truncated));

        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void The_embedded_index_loads_quickly_and_holds_the_frequent_English_words()
    {
        PhoneticIndex.FromEmbeddedResources(); // warm up the reader code
        var watch = Stopwatch.StartNew();
        var index = PhoneticIndex.FromEmbeddedResources();
        watch.Stop();
        output.WriteLine($"Phonetic index: {index.WordCount} words, {index.EntryCount} pronunciations, {index.SizeInBytes / 1024} KB in arrays, loaded in {watch.Elapsed.TotalMilliseconds:F1} ms");

        index.WordCount.Should().BeGreaterThan(25_000);
        index.SizeInBytes.Should().BeLessThan(8_000_000);
        watch.ElapsedMilliseconds.Should().BeLessThan(500, "it's flat arrays, not text");
    }
}
