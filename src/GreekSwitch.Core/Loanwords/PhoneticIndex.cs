using System.Runtime.InteropServices;
using System.Text;
using GreekSwitch.Core.Data;

namespace GreekSwitch.Core.Loanwords;

/// <summary>One English word to index: its Zipf frequency and its pronunciations.</summary>
public sealed record PhoneticIndexWord(string Word, double Zipf, IReadOnlyList<PhoneticWord> Pronunciations);

/// <summary>
/// English words with their pronunciations in the shared phoneme alphabet, and the
/// consonant-skeleton and one-deletion indexes that find candidates for a Greek word. Built by
/// tools/PhoneticIndexBuilder from CMUdict and the English frequency list, stored as
/// data/phonetic/en.idx, embedded in Core, and loaded as flat arrays (nothing parsed as text).
/// Immutable after construction, so it can be shared between threads.
/// <para>
/// Binary format v1 (data/phonetic/README.md), little-endian:
/// "GSPI", version 1, then the int32 counts words, entries, word bytes, phoneme bytes, skeleton
/// pairs, deletion pairs; then word offsets int32[words + 1], word UTF-8 bytes, Zipf × 25 byte[words];
/// entry word int32[entries], entry offsets int32[entries + 1], entry stress sbyte[entries], phoneme
/// bytes; skeleton keys uint32[], skeleton entries int32[]; deletion keys uint32[], deletion entries int32[].
/// Key arrays are sorted (ties by entry).
/// </para>
/// </summary>
public sealed class PhoneticIndex
{
    public const string Magic = "GSPI";
    public const byte Version = 1;
    public const double ZipfQuantum = 25.0;

    private readonly int[] _wordOffsets;
    private readonly byte[] _wordBytes;
    private readonly byte[] _zipf;
    private readonly int[] _entryWord;
    private readonly int[] _entryOffsets;
    private readonly sbyte[] _entryStress;
    private readonly byte[] _phonemes;
    private readonly uint[] _skeletonKeys;
    private readonly int[] _skeletonEntries;
    private readonly uint[] _deletionKeys;
    private readonly int[] _deletionEntries;

    private PhoneticIndex(
        int[] wordOffsets,
        byte[] wordBytes,
        byte[] zipf,
        int[] entryWord,
        int[] entryOffsets,
        sbyte[] entryStress,
        byte[] phonemes,
        uint[] skeletonKeys,
        int[] skeletonEntries,
        uint[] deletionKeys,
        int[] deletionEntries)
    {
        _wordOffsets = wordOffsets;
        _wordBytes = wordBytes;
        _zipf = zipf;
        _entryWord = entryWord;
        _entryOffsets = entryOffsets;
        _entryStress = entryStress;
        _phonemes = phonemes;
        _skeletonKeys = skeletonKeys;
        _skeletonEntries = skeletonEntries;
        _deletionKeys = deletionKeys;
        _deletionEntries = deletionEntries;
    }

    public int WordCount => _zipf.Length;

    /// <summary>Pronunciations; a word can have more than one.</summary>
    public int EntryCount => _entryWord.Length;

    /// <summary>Approximate bytes held by the arrays.</summary>
    public long SizeInBytes =>
        (4L * (_wordOffsets.Length + _entryWord.Length + _entryOffsets.Length + _skeletonEntries.Length + _deletionEntries.Length
               + _skeletonKeys.Length + _deletionKeys.Length))
        + _wordBytes.Length + _zipf.Length + _entryStress.Length + _phonemes.Length;

    public static PhoneticIndex FromEmbeddedResources()
    {
        using var stream = EmbeddedData.Open(EmbeddedData.EnglishPhonetic);
        return Read(stream);
    }

    public string Word(int wordId) =>
        Encoding.UTF8.GetString(_wordBytes, _wordOffsets[wordId], _wordOffsets[wordId + 1] - _wordOffsets[wordId]);

    public double Zipf(int wordId) => _zipf[wordId] / ZipfQuantum;

    public int WordOf(int entry) => _entryWord[entry];

