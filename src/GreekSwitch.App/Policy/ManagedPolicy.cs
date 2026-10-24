using GreekSwitch.App.Settings;
using Microsoft.Extensions.Logging;

namespace GreekSwitch.App.Policy;

/// <summary>
/// Settings fixed by IT through the policy keys. They override settings.json without changing
/// it, so removing a policy gives the user back their own choice. Null or false = not set.
/// </summary>
internal sealed record ManagedPolicy
{
    public const string EnabledValue = "Enabled";
    public const string ExcludedProcessesValue = "ExcludedProcesses";
    public const string AdditionalAlwaysExcludedValue = "AdditionalAlwaysExcluded";
    public const string DisableUndoLearningValue = "DisableUndoLearning";
    public const string DisableLoanwordSuggestionsValue = "DisableLoanwordSuggestions";

    public static ManagedPolicy None { get; } = new();

    /// <summary>On or off, locked; the tray menu, the toggle hotkey and Settings can't change it.</summary>
    public bool? Enabled { get; init; }

    /// <summary>Replaces the user's excluded apps (locked in Settings).</summary>
    public IReadOnlyList<string>? ExcludedProcesses { get; init; }

    /// <summary>Added to <see cref="AppSettings.AlwaysExcludedProcesses"/>; nothing can switch them back on.</summary>
    public IReadOnlyList<string> AdditionalAlwaysExcluded { get; init; } = [];

    /// <summary>No new words are learned from undo or the convert hotkey. Words already learned still apply.</summary>
    public bool DisableUndoLearning { get; init; }

    /// <summary>Forces <see cref="AppSettings.SuggestLoanwords"/> off.</summary>
    public bool DisableLoanwordSuggestions { get; init; }

    public bool IsEmpty =>
        Enabled is null && ExcludedProcesses is null && AdditionalAlwaysExcluded.Count == 0 && !DisableUndoLearning && !DisableLoanwordSuggestions;

    /// <summary>
    /// Reads both policy keys. For each value, machine policy (HKLM) wins over user policy
    /// (HKCU); the extra always-excluded apps from both are combined, since adding exclusions
    /// can only make GreekSwitch do less. A value of the wrong type is ignored.
    /// </summary>
    public static ManagedPolicy Read(IPolicyRegistry registry)
    {
        bool? Flag(string name) => First(registry, name, AsFlag);
        IReadOnlyList<string>? List(PolicyScope scope, string name) => AsList(registry.GetValue(scope, name));

        return new ManagedPolicy
        {
            Enabled = Flag(EnabledValue),
            ExcludedProcesses = List(PolicyScope.Machine, ExcludedProcessesValue) ?? List(PolicyScope.User, ExcludedProcessesValue),
            AdditionalAlwaysExcluded =
            [
                .. (List(PolicyScope.Machine, AdditionalAlwaysExcludedValue) ?? [])
                    .Concat(List(PolicyScope.User, AdditionalAlwaysExcludedValue) ?? [])
                    .Distinct(StringComparer.OrdinalIgnoreCase),
            ],
            DisableUndoLearning = Flag(DisableUndoLearningValue) ?? false,
            DisableLoanwordSuggestions = Flag(DisableLoanwordSuggestionsValue) ?? false,
        };
    }

    public bool EffectiveEnabled(AppSettings settings) => Enabled ?? settings.Enabled;

    public IReadOnlyList<string> EffectiveExcluded(AppSettings settings) => ExcludedProcesses ?? settings.ExcludedProcesses;

    public bool EffectiveSuggestLoanwords(AppSettings settings) => settings.SuggestLoanwords && !DisableLoanwordSuggestions;

    /// <summary>Logs which values are set: names, flags and counts only (no process names).</summary>
    public void Log(ILogger logger)
    {
        if (IsEmpty)
        {
            return;
        }

        logger.LogInformation(
            "Policy: Enabled {Enabled}, ExcludedProcesses {Excluded}, AdditionalAlwaysExcluded {Extra}, DisableUndoLearning {NoLearning}, DisableLoanwordSuggestions {NoLoanwords}",
            Enabled?.ToString() ?? "not set",
            ExcludedProcesses is null ? "not set" : $"{ExcludedProcesses.Count} app(s)",
            AdditionalAlwaysExcluded.Count,
            DisableUndoLearning,
            DisableLoanwordSuggestions);
    }

    public bool Equals(ManagedPolicy? other) =>
        other is not null
        && Enabled == other.Enabled
        && SameList(ExcludedProcesses, other.ExcludedProcesses)
        && SameList(AdditionalAlwaysExcluded, other.AdditionalAlwaysExcluded)
        && DisableUndoLearning == other.DisableUndoLearning
        && DisableLoanwordSuggestions == other.DisableLoanwordSuggestions;

    public override int GetHashCode() => HashCode.Combine(Enabled, ExcludedProcesses?.Count, AdditionalAlwaysExcluded.Count, DisableUndoLearning, DisableLoanwordSuggestions);

    private static T? First<T>(IPolicyRegistry registry, string name, Func<object?, T?> parse)
        where T : struct =>
        parse(registry.GetValue(PolicyScope.Machine, name)) ?? parse(registry.GetValue(PolicyScope.User, name));

    /// <summary>REG_DWORD (or REG_QWORD) 0/1; a REG_SZ "0"/"1" is accepted too, since .reg files and GPP often write strings.</summary>
    private static bool? AsFlag(object? value) => value switch
    {
        int i => i != 0,
        long l => l != 0,
        string s when int.TryParse(s.Trim(), out var i) => i != 0,
        _ => null,
    };

    /// <summary>REG_MULTI_SZ, one entry per line; a REG_SZ with entries separated by ';' is accepted too.</summary>
    private static IReadOnlyList<string>? AsList(object? value)
    {
        IEnumerable<string>? items = value switch
        {
            string[] lines => lines,
            string s => s.Split(';'),
            _ => null,
        };

        return items is null ? null : [.. items.Select(i => i.Trim()).Where(i => i.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private static bool SameList(IReadOnlyList<string>? a, IReadOnlyList<string>? b) =>
        a is null ? b is null : b is not null && a.SequenceEqual(b, StringComparer.OrdinalIgnoreCase);
}
