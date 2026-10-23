namespace GreekSwitch.Core.Input;

/// <summary>A key-down event as reported by the Windows layer, with every modifier that matters.</summary>
public readonly record struct KeyInput(
    int VirtualKey,
    bool Shift = false,
    bool CapsLock = false,
    bool Control = false,
    bool Alt = false,
    bool Win = false)
{
    public KeyStroke ToStroke() => new(VirtualKey, Shift, CapsLock);
}
