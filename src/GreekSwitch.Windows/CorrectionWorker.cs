using GreekSwitch.Core.Decisions;
using GreekSwitch.Core.Layout;
using GreekSwitch.Core.Session;
using GreekSwitch.Windows.Focus;
using GreekSwitch.Windows.Hooks;
using GreekSwitch.Windows.Layouts;
using GreekSwitch.Windows.Output;
using Microsoft.Extensions.Logging;
using static GreekSwitch.Windows.Native.NativeMethods;

namespace GreekSwitch.Windows;

/// <summary>
/// Consumes hook events on a worker task and drives the <see cref="TypingSession"/>. It decides
/// when to be fully passive (disabled, paused, excluded app, password field) and then tells the
/// hook to stop capturing keys at all. Password checks run on the UI Automation thread and come
/// back as events; until one answers, capture stays off (fail closed). A failure while
/// processing one event is logged (type and stack trace, never text), the buffer is reset, and
/// the worker carries on.
/// </summary>
public sealed class CorrectionWorker
{
    private readonly InputMonitor _monitor;
    private readonly TypingSession _session;
    private readonly DecisionEngine _engine;
    private readonly ILayoutSwitcher _layouts;
    private readonly IPasswordFieldDetector _passwords;
    private readonly ICorrectionPolicy _policy;
    private static readonly TimeSpan PasswordCheckTimeout = TimeSpan.FromSeconds(3);

    private readonly ILogger _logger;
    private readonly UiaThread _uia;
    private readonly PasswordGate _gate = new();
    private readonly IInputSender _sender;
    private bool _excluded;
    private long _droppedWhenCaptureStopped;

    public CorrectionWorker(
        InputMonitor monitor,
        TypingSession session,
        DecisionEngine engine,
        ILayoutSwitcher layouts,
        IPasswordFieldDetector passwords,
        ICorrectionPolicy policy,
        UiaThread uia,
        ILogger logger,
        IInputSender? sender = null)
    {
        _sender = sender ?? new Win32InputSender();
        _uia = uia;
        _monitor = monitor;
        _session = session;
        _engine = engine;
        _layouts = layouts;
        _passwords = passwords;
        _policy = policy;
        _logger = logger;
    }

