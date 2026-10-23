using static GreekSwitch.Windows.Native.NativeMethods;

namespace GreekSwitch.Windows;

public static class ErrorReporting
{
    /// <summary>
    /// Excludes this executable from Windows Error Reporting, so a crash doesn't produce a report
    /// or dump containing whatever was in memory (such as the word being typed).
    /// </summary>
    /// <returns>True if Windows accepted the exclusion.</returns>
    public static bool ExcludeThisApp() =>
        Environment.ProcessPath is { } path && WerAddExcludedApplication(System.IO.Path.GetFileName(path), false) >= 0;
}
