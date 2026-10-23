using GreekSwitch.Core;
using GreekSwitch.Core.Layout;

namespace GreekSwitch.Windows.Layouts;

/// <summary>What a real keyboard layout produces, one key at a time.</summary>
public interface IKeyProbe
{
    /// <summary>The character for a key, or null if it produces none (or several).</summary>
    KeyOutput? Probe(int virtualKey, bool shift, bool capsLock);

    /// <summary>What dead key + base produce: one composed character, or null if they don't combine.</summary>
    char? Compose(KeyStroke deadKey, KeyStroke baseKey);
}

/// <summary>A difference between the static map and the OS. Describes keys, never typed text.</summary>
public sealed record LayoutMismatch(int VirtualKey, string Detail);

public sealed record LayoutVerification(LayoutMap Map, IReadOnlyList<LayoutMismatch> Mismatches);

/// <summary>
/// Compares a static <see cref="LayoutMap"/> with what the OS layout does and returns a map
/// corrected to the OS wherever they differ.
/// </summary>
public static class LayoutComparer
{
    public static LayoutVerification Verify(LayoutMap expected, IKeyProbe probe)
    {
        var mismatches = new List<LayoutMismatch>();
        var keyOverrides = new Dictionary<int, KeyDefinition>();

        foreach (var (vk, def) in expected.Keys.OrderBy(k => k.Key))
        {
            var normal = probe.Probe(vk, shift: false, capsLock: false);
            var shifted = probe.Probe(vk, shift: true, capsLock: false);
            var caps = probe.Probe(vk, shift: false, capsLock: true);
            if (normal is not { } n || shifted is not { } s)
            {
                mismatches.Add(new LayoutMismatch(vk, "produces no single character in the OS layout; keeping the static entry"));
                continue;
            }

            var actual = new KeyDefinition(
                n.Character,
                s.Character,
                CapsLockActsAsShift: caps is { } c && c.Character == s.Character && n.Character != s.Character,
                NormalIsDead: n.IsDeadKey,
                ShiftedIsDead: s.IsDeadKey);

            if (actual != def)
            {
                keyOverrides[vk] = actual;
                mismatches.Add(new LayoutMismatch(vk, Describe(def, actual)));
            }
        }

        var corrected = expected.WithOverrides(keyOverrides);
        var compositionOverrides = new Dictionary<(char, char), char>();
        foreach (var ((dead, baseChar), composed) in expected.Compositions)
        {
            if (FindKey(corrected, dead, isDead: true) is not { } deadKey || FindKey(corrected, baseChar, isDead: false) is not { } baseKey)
            {
                continue;
            }

            var actual = probe.Compose(deadKey, baseKey);
            if (actual is { } a && a != composed)
            {
                compositionOverrides[(dead, baseChar)] = a;
                mismatches.Add(new LayoutMismatch(deadKey.VirtualKey, $"composition with VK 0x{baseKey.VirtualKey:X2} differs"));
            }
            else if (actual is null)
            {
                mismatches.Add(new LayoutMismatch(deadKey.VirtualKey, $"does not compose with VK 0x{baseKey.VirtualKey:X2} in the OS layout"));
            }
        }

        return new LayoutVerification(corrected.WithOverrides(compositions: compositionOverrides), mismatches);
    }

    private static KeyStroke? FindKey(LayoutMap map, char c, bool isDead)
    {
        foreach (var (vk, def) in map.Keys)
        {
            if (def.Normal == c && def.NormalIsDead == isDead)
            {
                return new KeyStroke(vk);
            }

            if (def.Shifted == c && def.ShiftedIsDead == isDead)
            {
                return new KeyStroke(vk, Shift: true);
            }
        }

        return null;
    }

    private static string Describe(KeyDefinition expected, KeyDefinition actual)
    {
        var parts = new List<string>();
        if (expected.Normal != actual.Normal || expected.NormalIsDead != actual.NormalIsDead)
        {
            parts.Add("unshifted");
        }

        if (expected.Shifted != actual.Shifted || expected.ShiftedIsDead != actual.ShiftedIsDead)
        {
            parts.Add("shifted");
        }

        if (expected.CapsLockActsAsShift != actual.CapsLockActsAsShift)
        {
            parts.Add("caps lock");
        }

        return "differs: " + string.Join(", ", parts);
    }
}
