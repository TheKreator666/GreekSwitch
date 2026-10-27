using GreekSwitch.Core.Loanwords;

namespace GreekSwitch.App.Settings;

/// <summary>Contents of %APPDATA%\GreekSwitch\settings.json.</summary>
internal sealed class AppSettings
{
    /// <summary>Version of this file's schema/defaults; older files are migrated on load.</summary>
    public const int CurrentVersion = 3;

    /// <summary>
    /// Defaults added in version 2 (remote desktops, VMs), merged once into existing files'
    /// exclusion lists. Each later version adds its own list here.
    /// </summary>
    public static IReadOnlyList<string> AddedInVersion2 { get; } =
    [
        "mstsc.exe", "msrdc.exe", "wfica32.exe", "CDViewer.exe", "vmconnect.exe", "vmware.exe",
        "vmware-vmx.exe", "VirtualBoxVM.exe", "AnyDesk.exe", "TeamViewer.exe", "parsecd.exe",
    ];

    /// <summary>Schema version of this file (1 = written before versioning).</summary>
    public int SettingsVersion { get; set; } = CurrentVersion;

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

    /// <summary>Words never to convert that the user typed into Settings (readable, editable).</summary>
    public List<string> NeverConvert { get; set; } = [];

    /// <summary>
    /// Words learned from undo, as HMAC-SHA256 hashes only: an undone "word" may be a password
    /// typed into a box we couldn't recognise, so it is never stored as text.
    /// </summary>
    public List<string> NeverConvertLearned { get; set; } = [];

    /// <summary>The HMAC key for <see cref="NeverConvertLearned"/>, encrypted with DPAPI for this Windows user.</summary>
    public string? NeverConvertKey { get; set; }

    /// <summary>Learned from "convert last word" on a word the engine kept: HMAC hashes, same key as above.</summary>
    public List<string> AlwaysConvertLearned { get; set; } = [];

    /// <summary>Hotkey: convert the word before the caret to the other layout.</summary>
    public string ConvertLastWordHotkey { get; set; } = "Ctrl+Alt+Space";

    /// <summary>Hotkey: convert the selected text (copy, convert, paste, restore the clipboard).</summary>
    public string ConvertSelectionHotkey { get; set; } = "Ctrl+Alt+Shift+Space";

    /// <summary>Hotkey: the same switch as the tray menu's Enabled.</summary>
    public string ToggleHotkey { get; set; } = "Ctrl+Alt+Shift+G";

    /// <summary>Flash the tray icon briefly when a correction happens.</summary>
    public bool FlashOnCorrection { get; set; } = true;

    /// <summary>Fix words typed with Caps Lock on by accident ("hELLO" → "Hello") and turn Caps Lock off.</summary>
    public bool FixAccidentalCapsLock { get; set; }

    /// <summary>Add the accents to Greek words typed without them (ειναι → είναι), in either layout.</summary>
    public bool RestoreAccents { get; set; } = true;

    /// <summary>
    /// Suggest the English spelling of loanwords typed in Greek (κοντέινερ → container), milestone 15.
    /// The policy value DisableLoanwordSuggestions forces it off.
    /// </summary>
    public bool SuggestLoanwords { get; set; } = true;

    /// <summary>Tier 2: replace strong matches on non-dictionary Greek words with tech terms automatically (ντεπλόι → deploy). Off by default.</summary>
    public bool AutoReplaceLoanwords { get; set; }

    /// <summary>Loanword pairs the user typed into Settings (readable, editable): always replaced.</summary>
    public List<LoanwordPair> LoanwordPairs { get; set; } = [];

    /// <summary>
    /// Pairs learned from accepted and undone suggestions: an HMAC hash of the Greek word (same key
    /// as above), the English dictionary target, and accept/reject counts. Never the Greek word.
    /// </summary>
    public List<LearnedLoanword> LoanwordLearned { get; set; } = [];

    /// <summary>Executables where corrections are only suggested (Tab accepts), never made automatically.</summary>
    public List<string> SuggestOnlyProcesses { get; set; } = [];

    /// <summary>The first-run dialog has been shown.</summary>
    public bool FirstRunCompleted { get; set; }

    /// <summary>Debug builds only: log the words themselves. Ignored in Release builds.</summary>
    public bool DebugLogText { get; set; }

    /// <summary>
    /// Always excluded, whatever settings.json says, so nothing (including another program
    /// editing the file) can switch GreekSwitch on inside a password manager.
    /// </summary>
    public static IReadOnlyList<string> AlwaysExcludedProcesses { get; } =
    [
        "KeePass.exe", "KeePassXC.exe", "1Password.exe", "Bitwarden.exe", "Dashlane.exe",
        "NordPass.exe", "Enpass.exe", "RoboForm.exe", "LastPass.exe", "ProtonPass.exe",
    ];

    public static IReadOnlyList<string> DefaultExcludedProcesses { get; } =
    [
        // Terminals and editors, where text is code or commands.
        "WindowsTerminal.exe", "OpenConsole.exe", "conhost.exe", "cmd.exe", "powershell.exe", "pwsh.exe",
        "mintty.exe", "putty.exe", "Code.exe", "devenv.exe", "rider64.exe", "idea64.exe", "nvim-qt.exe",

        // Remote desktops and virtual machines: keys are decoded with the *local* layout there,
        // while the remote side has its own.
        .. AddedInVersion2,

        // Launchers and games (WASD and chat shortcuts are not words).
        "steam.exe", "steamwebhelper.exe", "EpicGamesLauncher.exe", "Battle.net.exe", "EADesktop.exe",
        "RiotClientServices.exe", "GalaxyClient.exe", "upc.exe", "UbisoftConnect.exe",
        "League of Legends.exe", "VALORANT-Win64-Shipping.exe", "cs2.exe", "csgo.exe", "dota2.exe",
        "FortniteClient-Win64-Shipping.exe", "GTA5.exe", "r5apex.exe", "RocketLeague.exe", "Overwatch.exe",
        "javaw.exe", "Minecraft.Windows.exe",
    ];
}