    public ReadOnlySpan<Phoneme> Phonemes(int entry) =>
        MemoryMarshal.Cast<byte, Phoneme>(_phonemes.AsSpan(_entryOffsets[entry], _entryOffsets[entry + 1] - _entryOffsets[entry]));

    public int StressFromEnd(int entry) => _entryStress[entry];

    /// <summary>
    /// Adds every entry within one consonant edit of <paramref name="skeleton"/>: entries with the
    /// same skeleton, one consonant more, one fewer, or one different.
    /// </summary>
    public void CollectCandidates(ReadOnlySpan<byte> skeleton, ISet<int> into)
    {
        if (skeleton.Length == 0)
        {
            return;
        }

        var own = PhoneticSkeleton.Key(skeleton);
        AddMatches(_skeletonKeys, _skeletonEntries, own, into);  // same skeleton
        AddMatches(_deletionKeys, _deletionEntries, own, into);  // English has one consonant more
        foreach (var key in PhoneticSkeleton.DeletionKeys(skeleton))
        {
            AddMatches(_skeletonKeys, _skeletonEntries, key, into); // English has one fewer
            AddMatches(_deletionKeys, _deletionEntries, key, into); // one consonant differs
        }
    }

    public static PhoneticIndex Build(IEnumerable<PhoneticIndexWord> words)
    {
        var wordOffsets = new List<int> { 0 };
        var wordBytes = new List<byte>();
        var zipf = new List<byte>();
        var entryWord = new List<int>();
        var entryOffsets = new List<int> { 0 };
        var entryStress = new List<sbyte>();
        var phonemes = new List<byte>();
        var skeletons = new List<(uint Key, int Entry)>();
        var deletions = new List<(uint Key, int Entry)>();

        foreach (var word in words)
        {
            var wordId = zipf.Count;
            wordBytes.AddRange(Encoding.UTF8.GetBytes(word.Word));
            wordOffsets.Add(wordBytes.Count);
            zipf.Add(QuantiseZipf(word.Zipf));

            foreach (var pronunciation in word.Pronunciations)
            {
                var skeleton = PhoneticSkeleton.Of(pronunciation.Phonemes);
                if (skeleton.Length == 0 || pronunciation.Length > PhoneticDistance.MaxLength)
                {
                    continue;
                }

                var entry = entryWord.Count;
                entryWord.Add(wordId);
                phonemes.AddRange(pronunciation.Phonemes.Select(p => (byte)p));
                entryOffsets.Add(phonemes.Count);
                entryStress.Add((sbyte)Math.Clamp(pronunciation.StressFromEnd, -1, sbyte.MaxValue));
                skeletons.Add((PhoneticSkeleton.Key(skeleton), entry));
                deletions.AddRange(PhoneticSkeleton.DeletionKeys(skeleton).Select(k => (k, entry)));
            }
        }

        skeletons.Sort();
        deletions.Sort();
        return new PhoneticIndex(
            [.. wordOffsets],
            [.. wordBytes],
            [.. zipf],
            [.. entryWord],
            [.. entryOffsets],
            [.. entryStress],
            [.. phonemes],
            [.. skeletons.Select(s => s.Key)],
            [.. skeletons.Select(s => s.Entry)],
            [.. deletions.Select(s => s.Key)],
            [.. deletions.Select(s => s.Entry)]);
    }

