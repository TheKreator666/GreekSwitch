using GreekSwitch.App.Settings;
using GreekSwitch.Core.Decisions;
using Microsoft.Extensions.Logging;

namespace GreekSwitch.App.UI;

/// <summary>Tray icon and menu: Enabled ✓, Pause for 5 min, Settings, Exit.</summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private static readonly TimeSpan PauseLength = TimeSpan.FromMinutes(5);

    private readonly SettingsStore _store;
    private readonly ILogger _logger;
    private readonly AppPolicy _policy = new();
    private readonly NeverConvertList _neverConvert;
    private readonly SynchronizationContext _ui;
    private readonly NotifyIcon _tray;
    private readonly ToolStripMenuItem _enabledItem;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly System.Windows.Forms.Timer _pauseTimer = new() { Interval = 1000 };
    private readonly Icon _activeIcon = IconFactory.Create(active: true);
    private readonly Icon _inactiveIcon = IconFactory.Create(active: false);
    private readonly Icon _errorIcon = IconFactory.Create(active: false, error: true);
    private SessionEventsWindow? _sessionEvents;
    private AppSettings _settings;
    private GreekSwitchHost? _host;
    private SettingsForm? _settingsForm;

    public TrayApplicationContext(SettingsStore store, ILogger logger)
    {
        _store = store;
        _logger = logger;
        _ui = SynchronizationContext.Current ?? throw new InvalidOperationException("Create on the UI thread.");
        _settings = store.Load();
        _policy.Apply(_settings);
        var (key, keyCreated) = LearnedWordKey.LoadOrCreate(_settings, logger);
        _neverConvert = new NeverConvertList(_settings.NeverConvert, _settings.NeverConvertLearned, new HmacWordHasher(key));
        System.Security.Cryptography.CryptographicOperations.ZeroMemory(key); // the hasher keeps its own copy
        if (keyCreated)
        {
            store.Save(_settings);
        }

        _neverConvert.Changed += (_, _) => _ui.Post(_ => SaveNeverConvert(), null);

        _enabledItem = new ToolStripMenuItem("&Enabled", null, (_, _) => ToggleEnabled()) { Checked = _settings.Enabled };
        _pauseItem = new ToolStripMenuItem("&Pause for 5 min", null, (_, _) => TogglePause());
        var menu = new ContextMenuStrip();
        menu.Items.Add(_enabledItem);
        menu.Items.Add(_pauseItem);
        menu.Items.Add(new ToolStripMenuItem("&Settings…", null, (_, _) => ShowSettings()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("E&xit", null, (_, _) => ExitThread()));

        _tray = new NotifyIcon
        {
            Icon = _inactiveIcon,
            Text = "GreekSwitch: loading dictionaries…",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _tray.DoubleClick += (_, _) => ShowSettings();
        _pauseTimer.Tick += (_, _) => OnPauseTick();

        _ = StartAsync();
    }

    protected override void ExitThreadCore()
    {
        _pauseTimer.Stop();
        _host?.Dispose();
        _host = null;
        _tray.Visible = false;
        base.ExitThreadCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _host?.Dispose();
            _tray.Dispose();
            _pauseTimer.Dispose();
            _activeIcon.Dispose();
            _inactiveIcon.Dispose();
            _errorIcon.Dispose();
            _sessionEvents?.Dispose();
            _settingsForm?.Dispose();
        }

        base.Dispose(disposing);
    }

    private async Task StartAsync()
    {
        try
        {
            _host = await GreekSwitchHost.StartAsync(_settings, _policy, _neverConvert, _logger).ConfigureAwait(true);
            _host.HealthChanged += (_, _) => _ui.Post(_ => UpdateUi(), null);
            _sessionEvents = new SessionEventsWindow(change => _host?.NotifySessionChange(change));
            UpdateUi();
        }
#pragma warning disable CA1031 // shown to the user and logged; the app cannot run without its hooks
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError("Startup failed: {Type}{NewLine}{Stack}", ex.GetType().FullName, Environment.NewLine, ex.StackTrace);
            MessageBox.Show(
                $"GreekSwitch could not start ({ex.GetType().Name}). See {Logging.FileLogger.DefaultPath}.",
                "GreekSwitch",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            ExitThread();
        }
    }

    private void ToggleEnabled()
    {
        _settings.Enabled = !_settings.Enabled;
        _policy.Apply(_settings);
        _store.Save(_settings);
        _host?.NotifyPolicyChanged();
        UpdateUi();
    }

    private void TogglePause()
    {
        _policy.PausedUntilUtc = _policy.IsPaused ? DateTime.MinValue : DateTime.UtcNow + PauseLength;
        _pauseTimer.Enabled = _policy.IsPaused;
        _host?.NotifyPolicyChanged();
        UpdateUi();
    }

    private void OnPauseTick()
    {
        if (!_policy.IsPaused)
        {
            _pauseTimer.Stop();
            _host?.NotifyPolicyChanged();
        }

        UpdateUi();
    }

    private void ShowSettings()
    {
        if (_settingsForm is { IsDisposed: false })
        {
            _settingsForm.Activate();
            return;
        }

        SyncNeverConvert();
        using var form = _settingsForm = new SettingsForm(_settings, StartupRegistration.IsEnabled());
        if (form.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        _settings = form.Result;
        _neverConvert.ReplaceAll(_settings.NeverConvert);
        if (form.ForgetLearnedWords)
        {
            _neverConvert.ForgetLearned();
            _settings.NeverConvertLearned = [];
        }
        _policy.Apply(_settings);
        _store.Save(_settings);
        if (form.StartWithWindows != StartupRegistration.IsEnabled())
        {
            StartupRegistration.Set(form.StartWithWindows);
        }

        _host?.Apply(_settings);
        UpdateUi();
    }

    private void SaveNeverConvert()
    {
        SyncNeverConvert();
        _store.Save(_settings);
    }

    private void SyncNeverConvert()
    {
        _settings.NeverConvert = [.. _neverConvert.Snapshot()];
        _settings.NeverConvertLearned = [.. _neverConvert.LearnedSnapshot()];
    }

    private void UpdateUi()
    {
        _enabledItem.Checked = _settings.Enabled;
        _pauseItem.Enabled = _settings.Enabled;
        var remaining = _policy.PausedUntilUtc - DateTime.UtcNow;
        _pauseItem.Text = _policy.IsPaused ? $"&Resume (paused, {remaining:m\\:ss} left)" : "&Pause for 5 min";
        var failed = _host?.Health == HostHealth.Failed;
        _tray.Icon = failed ? _errorIcon : _host is not null && _policy.IsActive ? _activeIcon : _inactiveIcon;
        _tray.Text = _host is null ? "GreekSwitch: loading…"
            : failed ? "GreekSwitch: error, see the log"
            : _host.Health == HostHealth.Recovering ? "GreekSwitch: recovering…"
            : !_settings.Enabled ? "GreekSwitch: disabled"
            : _policy.IsPaused ? "GreekSwitch: paused"
            : "GreekSwitch: on";
    }
}
