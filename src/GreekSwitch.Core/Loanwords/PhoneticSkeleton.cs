namespace GreekSwitch.Core.Loanwords;

/// <summary>
/// The consonant skeleton used to find candidates: the consonants of a word in their coarse
/// classes (<see cref="Phonemes.SkeletonClass"/>), vowels and y dropped (container → k n t n r).
/// English words are indexed under their skeleton and under every skeleton with one consonant
/// deleted (the SymSpell trick), so a lookup of the typed word's skeleton and its own one-consonant
/// deletions finds every word within one consonant edit, without trying substitutions.
/// </summary>
public static class PhoneticSkeleton
{
    public static byte[] Of(ReadOnlySpan<Phoneme> phonemes)
    {
        var skeleton = new List<byte>(phonemes.Length);
        foreach (var p in phonemes)
        {
            if (Phonemes.SkeletonClass(p) is { } c)
            {
                skeleton.Add((byte)c);
            }
        }

        return [.. skeleton];
    }

    /// <summary>A 32-bit FNV-1a key. Collisions only add candidates, which are scored anyway.</summary>
    public static uint Key(ReadOnlySpan<byte> skeleton)
    {
        var hash = 2166136261u;
        foreach (var b in skeleton)
        {
            hash = (hash ^ (uint)(b + 1)) * 16777619u;
        }

        return hash;
    }

    /// <summary>Keys of the skeleton with each one consonant deleted, without repeats. None for a skeleton of one.</summary>
    public static IReadOnlyList<uint> DeletionKeys(ReadOnlySpan<byte> skeleton)
    {
        if (skeleton.Length < 2)
        {
            return [];
        }

        var keys = new List<uint>(skeleton.Length);
        Span<byte> buffer = stackalloc byte[skeleton.Length - 1];
        for (var skip = 0; skip < skeleton.Length; skip++)
        {
            if (skip > 0 && skeleton[skip] == skeleton[skip - 1])
            {
                continue; // deleting either of two equal neighbours gives the same skeleton
            }

            skeleton[..skip].CopyTo(buffer);
            skeleton[(skip + 1)..].CopyTo(buffer[skip..]);
            var key = Key(buffer);
            if (!keys.Contains(key))
            {
                keys.Add(key);
            }
        }

        return keys;
    }
}