    public void Write(Stream stream)
    {
        EnsureLittleEndian();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Encoding.ASCII.GetBytes(Magic));
        writer.Write(Version);
        writer.Write(WordCount);
        writer.Write(EntryCount);
        writer.Write(_wordBytes.Length);
        writer.Write(_phonemes.Length);
        writer.Write(_skeletonKeys.Length);
        writer.Write(_deletionKeys.Length);
        writer.Write(MemoryMarshal.AsBytes(_wordOffsets.AsSpan()));
        writer.Write(_wordBytes);
        writer.Write(_zipf);
        writer.Write(MemoryMarshal.AsBytes(_entryWord.AsSpan()));
        writer.Write(MemoryMarshal.AsBytes(_entryOffsets.AsSpan()));
        writer.Write(MemoryMarshal.AsBytes(_entryStress.AsSpan()));
        writer.Write(_phonemes);
        writer.Write(MemoryMarshal.AsBytes(_skeletonKeys.AsSpan()));
        writer.Write(MemoryMarshal.AsBytes(_skeletonEntries.AsSpan()));
        writer.Write(MemoryMarshal.AsBytes(_deletionKeys.AsSpan()));
        writer.Write(MemoryMarshal.AsBytes(_deletionEntries.AsSpan()));
    }

    public static PhoneticIndex Read(Stream stream)
    {
        EnsureLittleEndian();
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != Magic || reader.ReadByte() != Version)
        {
            throw new FormatException("Not a GreekSwitch phonetic index (v1).");
        }

        var words = Count(reader);
        var entries = Count(reader);
        var wordByteCount = Count(reader);
        var phonemeCount = Count(reader);
        var skeletonCount = Count(reader);
        var deletionCount = Count(reader);

        var index = new PhoneticIndex(
            ReadArray<int>(reader, words + 1),
            ReadBytes(reader, wordByteCount),
            ReadBytes(reader, words),
            ReadArray<int>(reader, entries),
            ReadArray<int>(reader, entries + 1),
            ReadArray<sbyte>(reader, entries),
            ReadBytes(reader, phonemeCount),
            ReadArray<uint>(reader, skeletonCount),
            ReadArray<int>(reader, skeletonCount),
            ReadArray<uint>(reader, deletionCount),
            ReadArray<int>(reader, deletionCount));
        index.Validate();
        return index;
    }

    private static byte QuantiseZipf(double zipf) => (byte)Math.Clamp(Math.Round(zipf * ZipfQuantum), 0, 255);

    private static void AddMatches(uint[] keys, int[] entries, uint key, ISet<int> into)
    {
        var lo = 0;
        var hi = keys.Length;
        while (lo < hi)
        {
            var mid = (lo + hi) >>> 1;
            if (keys[mid] < key)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        for (var i = lo; i < keys.Length && keys[i] == key; i++)
        {
            into.Add(entries[i]);
        }
    }

    private static int Count(BinaryReader reader)
    {
        var count = reader.ReadInt32();
        return count is >= 0 and < 64_000_000 ? count : throw new FormatException("Corrupt phonetic index.");
    }

    private static byte[] ReadBytes(BinaryReader reader, int count)
    {
        var bytes = reader.ReadBytes(count);
        return bytes.Length == count ? bytes : throw new FormatException("Truncated phonetic index.");
    }

    private static T[] ReadArray<T>(BinaryReader reader, int count)
        where T : unmanaged
    {
        var array = new T[count];
        var bytes = MemoryMarshal.AsBytes(array.AsSpan());
        if (reader.Read(bytes) != bytes.Length)
        {
            throw new FormatException("Truncated phonetic index.");
        }

        return array;
    }

    private static void EnsureLittleEndian()
    {
        if (!BitConverter.IsLittleEndian)
        {
            throw new PlatformNotSupportedException("The phonetic index is little-endian.");
        }
    }

    private void Validate()
    {
        var ok = _wordOffsets[0] == 0 && _wordOffsets[^1] == _wordBytes.Length
            && _entryOffsets[0] == 0 && _entryOffsets[^1] == _phonemes.Length
            && _entryWord.All(w => (uint)w < (uint)WordCount)
            && _skeletonEntries.All(e => (uint)e < (uint)EntryCount)
            && _deletionEntries.All(e => (uint)e < (uint)EntryCount)
            && _phonemes.All(p => p < Loanwords.Phonemes.Count);
        for (var i = 1; ok && i < _entryOffsets.Length; i++)
        {
            ok = _entryOffsets[i] >= _entryOffsets[i - 1];
        }

        for (var i = 1; ok && i < _wordOffsets.Length; i++)
        {
            ok = _wordOffsets[i] >= _wordOffsets[i - 1];
        }

        if (!ok)
        {
            throw new FormatException("Corrupt phonetic index.");
        }
    }
}
