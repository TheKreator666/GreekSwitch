using GreekSwitch.App.Settings;
using GreekSwitch.Core;
using GreekSwitch.Core.Decisions;
using GreekSwitch.Core.Input;
using GreekSwitch.Core.Layout;
using GreekSwitch.Core.Scoring;
using GreekSwitch.Core.Session;
using GreekSwitch.Windows;
using GreekSwitch.Windows.Focus;
using GreekSwitch.Windows.Health;
using GreekSwitch.Windows.Hooks;
using GreekSwitch.Windows.Layouts;
using GreekSwitch.Windows.Output;
using Microsoft.Extensions.Logging;

namespace GreekSwitch.App;

internal enum HostHealth
{
    Healthy,

    /// <summary>A hook was reinstalled or the worker restarted; checking that it works again.</summary>
    Recovering,

    /// <summary>Recovery didn't help: corrections may not be happening. The tray shows an error.</summary>
    Failed,
}

/// <summary>What happened to the user's session, from WM_WTSSESSION_CHANGE / WM_POWERBROADCAST.</summary>
internal enum SessionChange
{
    Locked,
    Unlocked,
    Suspending,
    Resumed,
}

/// <summary>
/// Composition root: builds the pipeline, starts the hooks and the worker, keeps them healthy
/// (watchdog, worker restart), and tears them down.
/// </summary>
internal sealed class GreekSwitchHost : IDisposable
{
    private const int MaxWorkerRestarts = 3;
    private const int MaxHookReinstalls = 3;
    private static readonly TimeSpan WatchdogInterval = TimeSpan.FromSeconds(5);

    private readonly InputMonitor _monitor;
    private readonly UiaThread _uia;
    private readonly DecisionEngine _engine;
    private readonly TypingSession _session;
    private readonly CorrectionWorker _workerLoop;
    private readonly CancellationTokenSource _stop = new();
    private readonly ILogger _logger;
    private readonly System.Threading.Timer _watchdog;
    private Task _worker = Task.CompletedTask;
    private int _workerRestarts;
    private bool _workerDead;
    private int _silentChecks;
    private volatile bool _sessionLocked;
    private volatile HostHealth _health = HostHealth.Healthy;

    private GreekSwitchHost(InputMonitor monitor, UiaThread uia, DecisionEngine engine, TypingSession session, CorrectionWorker worker, ILogger logger)
    {
        _uia = uia;
        _monitor = monitor;
        _engine = engine;
        _session = session;
        _workerLoop = worker;
        _logger = logger;
        StartWorker();
        _watchdog = new System.Threading.Timer(_ => CheckHooks(), null, WatchdogInterval, WatchdogInterval);
    }

    /// <summary>Raised (on a background thread) when <see cref="Health"/> changes.</summary>
    public event EventHandler? HealthChanged;

    public HostHealth Health => _health;

    public static async Task<GreekSwitchHost> StartAsync(AppSettings settings, AppPolicy policy, NeverConvertList neverConvert, ILogger logger)
    {
        // Load the language data (and check the layouts) off the UI thread.
        var (dictionaries, trigrams, frequencies, greek, english) = await Task.Run(() =>
        {
            return (LexiconDictionaryService.FromEmbeddedResources(),
                TrigramModels.FromEmbeddedResources(),
                WordFrequencyTable.FromEmbeddedResources(),
                LayoutVerifier.VerifyInstalled(StaticLayouts.Greek, logger),
                LayoutVerifier.VerifyInstalled(StaticLayouts.English, logger));
        }).ConfigureAwait(true);

        var engine = new DecisionEngine(new DualDecoder(greek, english), new Scorer(dictionaries, trigrams, frequencies), neverConvert, ToScoring(settings));
        var layouts = new LayoutSwitcher(logger);
        var uia = new UiaThread();
        var monitor = new InputMonitor
        {
#if DEBUG
            AcceptForeignInjectedInput = Environment.GetEnvironmentVariable("GREEKSWITCH_TEST_ACCEPT_INJECTED") == "1",
#endif
        };
        if (monitor.AcceptForeignInjectedInput)
        {
            logger.LogWarning("TEST MODE: processing keys injected by other programs");
        }

        var session = new TypingSession(
            engine,
            new KeyClassifier(greek, english),
            new Corrector(
                layouts,
                lang => lang == Lang.Greek ? greek : english,
                monitor.TypeAhead,
                monitor.Keyboard,
                logger,
                monitor.Focus,
                textVerifier: new UiaTextVerifier(uia))
            {
                SwitchSettle = TimeSpan.FromMilliseconds(Math.Clamp(settings.LayoutSwitchSettleMs, 0, 250)),
            },
            neverConvert,
            logger,
            ToSession(settings));

        try
        {
            await monitor.StartAsync().ConfigureAwait(true);
        }
        catch
        {
            monitor.Dispose();
            uia.Dispose();
            throw;
        }

        var worker = new CorrectionWorker(monitor, session, engine, layouts, new PasswordFieldDetector(), policy, uia, logger);
        logger.LogInformation("GreekSwitch started");
        return new GreekSwitchHost(monitor, uia, engine, session, worker, logger);
    }

