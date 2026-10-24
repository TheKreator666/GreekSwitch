using GreekSwitch.App.Policy;
using GreekSwitch.App.Settings;
using GreekSwitch.Windows;

namespace GreekSwitch.App;

/// <summary>
/// Enabled / paused / excluded state, written on the UI thread and read by the worker. Built
/// from the user's settings with the IT policy (<see cref="ManagedPolicy"/>) on top.
/// </summary>
internal sealed class AppPolicy : ICorrectionPolicy
{
    private volatile bool _enabled = true;
    private volatile HashSet<string> _excluded = new(StringComparer.OrdinalIgnoreCase);
    private volatile HashSet<string> _suggestOnly = new(StringComparer.OrdinalIgnoreCase);
    private volatile HashSet<string> _alwaysExcluded = new(AppSettings.AlwaysExcludedProcesses, StringComparer.OrdinalIgnoreCase);
    private long _pausedUntilTicks;

    public bool Enabled => _enabled;

    public DateTime PausedUntilUtc
    {
        get => new(Interlocked.Read(ref _pausedUntilTicks), DateTimeKind.Utc);
        set => Interlocked.Exchange(ref _pausedUntilTicks, value.Ticks);
    }

    public bool IsPaused => DateTime.UtcNow < PausedUntilUtc;

    public bool IsActive => _enabled && !IsPaused;

    public bool IsExcluded(string processName) => _excluded.Contains(processName) || _alwaysExcluded.Contains(processName);

    public bool IsSuggestOnly(string processName) => _suggestOnly.Contains(processName);

    public void Apply(AppSettings settings, ManagedPolicy managed)
    {
        _enabled = managed.EffectiveEnabled(settings);
        _excluded = new HashSet<string>(managed.EffectiveExcluded(settings), StringComparer.OrdinalIgnoreCase);
        _suggestOnly = new HashSet<string>(settings.SuggestOnlyProcesses, StringComparer.OrdinalIgnoreCase);
        _alwaysExcluded = new HashSet<string>(AppSettings.AlwaysExcludedProcesses.Concat(managed.AdditionalAlwaysExcluded), StringComparer.OrdinalIgnoreCase);
    }
}
