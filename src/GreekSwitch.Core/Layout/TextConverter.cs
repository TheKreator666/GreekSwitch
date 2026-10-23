using System.Text;
using GreekSwitch.Core.Text;

namespace GreekSwitch.Core.Layout;

/// <summary>
/// Re-reads text in the other layout, word by word: the keys that typed each word (found from
/// its script) are decoded with the other layout. Used by "convert selection". Whitespace is kept
/// as it is; a word with no letters, or with a character no key types, is left unchanged.
/// </summary>
public sealed class TextConverter
{
    private readonly LayoutMap _greek;
    private readonly LayoutMap _english;
    private readonly DualDecoder _decoder;

    public TextConverter(LayoutMap greek, LayoutMap english)
    {
        _greek = greek;
        _english = english;
        _decoder = new DualDecoder(greek, english);
    }

    public string Convert(string text)
    {
        var result = new StringBuilder(text.Length);
        var word = new StringBuilder();
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                result.Append(ConvertWord(word.ToString())).Append(c);
                word.Clear();
            }
            else
            {
                word.Append(c);
            }
        }

        return result.Append(ConvertWord(word.ToString())).ToString();
    }

    /// <summary>The language most of the text's letters are in (null if it has none).</summary>
    public static Lang? Script(string text)
    {
        var greek = text.Count(GreekText.IsGreekLetter);
        var latin = text.Count(char.IsAsciiLetter);
        return greek == 0 && latin == 0 ? null : greek >= latin ? Lang.Greek : Lang.English;
    }

    private string ConvertWord(string word)
    {
        if (Script(word) is not { } source)
        {
            return word;
        }

        var keys = KeyPlanner.Plan(word, source == Lang.Greek ? _greek : _english, capsLock: false);
        if (keys is null)
        {
            return word;
        }

        var target = source.Other();
        var converted = _decoder.Decode(keys, target);
        return target == Lang.Greek ? GreekText.FixFinalSigma(converted) : converted;
    }
}
