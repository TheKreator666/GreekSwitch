using GreekSwitch.App.Settings;
using Microsoft.Extensions.Logging.Abstractions;

namespace GreekSwitch.App.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "GreekSwitchTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void Missing_file_gives_defaults_and_creates_the_file()
    {
        var store = new SettingsStore(_dir, NullLogger.Instance);

        var settings = store.Load();

        settings.Enabled.Should().BeTrue();
        File.Exists(store.FilePath).Should().BeTrue();
    }

    [Fact]
    public void Round_trips_settings()
    {
        var store = new SettingsStore(_dir, NullLogger.Instance);
        var settings = store.Load();
        settings.MinLength = 4;
        settings.NeverConvert = ["Καλημέρα"];
        store.Save(settings);

        var loaded = new SettingsStore(_dir, NullLogger.Instance).Load();

        loaded.MinLength.Should().Be(4);
        loaded.NeverConvert.Should().Equal("Καλημέρα");
    }

    [Fact]
    public void A_broken_file_is_kept_as_bad_before_defaults_are_written()
    {
        Directory.CreateDirectory(_dir);
        var store = new SettingsStore(_dir, NullLogger.Instance);
        const string broken = "{ \"enabled\": false, \"minLength\": 4, oops }";
        File.WriteAllText(store.FilePath, broken);

        var settings = store.Load();

        settings.Enabled.Should().BeTrue("defaults are used");
        File.ReadAllText(store.FilePath + ".bad").Should().Be(broken, "the user's edits must not be lost");
    }

    [Fact]
    public void Out_of_range_values_are_clamped()
    {
        Directory.CreateDirectory(_dir);
        var store = new SettingsStore(_dir, NullLogger.Instance);
        File.WriteAllText(store.FilePath, "{ \"minLength\": 99, \"replaceThreshold\": -5 }");

        var settings = store.Load();

        settings.MinLength.Should().Be(20);
        settings.ReplaceThreshold.Should().Be(0);
    }

    [Fact]
    public void Older_settings_get_new_default_exclusions_added_once_without_losing_choices()
    {
        Directory.CreateDirectory(_dir);
        var store = new SettingsStore(_dir, NullLogger.Instance);
        File.WriteAllText(store.FilePath, "{ \"excludedProcesses\": [\"cmd.exe\", \"myapp.exe\"] }");

        var settings = store.Load();

        settings.ExcludedProcesses.Should().Contain(["cmd.exe", "myapp.exe", "mstsc.exe", "wfica32.exe"]);
        settings.ExcludedProcesses.Should().NotContain("steam.exe", "only defaults added in later versions are merged in");
        settings.SettingsVersion.Should().Be(AppSettings.CurrentVersion);

        // The user then removes one: it must stay removed on the next load.
        settings.ExcludedProcesses.Remove("mstsc.exe");
        store.Save(settings);
        new SettingsStore(_dir, NullLogger.Instance).Load().ExcludedProcesses.Should().NotContain("mstsc.exe");
    }

    [Fact]
    public void New_installs_get_the_first_run_dialog_and_existing_ones_do_not()
    {
        new SettingsStore(_dir, NullLogger.Instance).Load().FirstRunCompleted.Should().BeFalse("a new install");

        var existing = Path.Combine(_dir, "existing");
        Directory.CreateDirectory(existing);
        File.WriteAllText(Path.Combine(existing, "settings.json"), "{ \"settingsVersion\": 2 }");
        new SettingsStore(existing, NullLogger.Instance).Load().FirstRunCompleted.Should().BeTrue("set up before the dialog existed");
    }

    [Fact]
    public void Hotkey_defaults_parse()
    {
        var settings = new AppSettings();

        foreach (var hotkey in new[] { settings.ConvertLastWordHotkey, settings.ConvertSelectionHotkey, settings.ToggleHotkey })
        {
            GreekSwitch.Core.Input.HotkeyText.Parse(hotkey).Should().NotBeNull(hotkey);
        }
    }

    [Fact]
    public void Remote_desktop_and_vm_clients_are_excluded_by_default()
    {
        AppSettings.DefaultExcludedProcesses.Should().Contain(["mstsc.exe", "wfica32.exe", "CDViewer.exe", "vmconnect.exe", "AnyDesk.exe", "TeamViewer.exe"]);
    }
}
