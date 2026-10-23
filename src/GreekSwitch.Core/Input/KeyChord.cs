using System.Globalization;
using GreekSwitch.Core.Layout;

namespace GreekSwitch.Core.Input;

/// <summary>A hotkey: one key plus exact modifiers.</summary>
public readonly record struct KeyChord(int VirtualKey, bool Control = false, bool Alt = false, bool Shift = false, bool Win = false)
{
    /// <summary>True if <paramref name="key"/> is this chord, with exactly these modifiers.</summary>
    public bool Matches(KeyInput key) =>
        key.VirtualKey == VirtualKey && key.Control == Control && key.Alt == Alt && key.Shift == Shift && key.Win == Win;
}

/// <summary>Hotkeys as text ("Ctrl+Alt+Space"), for settings.json and the Settings dialog.</summary>
public static class HotkeyText
{
    private static readonly Dictionary<string, int> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Space"] = VirtualKeys.Space,
        ["Pause"] = 0x13,
        ["Insert"] = VirtualKeys.Insert,
        ["Delete"] = VirtualKeys.Delete,
        ["Home"] = VirtualKeys.Home,
        ["End"] = VirtualKeys.End,
        ["PageUp"] = VirtualKeys.Prior,
        ["PageDown"] = VirtualKeys.Next,
        ["ScrollLock"] = VirtualKeys.Scroll,
    };

    /// <summary>Parses "Ctrl+Alt+Space", "Pause", "Win+Shift+K"…; null if it isn't one key plus modifiers.</summary>
    public static KeyChord? Parse(string text)
    {
        bool ctrl = false, alt = false, shift = false, win = false;
        int? key = null;
        foreach (var raw in text.Split('+'))
        {
            var part = raw.Trim();
            switch (part.ToUpperInvariant())
            {
                case "CTRL" or "CONTROL":
                    ctrl = true;
                    continue;
                case "ALT":
                    alt = true;
                    continue;
                case "SHIFT":
                    shift = true;
                    continue;
                case "WIN":
                    win = true;
                    continue;
            }

            if (key is not null || KeyCode(part) is not { } vk)
            {
                return null; // a second key, or an unknown one
            }

            key = vk;
        }

        return key is { } k ? new KeyChord(k, ctrl, alt, shift, win) : null;
    }

    public static string Format(KeyChord chord)
    {
        var parts = new List<string>();
        if (chord.Win)
        {
            parts.Add("Win");
        }

        if (chord.Control)
        {
            parts.Add("Ctrl");
        }

        if (chord.Alt)
        {
            parts.Add("Alt");
        }

        if (chord.Shift)
        {
            parts.Add("Shift");
        }

        parts.Add(KeyName(chord.VirtualKey));
        return string.Join('+', parts);
    }

    private static int? KeyCode(string name)
    {
        if (NamedKeys.TryGetValue(name, out var named))
        {
            return named;
        }

        if (name.Length == 1 && char.IsAsciiLetterOrDigit(name[0]))
        {
            return char.ToUpperInvariant(name[0]);
        }

        if (name.Length is 2 or 3 && (name[0] is 'F' or 'f')
            && int.TryParse(name.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var f) && f is >= 1 and <= 24)
        {
            return 0x70 + f - 1;
        }

        return null;
    }

    private static string KeyName(int vk)
    {
        foreach (var (name, code) in NamedKeys)
        {
            if (code == vk)
            {
                return name;
            }
        }

        return vk is >= 0x70 and <= 0x87 ? $"F{vk - 0x70 + 1}" : ((char)vk).ToString();
    }
}
