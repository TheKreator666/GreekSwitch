using Microsoft.Extensions.Logging;

namespace GreekSwitch.Core.Tests.Support;

/// <summary>Records every formatted log line plus every structured argument, at every level.</summary>
public sealed class CapturingLogger : ILogger
{
    private readonly List<string> _entries = [];

    public IReadOnlyList<string> Entries => _entries;

    /// <summary>Everything that would reach a log sink: messages, argument values and exceptions.</summary>
    public string AllOutput => string.Join("\n", _entries);

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        _entries.Add($"scope: {state}");
        return null;
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _entries.Add($"{logLevel}: {formatter(state, exception)}");
        if (state is IEnumerable<KeyValuePair<string, object?>> values)
        {
            foreach (var (key, value) in values)
            {
                _entries.Add($"  {key}={value}");
            }
        }

        if (exception is not null)
        {
            _entries.Add(exception.ToString());
        }
    }
}
