using V = GreekSwitch.Core.Layout.VirtualKeys;

namespace GreekSwitch.Core.Layout;

/// <summary>
/// Built-in maps for Greek (00000408) and US English (00000409), checked against Windows 11
/// with ToUnicodeEx. The Windows layer re-verifies them at startup; the OS wins on mismatch.
/// </summary>
public static class StaticLayouts
{
    public const string GreekLayoutId = "00000408";
    public const string EnglishLayoutId = "00000409";

    /// <summary>Greek tonos (΄): the dead key on VK_OEM_1.</summary>
    public const char Tonos = '΄';

    /// <summary>Dialytika (¨): Shift + VK_OEM_1. Combines with ι and υ.</summary>
    public const char Dialytika = '¨';

    /// <summary>Dialytika with tonos (΅): Shift + W. Produces ΐ and ΰ.</summary>
    public const char DialytikaTonos = '΅';

    public static LayoutMap Greek { get; } = BuildGreek();

    public static LayoutMap English { get; } = BuildEnglish();

    public static LayoutMap For(Lang lang) => lang == Lang.Greek ? Greek : English;

    private static Dictionary<int, KeyDefinition> SharedNonLetters(char oem102Normal, char oem102Shifted) => new()
    {
        [0x30] = new('0', ')'),
        [0x31] = new('1', '!'),
        [0x32] = new('2', '@'),
        [0x33] = new('3', '#'),
        [0x34] = new('4', '$'),
        [0x35] = new('5', '%'),
        [0x36] = new('6', '^'),
        [0x37] = new('7', '&'),
        [0x38] = new('8', '*'),
        [0x39] = new('9', '('),
        [V.OemPlus] = new('=', '+'),
        [V.OemComma] = new(',', '<'),
        [V.OemMinus] = new('-', '_'),
        [V.OemPeriod] = new('.', '>'),
        [V.Oem2] = new('/', '?'),
        [V.Oem3] = new('`', '~'),
        [V.Oem4] = new('[', '{'),
        [V.Oem5] = new('\\', '|'),
        [V.Oem6] = new(']', '}'),
        [V.Oem7] = new('\'', '"'),
        [V.Oem102] = new(oem102Normal, oem102Shifted),
    };

    private static LayoutMap BuildEnglish()
    {
        var keys = SharedNonLetters('\\', '|');
        for (var c = 'a'; c <= 'z'; c++)
        {
            var upper = char.ToUpperInvariant(c);
            keys[upper] = new(c, upper, CapsLockActsAsShift: true);
        }

        keys[V.Oem1] = new(';', ':');
        return new LayoutMap(Lang.English, EnglishLayoutId, keys);
    }

    private static LayoutMap BuildGreek()
    {
        var keys = SharedNonLetters('<', '>');

        // Letter keys A..Z in order; '-' marks Q and W, which are special-cased below.
        const string lower = "αβψδεφγηιξκλμνοπ-ρστθω-χυζ";
        const string upper = "ΑΒΨΔΕΦΓΗΙΞΚΛΜΝΟΠ-ΡΣΤΘΩ-ΧΥΖ";
        for (var i = 0; i < 26; i++)
        {
            if (lower[i] != '-')
            {
                keys['A' + i] = new(lower[i], upper[i], CapsLockActsAsShift: true);
            }
        }

        keys['Q'] = new(';', ':');                                  // ; is the Greek question mark
        keys['W'] = new('ς', DialytikaTonos, ShiftedIsDead: true);  // Caps Lock leaves ς alone
        keys[V.Oem1] = new(Tonos, Dialytika, NormalIsDead: true, ShiftedIsDead: true);

        var compositions = new Dictionary<(char, char), char>();
        void Add(char dead, string bases, string results)
        {
            for (var i = 0; i < bases.Length; i++)
            {
                compositions[(dead, bases[i])] = results[i];
            }
        }

        Add(Tonos, "αεηιουωΑΕΗΙΟΥΩ", "άέήίόύώΆΈΉΊΌΎΏ");
        Add(Dialytika, "ιυΙΥ", "ϊϋΪΫ");
        Add(DialytikaTonos, "ιυ", "ΐΰ");

        return new LayoutMap(Lang.Greek, GreekLayoutId, keys, compositions);
    }
}
