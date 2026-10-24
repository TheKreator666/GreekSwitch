using GreekSwitch.Core.Layout;
using GreekSwitch.Windows.Native;
using static GreekSwitch.Windows.Native.NativeMethods;

namespace GreekSwitch.Windows.Output;

/// <summary>Copy and paste as injected Ctrl+C / Ctrl+V, for "convert selection".</summary>
public static class SelectionKeys
{
    private const int KeyC = 'C';
    private const int KeyV = 'V';

    public static INPUT[] Copy() => InputBuilder.Chord([VirtualKeys.Control, KeyC]);

    /// <summary>Ctrl+V, then <paramref name="trailingSpaces"/> Space presses (in the same batch, so they land after the paste).</summary>
    public static INPUT[] Paste(int trailingSpaces = 0) =>
        [.. InputBuilder.Chord([VirtualKeys.Control, KeyV]), .. Enumerable.Repeat(InputBuilder.Chord([VirtualKeys.Space]), trailingSpaces).SelectMany(k => k)];

    /// <summary>GetClipboardSequenceNumber: changes whenever any program writes the clipboard.</summary>
    public static uint ClipboardSequence() => GetClipboardSequenceNumber();
}
