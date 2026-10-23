namespace GreekSwitch.Core.Layout;

/// <summary>What one key produces in a layout, without and with Shift.</summary>
/// <param name="Normal">Character without Shift.</param>
/// <param name="Shifted">Character with Shift.</param>
/// <param name="CapsLockActsAsShift">Caps Lock inverts Shift for this key (letters).</param>
/// <param name="NormalIsDead">The unshifted character is a dead key (an accent waiting for a base).</param>
/// <param name="ShiftedIsDead">The shifted character is a dead key.</param>
public sealed record KeyDefinition(
    char Normal,
    char Shifted,
    bool CapsLockActsAsShift = false,
    bool NormalIsDead = false,
    bool ShiftedIsDead = false);

/// <summary>Result of mapping one key stroke through a layout.</summary>
public readonly record struct KeyOutput(char Character, bool IsDeadKey);
