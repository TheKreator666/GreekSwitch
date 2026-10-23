using GreekSwitch.App.Settings;
using GreekSwitch.Core.Decisions;
using GreekSwitch.Core.Input;
using GreekSwitch.Core.Layout;
using GreekSwitch.Core.Scoring;
using GreekSwitch.Core.Session;
using GreekSwitch.Windows;
using GreekSwitch.Windows.Focus;
using GreekSwitch.Windows.Hooks;
using GreekSwitch.Windows.Layouts;
using GreekSwitch.Windows.Output;
using Microsoft.Extensions.Logging;

namespace GreekSwitch.App;

/// <summary>Composition root: builds the pipeline, starts the hooks and the worker, tears them down.</summary>
internal sealed class GreekSwitchHost : IDisposable
{
    private readonly InputMonitor _monitor;
    private readonly DecisionEngine _engine;
    private readonly TypingSession _session;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _worker;
    private readonly ILogger _logger;

    private GreekSwitchHost(InputMonitor monitor, DecisionEngine engine, TypingSession session, CorrectionWorker worker, ILogger logger)
    {
        _monitor = monitor;
        _engine = engine;
        _session = session;
        _logger = logger;
        _worker = Task.Run(() => worker.RunAsync(_stop.Token));
        _worker.ContinueWith(
            t => _logger.LogError("Worker stopped unexpectedly: {Type}", t.Exception?.InnerException?.GetType().FullName),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    public static async Task<GreekSwitchHost> StartAsync(AppSettings settings, AppPolicy policy, NeverConvertList neverConvert, ILogger logger)
    {
        // Loading el_GR takes about a second; do it (and the layout check) off the UI thread.
        var (dictionaries, trigrams, greek, english) = await Task.Run(() =>
        {
            var dictionaries = HunspellDictionaryService.FromEmbeddedResources();
            dictionaries.Preload();
            return (dictionaries,
                TrigramModels.FromEmbeddedResources(),
                LayoutVerifier.VerifyInstalled(StaticLayouts.Greek, logger),
                LayoutVerifier.VerifyInstalled(StaticLayouts.English, logger));
        }).ConfigureAwait(true);

        var engine = new DecisionEngine(new DualDecoder(greek, english), new Scorer(dictionaries, trigrams), neverConvert, ToScoring(settings));
        var layouts = new LayoutSwitcher(logger);
        var session = new TypingSession(
            engine,
            new KeyClassifier(greek, english),
            new Corrector(layouts, logger),
            neverConvert,
            logger,
            ToSession(settings));

        var monitor = new InputMonitor();
        try
        {
            await monitor.StartAsync().ConfigureAwait(true);
        }
        catch
        {
            monitor.Dispose();
            throw;
        }

        var worker = new CorrectionWorker(monitor, session, engine, layouts, new PasswordFieldDetector(), policy, logger);
        logger.LogInformation("GreekSwitch started");
        return new GreekSwitchHost(monitor, engine, session, worker, logger);
    }

    /// <summary>Applies changed settings. Safe from the UI thread: options are swapped atomically.</summary>
    public void Apply(AppSettings settings)
    {
        _engine.Options = ToScoring(settings);
        _session.Options = ToSession(settings);
        NotifyPolicyChanged();
    }

    public void NotifyPolicyChanged() => _monitor.NotifyPolicyChanged();

    public void Dispose()
    {
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
}
