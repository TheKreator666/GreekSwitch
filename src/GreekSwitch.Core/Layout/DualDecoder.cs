using System.Text;

namespace GreekSwitch.Core.Layout;

/// <summary>
/// Reads one key sequence through both layouts. Case comes from Shift/Caps Lock on each key,
/// so case patterns (lower, Capitalized, ALL CAPS) carry over between the two readings.
/// </summary>
public sealed class DualDecoder
{
    private readonly LayoutMap _greek;
    private readonly LayoutMap _english;

    public DualDecoder(LayoutMap greek, LayoutMap english)
    {
        _greek = greek;
        _english = english;
    }

    public LayoutMap Map(Lang lang) => lang == Lang.Greek ? _greek : _english;

    public (string Greek, string English) Decode(IReadOnlyList<KeyStroke> keys) =>
        (Decode(keys, Lang.Greek), Decode(keys, Lang.English));

    /// <summary>
    /// Exactly what the layout would have put on screen, following the Windows dead-key rules:
    /// dead + matching base → precomposed letter; dead + anything else → spacing accent, then the
    /// character; a trailing dead key comes out as its spacing accent.
    /// </summary>
    public string Decode(IReadOnlyList<KeyStroke> keys, Lang lang)
    {
        var map = Map(lang);
        var sb = new StringBuilder(keys.Count);
        char? pendingDead = null;

        foreach (var key in keys)
        {
            if (!map.TryMap(key, out var output))
            {
                continue;
            }

            if (pendingDead is { } dead)
            {
                pendingDead = null;
                if (!output.IsDeadKey && map.Compose(dead, output.Character) is { } composed)
                {
                    sb.Append(composed);
                }
                else
                {
                    sb.Append(dead).Append(output.Character);
                }
            }
            else if (output.IsDeadKey)
            {
                pendingDead = output.Character;
            }
            else
            {
                sb.Append(output.Character);
            }
        }

        if (pendingDead is { } trailing)
        {
            sb.Append(trailing);
        }

        return sb.ToString();
    }
}
