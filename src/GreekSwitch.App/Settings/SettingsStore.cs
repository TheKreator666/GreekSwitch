using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace GreekSwitch.App.Settings;

/// <summary>Loads and saves settings.json. Writes go to a temp file and are then swapped in, so a crash can't leave half a file.</summary>
internal sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly ILogger _logger;
    private readonly object _gate = new();

    public SettingsStore(string directory, ILogger logger)
    {
        Directory = directory;
        FilePath = Path.Combine(directory, "settings.json");
        _logger = logger;
    }

    public static string DefaultDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GreekSwitch");

    public string Directory { get; }

    public string FilePath { get; }

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings is not null)
                {
                    if (!json.Contains("\"settingsVersion\"", StringComparison.Ordinal))
                    {
                        settings.SettingsVersion = 1; // written before versioning
                    }

                    var migrated = Migrate(settings);
                    Sanitize(settings);
                    if (migrated)
                    {
                        Save(settings);
                    }

                    return settings;
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning("settings.json could not be read ({Type}); using defaults", ex.GetType().Name);
            KeepBadCopy();
        }

        var defaults = new AppSettings();
        Save(defaults);
        return defaults;
    }

    public void Save(AppSettings settings)
    {
        lock (_gate)
        {
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                var temp = FilePath + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(settings, JsonOptions));
                File.Move(temp, FilePath, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogError("settings.json could not be saved ({Type})", ex.GetType().Name);
            }
        }
    }

    /// <summary>Saves the unreadable file as settings.json.bad, so a typo doesn't silently wipe the user's settings.</summary>
    private void KeepBadCopy()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                File.Copy(FilePath, FilePath + ".bad", overwrite: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning("settings.json.bad could not be written ({Type})", ex.GetType().Name);
        }
    }

    /// <summary>Brings an older file up to date; returns true if anything changed.</summary>
    private bool Migrate(AppSettings s)
    {
        if (s.SettingsVersion >= AppSettings.CurrentVersion)
        {
            return false;
        }

        if (s.SettingsVersion < 2)
        {
            var current = new HashSet<string>(s.ExcludedProcesses ?? [], StringComparer.OrdinalIgnoreCase);
            s.ExcludedProcesses = [.. s.ExcludedProcesses ?? [], .. AppSettings.AddedInVersion2.Where(p => !current.Contains(p))];
        }

        _logger.LogInformation("settings.json migrated from version {From} to {To}", s.SettingsVersion, AppSettings.CurrentVersion);
        s.SettingsVersion = AppSettings.CurrentVersion;
        return true;
    }

    private static AppSettings Sanitize(AppSettings s)
    {
        s.MinLength = Math.Clamp(s.MinLength, 1, 20);
        s.ReplaceThreshold = Math.Clamp(s.ReplaceThreshold, 0, 20);
        s.ExcludedProcesses = [.. (s.ExcludedProcesses ?? []).Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim())];
        s.NeverConvert = [.. (s.NeverConvert ?? []).Where(w => !string.IsNullOrWhiteSpace(w)).Select(w => w.Trim())];
        return s;
    }
}
