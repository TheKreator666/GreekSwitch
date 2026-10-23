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
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions);
                if (settings is not null)
                {
                    return Sanitize(settings);
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning("settings.json could not be read ({Type}); using defaults", ex.GetType().Name);
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

    private static AppSettings Sanitize(AppSettings s)
    {
        s.MinLength = Math.Clamp(s.MinLength, 1, 20);
        s.ReplaceThreshold = Math.Clamp(s.ReplaceThreshold, 0, 20);
        s.ExcludedProcesses = [.. (s.ExcludedProcesses ?? []).Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim())];
        s.NeverConvert = [.. (s.NeverConvert ?? []).Where(w => !string.IsNullOrWhiteSpace(w)).Select(w => w.Trim())];
        return s;
    }
}
