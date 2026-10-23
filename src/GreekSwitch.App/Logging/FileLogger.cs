using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace GreekSwitch.App.Logging;

/// <summary>
/// Minimal event log at %LOCALAPPDATA%\GreekSwitch\greekswitch.log, rolled over at 1 MB. It writes
/// what the pipeline logs: event names, languages, lengths, reasons and exception types with
/// stack traces. The pipeline never puts typed text into a log call (enforced by a unit test),
/// and exception messages are deliberately left out.
/// </summary>
internal sealed class FileLogger : ILogger
{
    private const long MaxBytes = 1024 * 1024;

    private readonly object _gate = new();
    private readonly LogLevel _minimum;

    public FileLogger(string path, LogLevel minimum)
    {
        FilePath = path;
        _minimum = minimum;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    }

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GreekSwitch", "greekswitch.log");

    public string FilePath { get; }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _minimum && logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var line = new StringBuilder()
            .Append(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture))
            .Append(' ')
            .Append(logLevel.ToString().ToUpperInvariant())
            .Append(' ')
            .Append(formatter(state, null));
        if (exception is not null)
        {
            line.Append(" | ").Append(exception.GetType().FullName).Append(Environment.NewLine).Append(exception.StackTrace);
        }

        lock (_gate)
        {
            try
            {
                if (File.Exists(FilePath) && new FileInfo(FilePath).Length > MaxBytes)
                {
                    File.Move(FilePath, FilePath + ".1", overwrite: true);
                }

                File.AppendAllText(FilePath, line.AppendLine().ToString());
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Logging must never take the app down; a lost line is acceptable.
            }
        }
    }
}
