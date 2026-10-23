using GreekSwitch.App.Logging;
using GreekSwitch.App.Settings;
using GreekSwitch.App.UI;
using Microsoft.Extensions.Logging;

namespace GreekSwitch.App;

internal static class Program
{
    private const string MutexName = @"Local\GreekSwitch.SingleInstance";

    [STAThread]
    private static int Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, MutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show("GreekSwitch is already running (see the tray).", "GreekSwitch", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 1;
        }

#if DEBUG
        var logger = new FileLogger(FileLogger.DefaultPath, LogLevel.Debug);
#else
        var logger = new FileLogger(FileLogger.DefaultPath, LogLevel.Information);
#endif
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            logger.LogCritical("Unhandled exception: {Type}{NewLine}{Stack}", ex?.GetType().FullName, Environment.NewLine, ex?.StackTrace);
        };
        Application.ThreadException += (_, e) =>
            logger.LogError("UI exception: {Type}{NewLine}{Stack}", e.Exception.GetType().FullName, Environment.NewLine, e.Exception.StackTrace);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        ApplicationConfiguration.Initialize();
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

        using var context = new TrayApplicationContext(new SettingsStore(SettingsStore.DefaultDirectory, logger), logger);
        Application.Run(context);
        return 0;
    }
}
