namespace GreekSwitch.Core.Layout;

/// <summary>
/// The reverse of <see cref="DualDecoder"/>: which keys type a given text on a layout. Lets the
/// corrector retype a word as real key presses, because some apps (Windows 11 Notepad) mangle
/// injected Unicode characters right after a layout switch, but handle real keys fine.
/// </summary>
public static class KeyPlanner
{
    /// <summary>
    /// Keys that make <paramref name="map"/> produce <paramref name="text"/> with Caps Lock in
    /// the given state, or null if some character has no key. Space, '\n' and '\t' map to
    /// Space, Enter and Tab.
    /// </summary>
    public static IReadOnlyList<KeyStroke>? Plan(string text, LayoutMap map, bool capsLock)
    {
        var plain = new Dictionary<char, KeyStroke>();
        var dead = new Dictionary<char, KeyStroke>();
        foreach (var vk in map.Keys.Keys.Order())
        {
            foreach (var shift in (bool[])[false, true])
            {
                var stroke = new KeyStroke(vk, shift, capsLock);
                if (map.TryMap(stroke, out var output))
                {
                    (output.IsDeadKey ? dead : plain).TryAdd(output.Character, stroke);
                }
            }
        }

        var keys = new List<KeyStroke>(text.Length);
        foreach (var c in text)
        {
            switch (c)
            {
                case ' ':
                    keys.Add(new KeyStroke(VirtualKeys.Space));
                    continue;
                case '\n':
                    keys.Add(new KeyStroke(VirtualKeys.Return));
                    continue;
                case '\t':
                    keys.Add(new KeyStroke(VirtualKeys.Tab));
                    continue;
            }

            if (plain.TryGetValue(c, out var key))
            {
                keys.Add(key);
                continue;
            }

            var composed = map.Compositions.FirstOrDefault(kv => kv.Value == c).Key;
            if (composed != default && dead.TryGetValue(composed.Dead, out var deadKey) && plain.TryGetValue(composed.Base, out var baseKey))
            {
                keys.Add(deadKey);
                keys.Add(baseKey);
                continue;
            }

            return null;
        }

        return keys;
    }
}
