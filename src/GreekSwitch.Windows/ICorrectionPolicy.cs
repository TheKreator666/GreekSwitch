namespace GreekSwitch.Windows;

/// <summary>The app's on/off switches as the worker sees them.</summary>
public interface ICorrectionPolicy
{
    /// <summary>Enabled and not paused.</summary>
    bool IsActive { get; }

    /// <summary>True for executables (e.g. "cmd.exe") where GreekSwitch must stay passive.</summary>
    bool IsExcluded(string processName);

    /// <summary>True for executables where corrections are only suggested (Tab accepts), never made.</summary>
    bool IsSuggestOnly(string processName) => false;
}
