using GreekSwitch.Core.Layout;
using GreekSwitch.Windows.Native;
using static GreekSwitch.Windows.Native.NativeMethods;

namespace GreekSwitch.Windows.Output;

/// <summary>
/// Builds the SendInput batch for one correction: <c>backspaces</c> × Backspace, then the text as
/// KEYEVENTF_UNICODE events, so it comes out right whatever the active layout. '\n' and '\t' are
/// sent as real Enter and Tab keys, because many apps ignore them as Unicode characters.
/// </summary>
public static class InputBuilder
{
    /// <summary>Marks our own events in dwExtraInfo ("GSWT"), in addition to the LLKHF_INJECTED flag.</summary>
    public const nuint Signature = 0x47535754;

    public static INPUT[] Build(int backspaces, string text)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(backspaces);
        var inputs = new List<INPUT>((backspaces + text.Length) * 2);

        for (var i = 0; i < backspaces; i++)
        {
            AddVirtualKey(inputs, VirtualKeys.Back);
        }

        foreach (var c in text)
        {
            switch (c)
            {
                case '\n':
                    AddVirtualKey(inputs, VirtualKeys.Return);
                    break;
                case '\t':
                    AddVirtualKey(inputs, VirtualKeys.Tab);
                    break;
                default:
                    inputs.Add(Key(0, c, KEYEVENTF_UNICODE));
                    inputs.Add(Key(0, c, KEYEVENTF_UNICODE | KEYEVENTF_KEYUP));
                    break;
            }
        }

        return [.. inputs];
    }

    private static void AddVirtualKey(List<INPUT> inputs, int vk)
    {
        inputs.Add(Key((ushort)vk, 0, 0));
        inputs.Add(Key((ushort)vk, 0, KEYEVENTF_KEYUP));
    }

    private static INPUT Key(ushort vk, ushort scan, uint flags) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT { wVk = vk, wScan = scan, dwFlags = flags, dwExtraInfo = Signature },
        },
    };
}
