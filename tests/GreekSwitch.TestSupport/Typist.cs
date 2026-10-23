using GreekSwitch.Core;
using GreekSwitch.Core.Layout;

namespace GreekSwitch.TestSupport;

/// <summary>
/// Turns "what the user physically pressed" into key strokes: given the text that appeared and
/// the layout that was active, finds the keys that produce it (including dead-key sequences).
/// </summary>
public static class Typist
{
    public static IReadOnlyList<KeyStroke> Type(string text, Lang layout) => Type(text, StaticLayouts.For(layout));

    public static IReadOnlyList<KeyStroke> Type(string text, LayoutMap map)
    {
        var reverse = BuildReverse(map);
        var strokes = new List<KeyStroke>();
        foreach (var c in text)
        {
            if (!reverse.TryGetValue(c, out var keys))
            {
                throw new ArgumentException($"'{c}' (U+{(int)c:X4}) cannot be typed on {map.LayoutId}", nameof(text));
            }

            strokes.AddRange(keys);
        }

        return strokes;
    }

    /// <summary>Greek if the text contains any Greek letter, otherwise English.</summary>
    public static Lang InferLayout(string word) =>
        word.Any(c => c is >= 'Ͱ' and <= 'Ͽ') ? Lang.Greek : Lang.English;

    private static Dictionary<char, KeyStroke[]> BuildReverse(LayoutMap map)
    {
        var plain = new Dictionary<char, KeyStroke[]>();
        var dead = new Dictionary<char, KeyStroke>();

        foreach (var (vk, def) in map.Keys.OrderBy(k => k.Key))
        {
            Register(def.Normal, def.NormalIsDead, new KeyStroke(vk));
            Register(def.Shifted, def.ShiftedIsDead, new KeyStroke(vk, Shift: true));
        }

        foreach (var ((deadChar, baseChar), composed) in map.Compositions)
        {
            if (dead.TryGetValue(deadChar, out var deadKey) && plain.TryGetValue(baseChar, out var baseKeys))
            {
                plain.TryAdd(composed, [deadKey, .. baseKeys]);
            }
        }

        plain.TryAdd(' ', [new KeyStroke(VirtualKeys.Space)]);
        plain.TryAdd('\n', [new KeyStroke(VirtualKeys.Return)]);
        plain.TryAdd('\t', [new KeyStroke(VirtualKeys.Tab)]);
        return plain;

        void Register(char c, bool isDead, KeyStroke stroke)
        {
            if (isDead)
            {
                dead.TryAdd(c, stroke);
            }
            else
            {
                plain.TryAdd(c, [stroke]);
            }
        }
    }
}
