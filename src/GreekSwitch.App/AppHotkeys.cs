using GreekSwitch.App.Settings;
using GreekSwitch.Core.Input;

namespace GreekSwitch.App;

internal enum HotkeyAction
{
    ConvertLastWord = 1,
    ConvertSelection = 2,
    Toggle = 3,
}

/// <summary>The app's hotkeys, as configured in settings.json.</summary>
internal static class AppHotkeys
{
    public static string Name(HotkeyAction action) => action switch
    {
        HotkeyAction.ConvertLastWord => "Convert last word",
        HotkeyAction.ConvertSelection => "Convert selection",
        _ => "Turn on/off",
    };

    public static string Text(AppSettings s, HotkeyAction action) => action switch
    {
        HotkeyAction.ConvertLastWord => s.ConvertLastWordHotkey,
        HotkeyAction.ConvertSelection => s.ConvertSelectionHotkey,
        _ => s.ToggleHotkey,
    };

    /// <summary>The configured chords; a blank or unparsable entry means that hotkey is off.</summary>
    public static IReadOnlyDictionary<HotkeyAction, KeyChord> Parse(AppSettings s)
    {
        var chords = new Dictionary<HotkeyAction, KeyChord>();
        foreach (var action in Enum.GetValues<HotkeyAction>())
        {
            if (!string.IsNullOrWhiteSpace(Text(s, action)) && HotkeyText.Parse(Text(s, action)) is { } chord && !chords.ContainsValue(chord))
            {
                chords[action] = chord;
            }
        }

        return chords;
    }
}
