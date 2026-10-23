namespace GreekSwitch.Core.Layout;

/// <summary>Windows virtual-key codes used by the core logic.</summary>
public static class VirtualKeys
{
    public const int Back = 0x08;
    public const int Tab = 0x09;
    public const int Return = 0x0D;
    public const int Shift = 0x10;
    public const int Control = 0x11;
    public const int Menu = 0x12;
    public const int Capital = 0x14;
    public const int Escape = 0x1B;
    public const int Space = 0x20;
    public const int Prior = 0x21;
    public const int Next = 0x22;
    public const int End = 0x23;
    public const int Home = 0x24;
    public const int Left = 0x25;
    public const int Up = 0x26;
    public const int Right = 0x27;
    public const int Down = 0x28;
    public const int Insert = 0x2D;
    public const int Delete = 0x2E;
    public const int D0 = 0x30;
    public const int D9 = 0x39;
    public const int A = 0x41;
    public const int Z = 0x5A;
    public const int LWin = 0x5B;
    public const int RWin = 0x5C;
    public const int NumLock = 0x90;
    public const int Scroll = 0x91;
    public const int LShift = 0xA0;
    public const int RShift = 0xA1;
    public const int LControl = 0xA2;
    public const int RControl = 0xA3;
    public const int LMenu = 0xA4;
    public const int RMenu = 0xA5;
    public const int Oem1 = 0xBA;      // US ;:   Greek tonos / dialytika dead key
    public const int OemPlus = 0xBB;   // =+
    public const int OemComma = 0xBC;  // ,<
    public const int OemMinus = 0xBD;  // -_
    public const int OemPeriod = 0xBE; // .>
    public const int Oem2 = 0xBF;      // /?
    public const int Oem3 = 0xC0;      // `~
    public const int Oem4 = 0xDB;      // [{
    public const int Oem5 = 0xDC;      // \|
    public const int Oem6 = 0xDD;      // ]}
    public const int Oem7 = 0xDE;      // '"
    public const int Oem102 = 0xE2;    // extra key on ISO keyboards

    public static bool IsLetter(int vk) => vk is >= A and <= Z;

    public static bool IsDigit(int vk) => vk is >= D0 and <= D9;

    /// <summary>Modifier and lock keys: pressing them alone neither types nor resets.</summary>
    public static bool IsModifier(int vk) => vk is Shift or Control or Menu or Capital or LWin or RWin
        or LShift or RShift or LControl or RControl or LMenu or RMenu or NumLock or Scroll;
}
