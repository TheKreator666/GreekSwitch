namespace GreekSwitch.Core;

/// <summary>
/// One physical key press as captured by the hook: the virtual-key code plus the modifier
/// state that affects which character the layout produces.
/// </summary>
/// <param name="VirtualKey">Windows virtual-key code (VK_*).</param>
/// <param name="Shift">Shift was held.</param>
/// <param name="CapsLock">Caps Lock was toggled on.</param>
/// <param name="IsDeadKey">
/// Set by the producer when it already knows the key was a dead key in the active layout.
/// Informational only: <see cref="Layout.DualDecoder"/> derives dead-key behaviour from each
/// <see cref="Layout.LayoutMap"/>, because a key can be dead in one layout and not the other.
/// </param>
public readonly record struct KeyStroke(int VirtualKey, bool Shift = false, bool CapsLock = false, bool IsDeadKey = false);
