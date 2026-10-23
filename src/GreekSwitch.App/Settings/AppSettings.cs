namespace GreekSwitch.App.Settings;

/// <summary>Contents of %APPDATA%\GreekSwitch\settings.json.</summary>
internal sealed class AppSettings
{
    public bool Enabled { get; set; } = true;

    /// <summary>Words with fewer keys are left alone unless dictionary and context agree.</summary>
    public int MinLength { get; set; } = 3;

    /// <summary>Score margin needed to replace when neither reading is a dictionary word.</summary>
    public double ReplaceThreshold { get; set; } = 2.0;

    public double DictionaryWeight { get; set; } = 2.0;

    public double NgramWeight { get; set; } = 1.0;

    public double ContextWeight { get; set; } = 0.3;

    /// <summary>
    /// Pause after the focused window reports a layout switch, before retyping a corrected word.
    /// Windows 11 Notepad still translated keys with the old layout at 0 ms; 30-60 ms was reliable.
    /// </summary>
    public int LayoutSwitchSettleMs { get; set; } = 50;

    /// <summary>Also correct words ended by Enter or Tab (off: Enter may have sent a chat message).</summary>
    public bool CorrectOnEnterAndTab { get; set; }

    /// <summary>Executables where GreekSwitch is fully passive. Matched case-insensitively.</summary>
    public List<string> ExcludedProcesses { get; set; } = [.. DefaultExcludedProcesses];

    /// <summary>Words never to convert. Filled by undo, editable in Settings.</summary>
    public List<string> NeverConvert { get; set; } = [];

    /// <summary>Debug builds only: log the words themselves. Ignored in Release builds.</summary>
    public bool DebugLogText { get; set; }

    public static IReadOnlyList<string> DefaultExcludedProcesses { get; } =
    [
        // Terminals and editors, where text is code or commands.
        "WindowsTerminal.exe", "OpenConsole.exe", "conhost.exe", "cmd.exe", "powershell.exe", "pwsh.exe",
        "mintty.exe", "putty.exe", "Code.exe", "devenv.exe", "rider64.exe", "idea64.exe", "nvim-qt.exe",

        // Password managers.
        "KeePass.exe", "KeePassXC.exe", "1Password.exe", "Bitwarden.exe",

        // Launchers and games (WASD and chat shortcuts are not words).
        "steam.exe", "steamwebhelper.exe", "EpicGamesLauncher.exe", "Battle.net.exe", "EADesktop.exe",
        "RiotClientServices.exe", "GalaxyClient.exe", "upc.exe", "UbisoftConnect.exe",
        "League of Legends.exe", "VALORANT-Win64-Shipping.exe", "cs2.exe", "csgo.exe", "dota2.exe",
        "FortniteClient-Win64-Shipping.exe", "GTA5.exe", "r5apex.exe", "RocketLeague.exe", "Overwatch.exe",
        "javaw.exe", "Minecraft.Windows.exe",
    ];
}
