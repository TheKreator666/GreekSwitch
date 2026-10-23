using GreekSwitch.Core;
using GreekSwitch.Core.Layout;
using GreekSwitch.Windows.Native;
using static GreekSwitch.Windows.Native.NativeMethods;

namespace GreekSwitch.Windows.Output;

/// <summary>
/// Builds SendInput batches for one correction: <c>backspaces</c> × Backspace, then the text.
/// <see cref="BuildKeys"/> types the text as real key presses (preferred). <see cref="Build"/>
/// types it as KEYEVENTF_UNICODE events, the fallback for characters no key produces. Windows 11
/// Notepad mangles those right after a layout switch.
/// </summary>
public static class InputBuilder
{
    /// <summary>Marks our own events in dwExtraInfo ("GSWT"), in addition to the LLKHF_INJECTED flag.</summary>
    public const nuint Signature = 0x47535754;

    /// <summary>Backspaces, then <paramref name="keys"/> as real key presses (Shift added where needed).</summary>
    /// <param name="scanCode">Scan code for a virtual key; apps that read scan codes need it.</param>
    public static INPUT[] BuildKeys(int backspaces, IReadOnlyList<KeyStroke> keys, Func<int, ushort> scanCode)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(backspaces);
        var inputs = new List<INPUT>((backspaces + keys.Count) * 4);

        for (var i = 0; i < backspaces; i++)
        {
            AddVirtualKey(inputs, VirtualKeys.Back, scanCode);
        }

        foreach (var key in keys)
        {
            if (key.Shift)
            {
                inputs.Add(Key(VirtualKeys.Shift, scanCode(VirtualKeys.Shift), 0));
            }

            AddVirtualKey(inputs, key.VirtualKey, scanCode);

            if (key.Shift)
            {
                inputs.Add(Key(VirtualKeys.Shift, scanCode(VirtualKeys.Shift), KEYEVENTF_KEYUP));
            }
        }

        return [.. inputs];
    }

    /// <summary>Presses <paramref name="keys"/> together: down in order, up in reverse (e.g. Alt+Shift).</summary>
    public static INPUT[] Chord(IReadOnlyList<int> keys)
    {
        var inputs = new List<INPUT>(keys.Count * 2);
        inputs.AddRange(keys.Select(vk => Key(vk, 0, 0)));
        inputs.AddRange(keys.Reverse().Select(vk => Key(vk, 0, KEYEVENTF_KEYUP)));
        return [.. inputs];
    }

    /// <summary>Backspaces, then <paramref name="text"/> as Unicode events ('\n' and '\t' as Enter and Tab).</summary>
    public static INPUT[] Build(int backspaces, string text)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(backspaces);
        var inputs = new List<INPUT>((backspaces + text.Length) * 2);
        static ushort NoScan(int vk) => 0;

        for (var i = 0; i < backspaces; i++)
        {
            AddVirtualKey(inputs, VirtualKeys.Back, NoScan);
        }

        foreach (var c in text)
        {
            switch (c)
            {
                case '\n':
                    AddVirtualKey(inputs, VirtualKeys.Return, NoScan);
                    break;
                case '\t':
                    AddVirtualKey(inputs, VirtualKeys.Tab, NoScan);
                    break;
                default:
                    inputs.Add(Key(0, c, KEYEVENTF_UNICODE));
                    inputs.Add(Key(0, c, KEYEVENTF_UNICODE | KEYEVENTF_KEYUP));
                    break;
            }
        }

        return [.. inputs];
    }

    private static void AddVirtualKey(List<INPUT> inputs, int vk, Func<int, ushort> scanCode)
    {
        var scan = scanCode(vk);
        inputs.Add(Key((ushort)vk, scan, 0));
        inputs.Add(Key((ushort)vk, scan, KEYEVENTF_KEYUP));
    }

    private static INPUT Key(int vk, ushort scan, uint flags) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT { wVk = (ushort)vk, wScan = scan, dwFlags = flags, dwExtraInfo = Signature },
        },
    };
}
