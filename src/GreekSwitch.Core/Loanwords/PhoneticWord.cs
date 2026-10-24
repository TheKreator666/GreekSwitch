namespace GreekSwitch.Core.Loanwords;

/// <summary>
/// A word in the shared phoneme alphabet, plus the position of its stressed vowel counted from
/// the last vowel (0 = stress on the last vowel, 1 = the one before, …; -1 = unknown). Counting
/// from the end keeps Greek tonos and CMUdict stress comparable when one side has an extra
/// vowel near the start.
/// </summary>
public sealed record PhoneticWord(Phoneme[] Phonemes, int StressFromEnd)
{
    public int Length => Phonemes.Length;

    public override string ToString() => Loanwords.Phonemes.Format(Phonemes);
}

/// <summary>Collects phonemes and the stressed vowel while a word is transcribed.</summary>
internal sealed class PhoneticWordBuilder
{
    private readonly List<Phoneme> _phonemes = new(16);
    private int _vowels;
    private int _stressedVowel = -1;

    public int Count => _phonemes.Count;

    public void Add(Phoneme p, bool stressed = false)
    {
        if (Phonemes.IsVowel(p))
        {
            if (stressed && _stressedVowel < 0)
            {
                _stressedVowel = _vowels;
            }

            _vowels++;
        }

        _phonemes.Add(p);
    }

    public void Add(Phoneme first, Phoneme second, bool stressed = false)
    {
        Add(first, stressed);
        Add(second);
    }

    public PhoneticWord Build() =>
        new([.. _phonemes], _stressedVowel < 0 ? -1 : _vowels - 1 - _stressedVowel);
}
