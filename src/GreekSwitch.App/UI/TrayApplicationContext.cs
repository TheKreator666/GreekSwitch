using GreekSwitch.App.Policy;
using GreekSwitch.App.Settings;
using GreekSwitch.Core;
using GreekSwitch.Core.Decisions;
using GreekSwitch.Core.Loanwords;
using GreekSwitch.Windows.Focus;
using GreekSwitch.Windows.Layouts;
using GreekSwitch.Windows.Output;
using Microsoft.Extensions.Logging;

namespace GreekSwitch.App.UI;

/// <summary>Tray icon and menu (Enabled ✓, Pause for 5 min, Settings, Exit), the hotkeys and the first-run dialog.</summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private const int FlashMs = 300;
    private static readonly TimeSpan PauseLength = TimeSpan.FromMinutes(5);

    private readonly SettingsStore _store;
    private readonly ILogger _logger;
    private readonly AppPolicy _policy = new();
    private readonly IPolicyRegistry _registry;
    private readonly NeverConvertList _neverConvert;
    private readonly NeverConvertList _alwaysConvert;
    private readonly LoanwordPairStore _loanwordPairs;
    private readonly SuggestionPopup _popup = new();
    private readonly SynchronizationContext _ui;
    private readonly NotifyIcon _tray;
    private readonly ToolStripMenuItem _enabledItem;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly System.Windows.Forms.Timer _pauseTimer = new() { Interval = 1000 };
    private readonly System.Windows.Forms.Timer _flashTimer = new() { Interval = FlashMs };
    private readonly Icon _activeIcon = IconFactory.Create(active: true);
    private readonly Icon _inactiveIcon = IconFactory.Create(active: false);
    private readonly Icon _errorIcon = IconFactory.Create(active: false, error: true);
    private readonly Icon _flashIcon = IconFactory.Create(active: true, flash: true);
    private SessionEventsWindow? _sessionEvents;
    private HotkeyWindow? _hotkeys;
    private SelectionConverter? _selection;
    private AppSettings _settings;
    private ManagedPolicy _managed = ManagedPolicy.None;
    private GreekSwitchHost? _host;
    private SettingsForm? _settingsForm;

    public TrayApplicationContext(SettingsStore store, ILogger logger, IPolicyRegistry? registry = null)
    {
        _store = store;
        _logger = logger;
        _registry = registry ?? new RegistryPolicy(logger);
        _ui = SynchronizationContext.Current ?? throw new InvalidOperationException("Create on the UI thread.");
        _settings = store.Load();
        var (key, keyCreated) = LearnedWordKey.LoadOrCreate(_settings, logger);
        var hasher = new HmacWordHasher(key);
        _neverConvert = new NeverConvertList(_settings.NeverConvert, _settings.NeverConvertLearned, hasher);
        _alwaysConvert = new NeverConvertList(learned: _settings.AlwaysConvertLearned, hasher: hasher);
        _loanwordPairs = new LoanwordPairStore(_settings.LoanwordPairs, _settings.LoanwordLearned, hasher);
        System.Security.Cryptography.CryptographicOperations.ZeroMemory(key); // the hasher keeps its own copy
        if (keyCreated)
        {
            store.Save(_settings);
        }

        RefreshPolicy(); // before the hooks start, so GreekSwitch disabled by policy never captures
        _neverConvert.Changed += (_, _) => _ui.Post(_ => SaveLearned(), null);
        _alwaysConvert.Changed += (_, _) => _ui.Post(_ => SaveLearned(), null);
        _loanwordPairs.Changed += (_, _) => _ui.Post(_ => SaveLearned(), null);

        _enabledItem = new ToolStripMenuItem("&Enabled", null, (_, _) => ToggleEnabled()) { Checked = _managed.EffectiveEnabled(_settings) };
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
        _flashTimer.Tick += (_, _) =>
        {
            _flashTimer.Stop();
            UpdateUi();
        };

        _ = StartAsync();
    }

    protected override void ExitThreadCore()
    {
        _pauseTimer.Stop();
        _flashTimer.Stop();
        _hotkeys?.Dispose();
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
            _flashTimer.Dispose();
            _hotkeys?.Dispose();
            _flashIcon.Dispose();
            _popup.Dispose();
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
            var host = _host = await GreekSwitchHost.StartAsync(_settings, _managed, _policy, _neverConvert, _alwaysConvert, _loanwordPairs, _logger).ConfigureAwait(true);
            host.HealthChanged += (_, _) => _ui.Post(_ => UpdateUi(), null);
            host.Corrected += (_, _) => _ui.Post(_ => Flash(), null);
            host.SuggestionShown += (_, e) => _ui.Post(
                _ =>
                {
                    _popup.ShowSuggestion(e.Id, e.Text, e.X, e.Y);
                    host.SuggestionOpened(e.Id); // Tab accepts only what the user can see
                },
                null);
            host.SuggestionHidden += (_, _) => _ui.Post(_ => _popup.HideSuggestion(), null);
            _popup.Expired += (_, id) => host.SuggestionExpired(id);
            _sessionEvents = new SessionEventsWindow(change => _host?.NotifySessionChange(change));
            _selection = new SelectionConverter(
                new WinFormsClipboard(),
                new Win32InputSender(),
                host.Layouts,
                host.TextConverter,
                () => host.ModifierHeld,
                () => KeyboardLayouts.ForegroundLayout().FocusWindow,
                CommandWindows.IsCommandWindow,
                _logger);
            _hotkeys = new HotkeyWindow(OnHotkey, _logger);
            _hotkeys.Register(_settings);
            UpdateUi();
            if (!_settings.FirstRunCompleted)
            {
                _ui.Post(_ => ShowFirstRun(), null); // outside this try: a dialog error isn't a startup failure
            }
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

    // async void: a WM_HOTKEY handler. Every await resumes on the UI thread; an exception reaches Application.ThreadException (logged).
    private async void OnHotkey(HotkeyAction action)
    {
        if (_host is not { } host)
        {
            return;
        }

        switch (action)
        {
            case HotkeyAction.Toggle:
                ToggleEnabled();
                break;

            // Both act only where keys are captured (not in password fields or excluded apps).
            // A correction refuses to type while Ctrl or Alt is down: wait for the hotkey's release.
            case HotkeyAction.ConvertLastWord when host.CaptureActive:
                if (await SelectionConverter.WaitForReleaseAsync(() => host.ModifierHeld).ConfigureAwait(true))
                {
                    host.ConvertLastWord();
                }
                else
                {
                    _logger.LogInformation("Last word not converted: the hotkey's keys are still held");
                }

                break;
            case HotkeyAction.ConvertSelection when host.CaptureActive && _selection is not null:
                if (await _selection.ConvertAsync().ConfigureAwait(true))
                {
                    host.ForgetWord(); // the text around the caret changed under the word buffer
                    Flash();
                }

                break;
            default:
                _logger.LogInformation("Hotkey {Action} ignored: not capturing in this window", action);
                break;
        }
    }

    private void Flash()
    {
        if (!_settings.FlashOnCorrection)
        {
            return;
        }

        _tray.Icon = _flashIcon;
        _flashTimer.Stop();
        _flashTimer.Start();
    }

    private void ShowFirstRun()
    {
        using (var form = new FirstRunForm(
            KeyboardLayouts.FindInstalled(Lang.Greek) != 0,
            KeyboardLayouts.FindInstalled(Lang.English) != 0,
            _settings,
            _hotkeys?.Unavailable ?? []))
        {
            form.ShowDialog();
            if (form.StartWithWindows)
            {
                StartupRegistration.Set(true);
            }
        }

        _settings.FirstRunCompleted = true;
        _store.Save(_settings);
    }

    private void ToggleEnabled()
    {
        if (_managed.Enabled is not null)
        {
            _logger.LogInformation("Enabled not changed: it is set by policy");
            return;
        }

        _settings.Enabled = !_settings.Enabled;
        _policy.Apply(_settings, _managed);
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

        RefreshPolicy();
        UpdateUi();
        SyncLearned();
        var learned = _settings.NeverConvertLearned.Count + _settings.AlwaysConvertLearned.Count + _settings.LoanwordLearned.Count;
        using var form = _settingsForm = new SettingsForm(_settings, _managed, StartupRegistration.IsEnabled(), learned, _hotkeys?.Unavailable ?? []);
        if (form.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        _settings = form.Result;
        _neverConvert.ReplaceAll(_settings.NeverConvert);
        _loanwordPairs.ReplaceAll(_settings.LoanwordPairs);
        if (form.ForgetLearnedWords)
        {
            _neverConvert.ForgetLearned();
            _alwaysConvert.ForgetLearned();
            _loanwordPairs.ForgetLearned();
            _settings.NeverConvertLearned = [];
            _settings.AlwaysConvertLearned = [];
            _settings.LoanwordLearned = [];
        }

        _policy.Apply(_settings, _managed);
        _store.Save(_settings);
        if (form.StartWithWindows != StartupRegistration.IsEnabled())
        {
            StartupRegistration.Set(form.StartWithWindows);
        }

        _host?.Apply(_settings, _managed);
        _hotkeys?.Register(_settings);
        UpdateUi();
    }

    /// <summary>
    /// Reads the policy keys (at start and when Settings opens; on the UI thread, never in the
    /// hook) and applies them on top of the user's settings.
    /// </summary>
    private void RefreshPolicy()
    {
        var managed = ManagedPolicy.Read(_registry);
        if (managed != _managed)
        {
            managed.Log(_logger);
        }

        _managed = managed;
        _policy.Apply(_settings, _managed);
        _neverConvert.LearningEnabled = !_managed.DisableUndoLearning;
        _alwaysConvert.LearningEnabled = !_managed.DisableUndoLearning;
        _host?.Apply(_settings, _managed); // loanword suggestions and learning follow the policy too
    }

    private void SaveLearned()
    {
        SyncLearned();
        _store.Save(_settings);
    }

    private void SyncLearned()
    {
        _settings.NeverConvert = [.. _neverConvert.Snapshot()];
        _settings.NeverConvertLearned = [.. _neverConvert.LearnedSnapshot()];
        _settings.AlwaysConvertLearned = [.. _alwaysConvert.LearnedSnapshot()];
        _settings.LoanwordPairs = [.. _loanwordPairs.Snapshot()];
        _settings.LoanwordLearned = [.. _loanwordPairs.LearnedSnapshot()];
    }

    private void UpdateUi()
    {
        var enabled = _managed.EffectiveEnabled(_settings);
        _enabledItem.Checked = enabled;
        _enabledItem.Enabled = _managed.Enabled is null;
        _enabledItem.Text = _managed.Enabled is null ? "&Enabled" : "&Enabled (set by your organization)";
        _pauseItem.Enabled = enabled;
        var remaining = _policy.PausedUntilUtc - DateTime.UtcNow;
        _pauseItem.Text = _policy.IsPaused ? $"&Resume (paused, {remaining:m\\:ss} left)" : "&Pause for 5 min";
        var failed = _host?.Health == HostHealth.Failed;
        _tray.Icon = failed ? _errorIcon : _host is not null && _policy.IsActive ? _activeIcon : _inactiveIcon;
        _tray.Text = _host is null ? "GreekSwitch: loading…"
            : failed ? "GreekSwitch: error, see the log"
            : _host.Health == HostHealth.Recovering ? "GreekSwitch: recovering…"
            : !enabled ? (_managed.Enabled is null ? "GreekSwitch: disabled" : "GreekSwitch: disabled by your organization")
            : _policy.IsPaused ? "GreekSwitch: paused"
            : "GreekSwitch: on";
    }
}
