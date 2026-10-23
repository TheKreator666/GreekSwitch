using static GreekSwitch.Windows.Native.NativeMethods;

namespace GreekSwitch.Windows.Focus;

/// <summary>
/// Windows where Enter runs something: terminals, and IDEs with integrated terminals. A correction
/// ended by Enter retypes Enter, which would run the corrected text as a new command, so
/// such corrections are never made there, whatever the settings or exclusions say.
/// </summary>
public static class CommandWindows
{
    private static readonly HashSet<string> Processes = new(StringComparer.OrdinalIgnoreCase)
    {
        "WindowsTerminal.exe", "OpenConsole.exe", "conhost.exe", "cmd.exe", "powershell.exe", "pwsh.exe",
        "wsl.exe", "bash.exe", "ssh.exe", "mintty.exe", "putty.exe", "alacritty.exe", "wezterm-gui.exe",
        "Code.exe", "devenv.exe", "rider64.exe", "idea64.exe", "pycharm64.exe", "webstorm64.exe",
    };

    private static readonly HashSet<string> WindowClasses = new(StringComparer.Ordinal)
    {
        "ConsoleWindowClass", "CASCADIA_HOSTING_WINDOW_CLASS", "PuTTY", "mintty",
    };

    public static bool IsCommandProcess(string processName) => Processes.Contains(processName);

    public static bool IsCommandWindowClass(string className) => WindowClasses.Contains(className);

    /// <summary>True if <paramref name="hwnd"/> (or its top-level window) belongs to a terminal or IDE.</summary>
    public static bool IsCommandWindow(nint hwnd)
    {
        if (hwnd == 0)
        {
            return false;
        }

        if (ProcessNames.FromWindow(hwnd) is { } process && IsCommandProcess(process))
        {
            return true;
        }

        return IsCommandWindowClass(ClassName(hwnd)) || IsCommandWindowClass(ClassName(GetForegroundWindow()));
    }

    private static unsafe string ClassName(nint hwnd)
    {
        var buffer = stackalloc char[256];
        var length = GetClassNameW(hwnd, buffer, 256);
        return length > 0 ? new string(buffer, 0, length) : "";
    }
}