    /// <summary>Applies changed settings. Safe from the UI thread: options are swapped atomically.</summary>
    public void Apply(AppSettings settings)
    {
        _engine.Options = ToScoring(settings);
        _session.Options = ToSession(settings);
        NotifyPolicyChanged();
    }

    public void NotifyPolicyChanged() => _monitor.NotifyPolicyChanged();

    /// <summary>Lock/unlock/sleep/resume: forget the word in progress; after resume, reinstall the hooks.</summary>
    public void NotifySessionChange(SessionChange change)
    {
        _monitor.Post(InputEvent.SessionChange());
        switch (change)
        {
            case SessionChange.Locked or SessionChange.Suspending:
                _sessionLocked = true; // input goes to the secure desktop, where the hooks can't see it
                break;
            case SessionChange.Unlocked:
                _sessionLocked = false;
                _monitor.Activity.Touch();
                break;
            case SessionChange.Resumed:
                _sessionLocked = false;
                _logger.LogInformation("Resumed from sleep: reinstalling hooks");
                _monitor.Reinstall();
                break;
        }
    }

    public void Dispose()
    {
        _watchdog.Dispose();
        _stop.Cancel();
        _monitor.Dispose(); // unhooks on the hook thread and completes the channel
        try
        {
            _worker.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is OperationCanceledException))
        {
            // Expected on shutdown.
        }

        _stop.Dispose();
        _uia.Dispose();
        _logger.LogInformation("GreekSwitch stopped");
    }

    private static ScoringOptions ToScoring(AppSettings s) => new()
    {
        MinLength = s.MinLength,
        ReplaceThreshold = s.ReplaceThreshold,
        DictionaryWeight = s.DictionaryWeight,
        NgramWeight = s.NgramWeight,
        ContextWeight = s.ContextWeight,
    };

    private static SessionOptions ToSession(AppSettings s) => new()
    {
        CorrectOnEnterAndTab = s.CorrectOnEnterAndTab,
        DebugLogText = s.DebugLogText,
    };

    private void StartWorker()
    {
        _worker = Task.Run(() => _workerLoop.RunAsync(_stop.Token));
        _worker.ContinueWith(OnWorkerFaulted, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
    }

    private void OnWorkerFaulted(Task faulted)
    {
        var error = faulted.Exception?.InnerException;
        _logger.LogError("Worker stopped unexpectedly: {Type}{NewLine}{Stack}", error?.GetType().FullName, Environment.NewLine, error?.StackTrace);
        if (_stop.IsCancellationRequested)
        {
            return;
        }

        if (++_workerRestarts <= MaxWorkerRestarts)
        {
            _logger.LogWarning("Restarting the worker (attempt {Attempt} of {Max})", _workerRestarts, MaxWorkerRestarts);
            SetHealth(HostHealth.Recovering);
            StartWorker();
        }
        else
        {
            _workerDead = true;
            SetHealth(HostHealth.Failed);
        }
    }

    private void CheckHooks()
    {
        if (_stop.IsCancellationRequested || _sessionLocked)
        {
            return;
        }

        if (!HookWatchdog.HooksLookSilent(HookWatchdog.LastInputTick(), _monitor.Activity.LastTick))
        {
            _silentChecks = 0;
            SetHealth(_workerDead ? HostHealth.Failed : HostHealth.Healthy);
            return;
        }

        if (++_silentChecks <= MaxHookReinstalls)
        {
            _logger.LogWarning("Input the hooks didn't see: reinstalling them (attempt {Attempt} of {Max})", _silentChecks, MaxHookReinstalls);
            SetHealth(HostHealth.Recovering);
            _monitor.Reinstall();
        }
        else
        {
            if (_health != HostHealth.Failed)
            {
                _logger.LogError("Keyboard hook still not receiving input after {Max} reinstalls", MaxHookReinstalls);
            }

            SetHealth(HostHealth.Failed);
        }
    }

    private void SetHealth(HostHealth health)
    {
        if (_health == health)
        {
            return;
        }

        _health = health;
        _logger.LogInformation("Health: {Health}", health);
        HealthChanged?.Invoke(this, EventArgs.Empty);
    }
}
