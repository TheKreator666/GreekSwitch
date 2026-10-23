using System.Collections.Frozen;

namespace GreekSwitch.Core.Layout;

/// <summary>
/// Virtual-key → character table for one keyboard layout, plus the dead-key compositions it
/// supports. Immutable; build a corrected copy with <see cref="WithKeys"/>.
/// </summary>
public sealed class LayoutMap
{
    private readonly FrozenDictionary<int, KeyDefinition> _keys;
    private readonly FrozenDictionary<(char Dead, char Base), char> _compositions;

    public LayoutMap(
        Lang language,
        string layoutId,
        IReadOnlyDictionary<int, KeyDefinition> keys,
        IReadOnlyDictionary<(char Dead, char Base), char>? compositions = null)
    {
        Language = language;
        LayoutId = layoutId;
        _keys = keys.ToFrozenDictionary();
        _compositions = (compositions ?? new Dictionary<(char, char), char>()).ToFrozenDictionary();
    }

    public Lang Language { get; }

    /// <summary>Windows keyboard layout identifier (KLID), e.g. "00000408".</summary>
    public string LayoutId { get; }

    public IReadOnlyDictionary<int, KeyDefinition> Keys => _keys;

    public IReadOnlyDictionary<(char Dead, char Base), char> Compositions => _compositions;

    public bool Contains(int virtualKey) => _keys.ContainsKey(virtualKey);

    public bool TryMap(KeyStroke stroke, out KeyOutput output)
    {
        if (!_keys.TryGetValue(stroke.VirtualKey, out var def))
        {
            output = default;
            return false;
        }

        var shifted = stroke.Shift ^ (stroke.CapsLock && def.CapsLockActsAsShift);
        output = shifted
            ? new KeyOutput(def.Shifted, def.ShiftedIsDead)
            : new KeyOutput(def.Normal, def.NormalIsDead);
        return true;
    }

    /// <summary>The precomposed character for dead key + base, or null if they don't combine.</summary>
    public char? Compose(char dead, char baseChar) =>
        _compositions.TryGetValue((dead, baseChar), out var composed) ? composed : null;

    /// <summary>A copy of this map with some key definitions replaced or added.</summary>
    public LayoutMap WithKeys(IReadOnlyDictionary<int, KeyDefinition> overrides)
    {
        var merged = new Dictionary<int, KeyDefinition>(_keys);
        foreach (var (vk, def) in overrides)
        {
            merged[vk] = def;
        }

        return new LayoutMap(Language, LayoutId, merged, _compositions);
    }
}
