using GreekSwitch.App.Settings;
using GreekSwitch.Windows;

namespace GreekSwitch.App;

/// <summary>Enabled / paused / excluded state, written on the UI thread and read by the worker.</summary>
internal sealed class AppPolicy : ICorrectionPolicy
{
    private static readonly HashSet<string> AlwaysExcluded = new(AppSettings.AlwaysExcludedProcesses, StringComparer.OrdinalIgnoreCase);

    private volatile bool _enabled = true;
    private volatile HashSet<string> _excluded = new(StringComparer.OrdinalIgnoreCase);
    private long _pausedUntilTicks;

    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    public DateTime PausedUntilUtc
    {
        get => new(Interlocked.Read(ref _pausedUntilTicks), DateTimeKind.Utc);
        set => Interlocked.Exchange(ref _pausedUntilTicks, value.Ticks);
    }

    public bool IsPaused => DateTime.UtcNow < PausedUntilUtc;

    public bool IsActive => _enabled && !IsPaused;

    public bool IsExcluded(string processName) => _excluded.Contains(processName) || AlwaysExcluded.Contains(processName);


    public void Apply(AppSettings settings)
    {
        _enabled = settings.Enabled;
        _excluded = new HashSet<string>(settings.ExcludedProcesses, StringComparer.OrdinalIgnoreCase);
    }
}