    /// <summary>No key for this long while accents are pending: the user paused, so add them.</summary>
    public static readonly TimeSpan PauseBeforeAccents = TimeSpan.FromMilliseconds(500);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        OnForegroundChanged(GetForegroundWindow());
        var reader = _monitor.Events;
        Task<bool>? next = null; // one outstanding wait at a time (single-reader channel)
        while (true)
        {
            next ??= reader.WaitToReadAsync(cancellationToken).AsTask();
            if (_session.AccentsPending
                && await Task.WhenAny(next, Task.Delay(PauseBeforeAccents, cancellationToken)).ConfigureAwait(false) != next
                && !cancellationToken.IsCancellationRequested)
            {
                Run(_session.OnPause);
                continue;
            }

            if (!await next.ConfigureAwait(false))
            {
                return; // the channel was completed
            }

            next = null;
            while (reader.TryRead(out var input))
            {
                Run(() => Handle(input));
            }
        }
    }

    /// <summary>Runs one step of the pipeline; a failure is logged and the session reset.</summary>
    private void Run(Action step)
    {
        try
        {
            step();
        }
#pragma warning disable CA1031 // keep the pipeline alive; the failure is logged, not swallowed
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError("Pipeline failed: {Type}{NewLine}{Stack}", ex.GetType().FullName, Environment.NewLine, ex.StackTrace);
            _session.Reset();
        }
    }

    private void Handle(InputEvent input)
    {
        switch (input.Kind)
        {
            case InputEventKind.Key:
                OnKey(input);
                break;
            case InputEventKind.MouseClick:
                _session.Reset();
                break;
            case InputEventKind.ForegroundChanged:
                LogFocusEvent(input);
                OnForegroundChanged(input.Window);
                break;
            case InputEventKind.FocusChanged:
                LogFocusEvent(input);
                OnFocusChanged(new FocusKey(input.Window, input.ChildId));
                break;
            case InputEventKind.PolicyChanged:
                _session.Reset();
                Refocus(_gate.Current);
                break;
            case InputEventKind.PasswordChecked:
                OnPasswordChecked(new FocusKey(input.Window, input.ChildId), input.Check);
                break;
            case InputEventKind.SessionReset:
                _session.Reset();
                _engine.ClearContext();
                break;
            case InputEventKind.ConvertLastWord:
                // Never in a password field or an excluded app: nothing was captured there anyway.
                if (_monitor.CaptureKeys)
                {
                    _session.ConvertLastWord();
                }

                break;
            case InputEventKind.AcceptSuggestion:
                // The hook swallowed a Tab for a suggestion that may have gone meanwhile (a key or
                // focus change raced it): then give the user their Tab back.
                if (!_monitor.CaptureKeys || !_session.AcceptSuggestion())
                {
                    PassTabOn();
                    _session.Reset(); // the session never sees our own Tab: it ends the word all the same
                }

                break;
            case InputEventKind.HookError:
                _logger.LogError("Keyboard hook callback failed: {Type}", input.ErrorType);
                _session.Reset();
                break;
        }
    }

    private void OnKey(InputEvent input)
    {
        _monitor.TypeAhead.MarkProcessed(input.TypedCount);
        _monitor.Focus.Expect(input.Window);

        // Keys queued just before the hook was told to stop capturing are dropped here.
        if (!_monitor.CaptureKeys)
        {
            return;
        }

        // The layout when the key was pressed (read in the hook), not when the worker got to it.
        if (input.KeyLanguage(_layouts.CurrentLayout) is not { } layout)
        {
            _session.Reset(); // a third layout is active: stay out of the way
            return;
        }

        _session.OnKey(input.Key, layout);
    }

    /// <summary>Diagnostics for focus churn: the event kind and whether the window is our own (never which app).</summary>
    private void LogFocusEvent(InputEvent input)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            GetWindowThreadProcessId(input.Window, out var pid);
            _logger.LogDebug("{Kind} event, own window {Own}", input.Kind, pid == Environment.ProcessId);
        }
    }

    private void PassTabOn()
    {
        // Never Tab into a terminal or IDE (suggestions aren't shown there, so this is a last guard).
        var (_, focus) = KeyboardLayouts.ForegroundLayout();
        if (CommandWindows.IsCommandWindow(focus))
        {
            _logger.LogInformation("Swallowed Tab not passed on: terminal or IDE");
            return;
        }

        var tab = InputBuilder.Chord([VirtualKeys.Tab]);
        if (_sender.Send(tab) != tab.Length)
        {
            _logger.LogWarning("Swallowed Tab could not be passed on");
        }
    }

    private void OnForegroundChanged(nint window)
    {
        _session.Reset();
        _engine.ClearContext();
        var process = ProcessNames.FromWindow(window);
        _excluded = process is not null && _policy.IsExcluded(process);
        _session.SuggestOnly = process is not null && _policy.IsSuggestOnly(process);
        var focus = KeyboardLayouts.ForegroundLayout().FocusWindow;
        Refocus(new FocusKey(focus != 0 ? focus : window, 0));
    }

    private void OnFocusChanged(FocusKey key)
    {
        _session.Reset();
        var process = ProcessNames.FromWindow(GetForegroundWindow());
        _excluded = process is not null && _policy.IsExcluded(process);
        _session.SuggestOnly = process is not null && _policy.IsSuggestOnly(process);
        Refocus(key);
    }

    /// <summary>Focus (or the policy) changed: block capture and ask UI Automation, unless the answer is known.</summary>
    private void Refocus(FocusKey key)
    {
        if (_gate.OnFocus(key, wouldCapture: _policy.IsActive && !_excluded))
        {
            _ = _uia.RunAsync(_passwords.CheckFocused, PasswordCheckTimeout).ContinueWith(
                t => _monitor.Post(InputEvent.PasswordChecked(key, t.Result.Answered ? t.Result.Value : null)),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnRanToCompletion,
                TaskScheduler.Default);
        }

        UpdateCapture();
    }

    private void OnPasswordChecked(FocusKey key, PasswordCheck? check)
    {
        if (check is null)
        {
            _logger.LogWarning("Password-field check timed out; staying passive in this field");
        }

        if (_gate.OnAnswer(key, check))
        {
            UpdateCapture();
        }
    }

    private void UpdateCapture()
    {
        var capture = _policy.IsActive && !_excluded && !_gate.Blocked;
        if (capture != _monitor.CaptureKeys)
        {
            if (capture && _monitor.DroppedKeys != _droppedWhenCaptureStopped)
            {
                _session.MarkPartial(); // keys were typed while capture was off
            }
            else if (!capture)
            {
                _droppedWhenCaptureStopped = _monitor.DroppedKeys;
            }

            _monitor.CaptureKeys = capture;

            // Categories only: never the app name or any text.
            var reason = capture ? "" : !_policy.IsActive ? ": disabled or paused" : _excluded ? ": excluded app"
                : _gate.Pending ? ": checking for a password field" : ": password field";
            _logger.LogInformation("Key capture {State}{Reason}", capture ? "on" : "off (passive)", reason);
        }

        if (!capture)
        {
            _session.Reset();
        }
    }
}
